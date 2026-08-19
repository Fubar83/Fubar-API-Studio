using Fubar.Studio.Core.Models;

namespace Fubar.Studio.Core.Protocols;

/// <summary>
/// What an <see cref="IRequestExecutor"/> needs to resolve <c>{{variable}}</c> tokens while
/// sending a request: which workspace it belongs to (for secret lookups, keyed by
/// <see cref="Models.Workspace.WorkspaceId"/>) and which environment is currently active.
/// </summary>
public sealed record RequestExecutionContext(Workspace Workspace, WorkspaceEnvironment? ActiveEnvironment);
