using System.Collections.Concurrent;
using Fubar.Studio.Core.Variables;

namespace Fubar.Studio.Infrastructure.Variables;

/// <summary>In-memory <see cref="ISessionVariableStore"/>: a per-workspace map of transient variables
/// (OAuth tokens/expiry, etc.) that live only for the app session and are never written to disk.</summary>
public sealed class SessionVariableStore : ISessionVariableStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _byWorkspace = new();

    public string? Get(string workspaceId, string key) =>
        _byWorkspace.TryGetValue(workspaceId, out var map) && map.TryGetValue(key, out var value) ? value : null;

    public bool TryGet(string workspaceId, string key, out string value)
    {
        if (_byWorkspace.TryGetValue(workspaceId, out var map) && map.TryGetValue(key, out var stored))
        {
            value = stored;
            return true;
        }

        value = "";
        return false;
    }

    public void Set(string workspaceId, string key, string? value)
    {
        var map = _byWorkspace.GetOrAdd(workspaceId, _ => new ConcurrentDictionary<string, string>());
        if (value is null)
        {
            map.TryRemove(key, out _);
        }
        else
        {
            map[key] = value;
        }
    }
}
