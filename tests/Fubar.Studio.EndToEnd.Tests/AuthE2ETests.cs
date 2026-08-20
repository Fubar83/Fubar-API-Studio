using Fubar.Studio.Application.Requests;
using Fubar.Studio.Core.Auth;
using Fubar.Studio.Core.Models;

namespace Fubar.Studio.EndToEnd.Tests;

/// <summary>Live end-to-end: each auth mode is applied to a real request against httpbin.org, which echoes
/// back what it received - proving the credential actually reached the wire. Examples of every supported
/// no-browser scheme. Opt-in via FUBAR_E2E=1 (see <see cref="HttpBin"/>).</summary>
public class AuthE2ETests
{
    private static async Task<(int Status, string Body)> Send(AuthConfig? auth, RequestModel request)
    {
        var (exec, ws) = HttpBin.Pipeline();
        var run = await exec.RunAsync(new RequestRun(request, ws, Environment: null, EffectiveAuth: auth, RecordHistory: false));
        return (run.Result.StatusCode, run.Result.Body);
    }

    [Fact]
    public async Task Bearer_token_reaches_the_server()
    {
        HttpBin.RequireLive();

        var (status, body) = await Send(
            new AuthConfig { Type = AuthType.Bearer, Token = "abc123" },
            HttpBin.Get($"{HttpBin.BaseUrl}/bearer"));

        Assert.Equal(200, status);          // /bearer returns 401 if no bearer was sent
        Assert.Contains("abc123", body);    // and echoes the token back
    }

    [Fact]
    public async Task Basic_auth_reaches_the_server()
    {
        HttpBin.RequireLive();

        var (status, _) = await Send(
            new AuthConfig { Type = AuthType.Basic, Username = "user", Password = "passwd" },
            HttpBin.Get($"{HttpBin.BaseUrl}/basic-auth/user/passwd"));

        Assert.Equal(200, status);          // 401 unless the base64 Basic header was built + sent
    }

    [Fact]
    public async Task ApiKey_in_a_header_reaches_the_server()
    {
        HttpBin.RequireLive();

        var (status, body) = await Send(
            new AuthConfig { Type = AuthType.ApiKey, ApiKeyName = "X-Api-Key", ApiKeyValue = "s3cret", ApiKeyLocation = ApiKeyLocation.Header },
            HttpBin.Get($"{HttpBin.BaseUrl}/headers"));

        Assert.Equal(200, status);
        Assert.Contains("s3cret", body);    // echoed under "headers"
    }

    [Fact]
    public async Task ApiKey_in_the_query_reaches_the_server()
    {
        HttpBin.RequireLive();

        var (status, body) = await Send(
            new AuthConfig { Type = AuthType.ApiKey, ApiKeyName = "api_key", ApiKeyValue = "s3cret", ApiKeyLocation = ApiKeyLocation.QueryParam },
            HttpBin.Get($"{HttpBin.BaseUrl}/get"));

        Assert.Equal(200, status);
        Assert.Contains("s3cret", body);    // echoed under "args"
    }

    [Fact]
    public async Task OAuth2_template_acquires_a_token_then_sends_it()
    {
        HttpBin.RequireLive();

        var auth = new AuthConfig
        {
            Type = AuthType.OAuth2,
            // The "token endpoint": /response-headers echoes its query params in the JSON body, so we can
            // capture a token from it without a real identity provider.
            TokenRequest = new AuthTokenRequest { Method = "GET", Url = $"{HttpBin.BaseUrl}/response-headers?access_token=tok-xyz" },
            TokenCaptures = [new CaptureRule { VariableName = AuthDefaults.AccessTokenVariable, Expression = "$.access_token" }],
        };

        var (status, body) = await Send(auth, HttpBin.Get($"{HttpBin.BaseUrl}/bearer"));

        Assert.Equal(200, status);
        Assert.Contains("tok-xyz", body);   // token was captured from the token request, then sent as Bearer
    }
}
