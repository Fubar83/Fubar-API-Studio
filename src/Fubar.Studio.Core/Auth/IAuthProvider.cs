using Fubar.Studio.Core.Models;

namespace Fubar.Studio.Core.Auth;

/// <summary>The result of ensuring/testing auth: whether it succeeded and a human-readable message
/// (e.g. "Token acquired, expires 2026-08-17 12:00Z" or the token-endpoint error).</summary>
public sealed record AuthOutcome(bool Ok, string Message);

/// <summary>
/// Makes an <see cref="AuthConfig"/>'s dynamic auth ready before a request is sent: for OAuth 2.0 it
/// reuses a cached, unexpired token from the session store, or acquires/refreshes one and stashes the
/// access token + expiry (+ refresh token) into session variables (auto-mapping). Other schemes are
/// static and need nothing here. Also drives the Auth tab's "Test" button.
/// </summary>
public interface IAuthProvider
{
    Task<AuthOutcome> EnsureAsync(AuthConfig auth, Workspace workspace, WorkspaceEnvironment? activeEnvironment, CancellationToken cancellationToken = default);

    /// <summary>Resolves the auth's variables and returns a preview of the OAuth token request that would
    /// be sent (secrets masked, no network call), for a "Verify request" button - so the user can confirm
    /// the endpoint/body/credentials are right before testing.</summary>
    string PreviewTokenRequest(AuthConfig auth, Workspace workspace, WorkspaceEnvironment? activeEnvironment);
}
