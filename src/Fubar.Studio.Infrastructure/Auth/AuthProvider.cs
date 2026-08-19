using Fubar.Studio.Core.Auth;
using Fubar.Studio.Core.Models;
using Fubar.Studio.Core.Variables;

namespace Fubar.Studio.Infrastructure.Auth;

/// <summary>
/// Ensures an OAuth 2.0 <see cref="AuthConfig"/> has a fresh access token before a request is sent (and
/// backs the Auth tab's Test button). Reuses a cached token from the session store while it's unexpired;
/// otherwise acquires one (client credentials) or refreshes it (refresh-token grant, using a stored or
/// configured refresh token), then auto-maps the access token / expiry / refresh token into
/// session-only variables so <c>{{oauth2_access_token}}</c> resolves in the Authorization header.
/// Non-OAuth2 schemes are static and need nothing here.
/// </summary>
public sealed class AuthProvider : IAuthProvider
{
    // Tokens are re-fetched this long before they actually expire, to avoid sending a just-expired one.
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromSeconds(30);

    private readonly IOAuthTokenService _tokenService;
    private readonly IVariableResolver _resolver;
    private readonly ISessionVariableStore _session;

    public AuthProvider(IOAuthTokenService tokenService, IVariableResolver resolver, ISessionVariableStore session)
    {
        _tokenService = tokenService;
        _resolver = resolver;
        _session = session;
    }

    public async Task<AuthOutcome> EnsureAsync(AuthConfig auth, Workspace workspace, WorkspaceEnvironment? activeEnvironment, CancellationToken cancellationToken = default, bool forceRefresh = false)
    {
        if (auth.Type != AuthType.OAuth2)
        {
            return new AuthOutcome(true, "");
        }

        var tokenVariable = string.IsNullOrWhiteSpace(auth.AccessTokenVariable) ? AuthDefaults.AccessTokenVariable : auth.AccessTokenVariable!;
        var expiryVariable = string.IsNullOrWhiteSpace(auth.ExpiryVariable) ? AuthDefaults.ExpiryVariable : auth.ExpiryVariable!;
        var refreshVariable = $"{tokenVariable}_refresh";

        // Reuse a cached, still-valid token.
        var cachedToken = _session.Get(workspace.WorkspaceId, tokenVariable);
        if (!forceRefresh && !string.IsNullOrEmpty(cachedToken) && !IsExpired(_session.Get(workspace.WorkspaceId, expiryVariable)))
        {
            return new AuthOutcome(true, "Using the cached token (still valid).");
        }

        string Resolve(string? value) => _resolver.Substitute(value, workspace, activeEnvironment);

        var tokenUrl = Resolve(auth.TokenUrl);
        if (string.IsNullOrWhiteSpace(tokenUrl))
        {
            return new AuthOutcome(false, "OAuth2: a token URL is required.");
        }

        // For the refresh grant, prefer a stored refresh token (from a previous acquire), else the configured one.
        var refreshToken = auth.OAuth2Grant == OAuth2GrantType.RefreshToken
            ? (_session.Get(workspace.WorkspaceId, refreshVariable) is { Length: > 0 } stored ? stored : Resolve(auth.RefreshToken))
            : null;

        var request = new OAuth2TokenRequest(
            auth.OAuth2Grant,
            tokenUrl,
            Resolve(auth.ClientId),
            Resolve(auth.ClientSecret),
            Resolve(auth.Scopes),
            refreshToken,
            auth.ClientAuthentication);

        try
        {
            var result = await _tokenService.AcquireAsync(request, cancellationToken);

            _session.Set(workspace.WorkspaceId, tokenVariable, result.AccessToken);
            _session.Set(workspace.WorkspaceId, expiryVariable, result.ExpiresAt?.ToUnixTimeSeconds().ToString());
            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                _session.Set(workspace.WorkspaceId, refreshVariable, result.RefreshToken);
            }

            var expiryText = result.ExpiresAt is { } expiresAt
                ? $"expires {expiresAt.UtcDateTime:yyyy-MM-dd HH:mm}Z"
                : "no expiry reported";
            return new AuthOutcome(true, $"Token acquired into {{{{{tokenVariable}}}}} ({expiryText}).",
                FormatResponse(result.StatusCode, result.RawResponse));
        }
        catch (OAuthTokenException ex)
        {
            // The endpoint replied but rejected the request - show the complete response so the user can
            // read the error (e.g. invalid_client, invalid_scope).
            return new AuthOutcome(false, $"OAuth2 token request failed: {ex.Message}",
                FormatResponse(ex.StatusCode, ex.RawResponse));
        }
        catch (Exception ex)
        {
            // No HTTP exchange (DNS/timeout/etc.) - there's no response body to show.
            return new AuthOutcome(false, $"OAuth2 token request failed: {ex.Message}", ex.Message);
        }
    }

    private static string FormatResponse(int statusCode, string body) =>
        string.IsNullOrEmpty(body) ? $"HTTP {statusCode}" : $"HTTP {statusCode}\n\n{body}";

    public string PreviewTokenRequest(AuthConfig auth, Workspace workspace, WorkspaceEnvironment? activeEnvironment)
    {
        if (auth.Type != AuthType.OAuth2)
        {
            return "Request verification is only available for OAuth 2.0.";
        }

        string Resolve(string? value) => _resolver.Substitute(value, workspace, activeEnvironment);

        var tokenUrl = Resolve(auth.TokenUrl);
        if (string.IsNullOrWhiteSpace(tokenUrl))
        {
            return "Set a token URL to preview the request.";
        }

        var request = new OAuth2TokenRequest(
            auth.OAuth2Grant,
            tokenUrl,
            Resolve(auth.ClientId),
            Resolve(auth.ClientSecret),
            Resolve(auth.Scopes),
            auth.OAuth2Grant == OAuth2GrantType.RefreshToken ? Resolve(auth.RefreshToken) : null,
            auth.ClientAuthentication);

        var tokenVariable = string.IsNullOrWhiteSpace(auth.AccessTokenVariable) ? AuthDefaults.AccessTokenVariable : auth.AccessTokenVariable!;
        return $"{_tokenService.Describe(request)}\n\n→ each request then sends:  Authorization: Bearer {{{{{tokenVariable}}}}}";
    }

    private static bool IsExpired(string? expiryUnixSeconds)
    {
        if (string.IsNullOrEmpty(expiryUnixSeconds))
        {
            return false; // no expiry known - treat the cached token as usable
        }

        return long.TryParse(expiryUnixSeconds, out var seconds)
            && DateTimeOffset.FromUnixTimeSeconds(seconds) - ExpiryBuffer <= DateTimeOffset.UtcNow;
    }
}
