using Fubar.Studio.Core.Auth;
using Fubar.Studio.Core.Models;

namespace Fubar.Studio.Core.Tests.Auth;

public class AuthHeaderResolverTests
{
    [Fact]
    public void Bearer_emits_authorization_header()
    {
        var header = Assert.Single(AuthHeaderResolver.HeadersFor(new AuthConfig { Type = AuthType.Bearer, Token = "abc" }));
        Assert.Equal("Authorization", header.Key);
        Assert.Equal("Bearer abc", header.Value);
    }

    [Fact]
    public void OAuth2_references_the_default_token_variable()
    {
        var header = Assert.Single(AuthHeaderResolver.HeadersFor(new AuthConfig { Type = AuthType.OAuth2 }));
        Assert.Equal($"Bearer {{{{{AuthDefaults.AccessTokenVariable}}}}}", header.Value);
    }

    [Fact]
    public void OAuth2_honours_a_custom_token_variable()
    {
        var header = Assert.Single(AuthHeaderResolver.HeadersFor(new AuthConfig { Type = AuthType.OAuth2, AccessTokenVariable = "my_tok" }));
        Assert.Equal("Bearer {{my_tok}}", header.Value);
    }

    [Fact]
    public void Header_api_key_emits_its_own_header()
    {
        var header = Assert.Single(AuthHeaderResolver.HeadersFor(new AuthConfig
        {
            Type = AuthType.ApiKey,
            ApiKeyLocation = ApiKeyLocation.Header,
            ApiKeyName = "X-Api-Key",
            ApiKeyValue = "secret",
        }));
        Assert.Equal("X-Api-Key", header.Key);
        Assert.Equal("secret", header.Value);
    }

    [Theory]
    [InlineData(AuthType.Basic)]
    [InlineData(AuthType.None)]
    public void Schemes_without_a_static_header_emit_nothing(AuthType type)
    {
        Assert.Empty(AuthHeaderResolver.HeadersFor(new AuthConfig { Type = type }));
    }

    [Fact]
    public void Query_located_api_key_emits_nothing()
    {
        Assert.Empty(AuthHeaderResolver.HeadersFor(new AuthConfig
        {
            Type = AuthType.ApiKey,
            ApiKeyLocation = ApiKeyLocation.QueryParam,
            ApiKeyName = "api_key",
            ApiKeyValue = "v",
        }));
    }
}
