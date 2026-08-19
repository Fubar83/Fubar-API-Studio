namespace Fubar.Studio.Core.Models;

/// <summary>
/// A single entry in <c>fubar.json</c>'s <c>variables</c> array. When <see cref="IsSecret"/> is
/// true, <see cref="Value"/> is always null/empty on disk - the real value lives only in the
/// host OS keyring, keyed <c>fubar:[WorkspaceId]:[Key]</c>, and is resolved at request-execution
/// time via <c>IVariableResolverService</c> / <c>ISecretStoreService</c>.
/// </summary>
public sealed class AppVariable
{
    public required string Key { get; set; }

    public string? Value { get; set; }

    public bool IsSecret { get; set; }

    public string? Description { get; set; }
}
