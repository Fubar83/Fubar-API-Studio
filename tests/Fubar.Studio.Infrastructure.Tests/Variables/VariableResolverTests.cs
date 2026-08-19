using Fubar.Studio.Core.Models;
using Fubar.Studio.Core.Secrets;
using Fubar.Studio.Infrastructure.Variables;

namespace Fubar.Studio.Infrastructure.Tests.Variables;

public class VariableResolverTests
{
    private sealed class FakeSecretStore : ISecretStoreService
    {
        private readonly Dictionary<string, string> _secrets = [];

        public void Seed(string workspaceId, string key, string value) => _secrets[$"{workspaceId}:{key}"] = value;

        public string? TryGetSecret(string workspaceId, string key) =>
            _secrets.TryGetValue($"{workspaceId}:{key}", out var value) ? value : null;

        public void SetSecret(string workspaceId, string key, string value) => Seed(workspaceId, key, value);

        public void DeleteSecret(string workspaceId, string key) => _secrets.Remove($"{workspaceId}:{key}");
    }

    private static Workspace MakeWorkspace(string id = "ws1") =>
        new() { RootPath = "C:/fake", Manifest = new AppManifest { Id = id, Name = "Test" } };

    [Fact]
    public void Resolve_ReturnsUndefined_WhenNoActiveEnvironment()
    {
        var resolver = new VariableResolver(new FakeSecretStore(), new SessionVariableStore());

        var result = resolver.Resolve("baseUrl", MakeWorkspace(), activeEnvironment: null);

        Assert.False(result.IsDefined);
    }

    [Fact]
    public void Resolve_ReturnsUndefined_WhenKeyNotInEnvironment()
    {
        var resolver = new VariableResolver(new FakeSecretStore(), new SessionVariableStore());
        var env = new WorkspaceEnvironment { Name = "Staging", Variables = [] };

        var result = resolver.Resolve("missing", MakeWorkspace(), env);

        Assert.False(result.IsDefined);
    }

    [Fact]
    public void Resolve_FallsBackToSessionStore_WhenNotInEnvironment()
    {
        var session = new SessionVariableStore();
        session.Set("ws1", "oauth2_access_token", "tok-123");
        var resolver = new VariableResolver(new FakeSecretStore(), session);

        var result = resolver.Resolve("oauth2_access_token", MakeWorkspace("ws1"), activeEnvironment: null);

        Assert.True(result.IsDefined);
        Assert.Equal("tok-123", result.Value);
    }

    [Fact]
    public void Resolve_ReturnsValueAndSourceName_ForPlainVariable()
    {
        var resolver = new VariableResolver(new FakeSecretStore(), new SessionVariableStore());
        var env = new WorkspaceEnvironment
        {
            Name = "Staging",
            Variables = [new AppVariable { Key = "baseUrl", Value = "https://staging.example.com" }],
        };

        var result = resolver.Resolve("baseUrl", MakeWorkspace(), env);

        Assert.True(result.IsDefined);
        Assert.Equal("https://staging.example.com", result.Value);
        Assert.Equal("Staging", result.SourceName);
    }

    [Fact]
    public void Resolve_PullsFromSecretStore_ForSecretVariable()
    {
        var secretStore = new FakeSecretStore();
        secretStore.Seed("ws1", "apiKey", "super-secret");
        var resolver = new VariableResolver(secretStore, new SessionVariableStore());
        var env = new WorkspaceEnvironment { Name = "Staging", Variables = [new AppVariable { Key = "apiKey", IsSecret = true }] };

        var result = resolver.Resolve("apiKey", MakeWorkspace("ws1"), env);

        Assert.True(result.IsDefined);
        Assert.Equal("super-secret", result.Value);
    }

    [Fact]
    public void Resolve_Undefined_WhenSecretNotSetInStore()
    {
        var resolver = new VariableResolver(new FakeSecretStore(), new SessionVariableStore());
        var env = new WorkspaceEnvironment { Name = "Staging", Variables = [new AppVariable { Key = "apiKey", IsSecret = true }] };

        var result = resolver.Resolve("apiKey", MakeWorkspace("ws1"), env);

        Assert.False(result.IsDefined);
    }

    [Fact]
    public void Substitute_ReplacesDefinedTokens_LeavesUndefinedTokensAsIs()
    {
        var resolver = new VariableResolver(new FakeSecretStore(), new SessionVariableStore());
        var env = new WorkspaceEnvironment
        {
            Name = "Staging",
            Variables = [new AppVariable { Key = "host", Value = "api.example.com" }],
        };

        var result = resolver.Substitute("https://{{host}}/v1/{{entity}}", MakeWorkspace(), env);

        Assert.Equal("https://api.example.com/v1/{{entity}}", result);
    }

    [Fact]
    public void Substitute_ReturnsInputUnchanged_WhenNoTokens()
    {
        var resolver = new VariableResolver(new FakeSecretStore(), new SessionVariableStore());

        var result = resolver.Substitute("https://api.example.com/v1/users", MakeWorkspace(), activeEnvironment: null);

        Assert.Equal("https://api.example.com/v1/users", result);
    }
}
