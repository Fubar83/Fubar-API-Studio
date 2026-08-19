using Fubar.Studio.Core.Models;

namespace Fubar.Studio.Core.Auth;

/// <summary>The result of ensuring/testing auth: whether it succeeded, a human-readable message
/// (e.g. "Token acquired, expires 2026-08-17 12:00Z" or the token-endpoint error), and - for OAuth 2.0
/// token requests - the complete HTTP response (status + body) so a "Test" action can show it verbatim
/// for verification. <see cref="Details"/> is null when there was no HTTP exchange (e.g. a cached token
/// or a non-OAuth scheme).</summary>
public sealed record AuthOutcome(bool Ok, string Message, string? Details = null);

/// <summary>
/// Makes an <see cref="AuthConfig"/>'s dynamic auth ready before a request is sent: for OAuth 2.0 it
/// reuses a cached, unexpired token from the session store, or acquires/refreshes one and stashes the
/// access token + expiry (+ refresh token) into session variables (auto-mapping). Other schemes are
/// static and need nothing here. Also drives the Auth tab's "Test" button.
/// </summary>
public interface IAuthProvider
{
    /// <summary><paramref name="forceRefresh"/> bypasses the cached-token shortcut so the Test button
    /// always makes a real token request and can show the complete response.</summary>
    Task<AuthOutcome> EnsureAsync(AuthConfig auth, Workspace workspace, WorkspaceEnvironment? activeEnvironment, CancellationToken cancellationToken = default, bool forceRefresh = false);

    /// <summary>Resolves the auth's variables and returns a preview of the OAuth token request that would
    /// be sent (secrets masked, no network call), for a "Verify request" button - so the user can confirm
    /// the endpoint/body/credentials are right before testing.</summary>
    string PreviewTokenRequest(AuthConfig auth, Workspace workspace, WorkspaceEnvironment? activeEnvironment);
}
