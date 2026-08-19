namespace Fubar.Studio.Core.Variables;

/// <summary>
/// An in-memory, per-workspace store for variables that must NOT be persisted to disk - OAuth access
/// tokens, their expiry, refresh tokens, and any other transient runtime values. Resolved by
/// <c>IVariableResolver</c> as a fallback under the active environment, so <c>{{token}}</c> works
/// everywhere while never being written to an environment file. Cleared when the app exits.
/// </summary>
public interface ISessionVariableStore
{
    string? Get(string workspaceId, string key);

    void Set(string workspaceId, string key, string? value);

    bool TryGet(string workspaceId, string key, out string value);
}
