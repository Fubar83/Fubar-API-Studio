using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Fubar.Studio.Core.Auth;
using Fubar.Studio.Core.Models;
using Fubar.Studio.Core.Secrets;
using Fubar.Studio.Infrastructure.Auth;
using Fubar.Studio.Infrastructure.Variables;

namespace Fubar.Studio.Infrastructure.Tests.Auth;

public class OAuthTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string ResponseBody { get; set; } = "{}";
        public string? LastBody { get; private set; }
        public AuthenticationHeaderValueSnapshot? LastAuthHeader { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            if (request.Headers.Authorization is { } auth)
            {
                LastAuthHeader = new AuthenticationHeaderValueSnapshot(auth.Scheme, auth.Parameter);
            }

            return new HttpResponseMessage(Status) { Content = new StringContent(ResponseBody) };
        }
    }

    public sealed record AuthenticationHeaderValueSnapshot(string Scheme, string? Parameter);

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler);
    }

    private sealed class NoSecrets : ISecretStoreService
    {
        public string? TryGetSecret(string workspaceId, string key) => null;
        public void SetSecret(string workspaceId, string key, string value) { }
        public void DeleteSecret(string workspaceId, string key) { }
    }

    private static Workspace Workspace => new() { RootPath = "C:/fake", Manifest = new AppManifest { Id = "ws1", Name = "T" } };

    [Fact]
    public async Task TokenService_ClientCredentials_SendsGrantAndParsesResponse()
    {
        var handler = new StubHandler { ResponseBody = """{ "access_token": "abc", "expires_in": 3600, "refresh_token": "r1" }""" };
        var service = new OAuthTokenService(new StubHttpClientFactory(handler));

        var result = await service.AcquireAsync(new OAuth2TokenRequest(
            OAuth2GrantType.ClientCredentials, "https://auth/token", "cid", "csec", "read write", null, OAuth2ClientAuth.Body));

        Assert.Equal("abc", result.AccessToken);
        Assert.Equal("r1", result.RefreshToken);
        Assert.NotNull(result.ExpiresAt);
        Assert.Contains("grant_type=client_credentials", handler.LastBody);
        Assert.Contains("client_id=cid", handler.LastBody);
        Assert.Contains("scope=read", handler.LastBody);
    }

    [Fact]
    public async Task TokenService_BasicHeaderAuth_SendsAuthorizationHeader_NotBodyCredentials()
    {
        var handler = new StubHandler { ResponseBody = """{ "access_token": "abc" }""" };
        var service = new OAuthTokenService(new StubHttpClientFactory(handler));

        await service.AcquireAsync(new OAuth2TokenRequest(
            OAuth2GrantType.ClientCredentials, "https://auth/token", "cid", "csec", null, null, OAuth2ClientAuth.BasicHeader));

        Assert.Equal("Basic", handler.LastAuthHeader?.Scheme);
        Assert.DoesNotContain("client_id", handler.LastBody);
    }

    [Fact]
    public async Task TokenService_Refresh_SendsRefreshGrant()
    {
        var handler = new StubHandler { ResponseBody = """{ "access_token": "new" }""" };
        var service = new OAuthTokenService(new StubHttpClientFactory(handler));

        await service.AcquireAsync(new OAuth2TokenRequest(
            OAuth2GrantType.RefreshToken, "https://auth/token", "cid", null, null, "the-refresh-token", OAuth2ClientAuth.Body));

        Assert.Contains("grant_type=refresh_token", handler.LastBody);
        Assert.Contains("refresh_token=the-refresh-token", handler.LastBody);
    }

    [Fact]
    public async Task TokenService_NonSuccess_Throws()
    {
        var handler = new StubHandler { Status = HttpStatusCode.BadRequest, ResponseBody = """{ "error": "invalid_client" }""" };
        var service = new OAuthTokenService(new StubHttpClientFactory(handler));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcquireAsync(
            new OAuth2TokenRequest(OAuth2GrantType.ClientCredentials, "https://auth/token", "cid", "bad", null, null, OAuth2ClientAuth.Body)));
    }

    private sealed class CountingTokenService : IOAuthTokenService
    {
        public int Calls { get; private set; }
        public OAuthTokenResult Result { get; set; } = new("token", DateTimeOffset.UtcNow.AddHours(1), null);

        public Task<OAuthTokenResult> AcquireAsync(OAuth2TokenRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Result);
        }

        public string Describe(OAuth2TokenRequest request) => $"POST {request.TokenUrl}";
    }

    private static AuthProvider MakeProvider(IOAuthTokenService tokenService, SessionVariableStore session) =>
        new(tokenService, new VariableResolver(new NoSecrets(), session), session);

    [Fact]
    public async Task Provider_AcquiresAndStoresTokenInSessionVariables()
    {
        var session = new SessionVariableStore();
        var provider = MakeProvider(new CountingTokenService(), session);
        var auth = new AuthConfig { Type = AuthType.OAuth2, TokenUrl = "https://auth/token", ClientId = "c", ClientSecret = "s" };

        var outcome = await provider.EnsureAsync(auth, Workspace, null);

        Assert.True(outcome.Ok);
        Assert.Equal("token", session.Get("ws1", AuthDefaults.AccessTokenVariable));
        Assert.False(string.IsNullOrEmpty(session.Get("ws1", AuthDefaults.ExpiryVariable)));
    }

    [Fact]
    public async Task Provider_ReusesCachedToken_WhenNotExpired()
    {
        var session = new SessionVariableStore();
        var token = new CountingTokenService();
        var provider = MakeProvider(token, session);
        var auth = new AuthConfig { Type = AuthType.OAuth2, TokenUrl = "https://auth/token", ClientId = "c" };

        await provider.EnsureAsync(auth, Workspace, null);
        await provider.EnsureAsync(auth, Workspace, null);

        Assert.Equal(1, token.Calls); // second call reused the cached, unexpired token
    }

    [Fact]
    public async Task Provider_Reacquires_WhenCachedTokenExpired()
    {
        var session = new SessionVariableStore();
        var token = new CountingTokenService { Result = new("token", DateTimeOffset.UtcNow.AddSeconds(-10), null) };
        var provider = MakeProvider(token, session);
        var auth = new AuthConfig { Type = AuthType.OAuth2, TokenUrl = "https://auth/token", ClientId = "c" };

        await provider.EnsureAsync(auth, Workspace, null); // stores an already-expired token
        await provider.EnsureAsync(auth, Workspace, null); // must re-acquire

        Assert.Equal(2, token.Calls);
    }

    [Fact]
    public void TokenService_Describe_ShowsRequest_MasksSecrets()
    {
        var service = new OAuthTokenService(new StubHttpClientFactory(new StubHandler()));
        var preview = service.Describe(new OAuth2TokenRequest(
            OAuth2GrantType.ClientCredentials, "https://auth/token", "my-client", "s3cret", "read", null, OAuth2ClientAuth.Body));

        Assert.Contains("POST https://auth/token", preview);
        Assert.Contains("grant_type=client_credentials", preview);
        Assert.Contains("client_id=my-client", preview); // non-secret shown
        Assert.Contains("scope=read", preview);
        Assert.DoesNotContain("s3cret", preview);         // secret masked
    }

    [Fact]
    public void Provider_PreviewTokenRequest_ResolvesVariables_AndNeedsTokenUrl()
    {
        var session = new SessionVariableStore();
        var provider = MakeProvider(new CountingTokenService(), session);

        var missingUrl = provider.PreviewTokenRequest(new AuthConfig { Type = AuthType.OAuth2 }, Workspace, null);
        Assert.Contains("token URL", missingUrl);

        var ok = provider.PreviewTokenRequest(
            new AuthConfig { Type = AuthType.OAuth2, TokenUrl = "https://auth/token", ClientId = "c" }, Workspace, null);
        Assert.Contains("POST https://auth/token", ok);
        Assert.Contains("Authorization: Bearer {{oauth2_access_token}}", ok);
    }

    [Fact]
    public async Task Provider_NoOp_ForNonOAuth2()
    {
        var token = new CountingTokenService();
        var provider = MakeProvider(token, new SessionVariableStore());

        var outcome = await provider.EnsureAsync(new AuthConfig { Type = AuthType.Bearer }, Workspace, null);

        Assert.True(outcome.Ok);
        Assert.Equal(0, token.Calls);
    }
}
