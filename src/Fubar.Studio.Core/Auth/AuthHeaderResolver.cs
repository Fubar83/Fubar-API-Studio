using Fubar.Studio.Core.Models;

namespace Fubar.Studio.Core.Auth;

/// <summary>
/// Synthesizes the HTTP header(s) an <see cref="AuthConfig"/> implies - the domain rule for how a scheme
/// applies to an outgoing request. Bearer and OAuth2 both send <c>Authorization: Bearer &lt;token&gt;</c>
/// (OAuth2's token is a session variable the auth provider populates/refreshes before send); a
/// header-located API key sends its own header. Basic and a query-located API key have no single static
/// header form and are applied elsewhere.
/// </summary>
public static class AuthHeaderResolver
{
    public static List<KeyValueItem> HeadersFor(AuthConfig config) => config.Type switch
    {
        AuthType.Bearer => [new KeyValueItem { Key = "Authorization", Value = $"Bearer {config.Token}" }],

        AuthType.OAuth2 =>
            [new KeyValueItem { Key = "Authorization", Value = $"Bearer {{{{{OAuthTokenVariable(config)}}}}}" }],

        AuthType.ApiKey when config.ApiKeyLocation == ApiKeyLocation.Header && !string.IsNullOrWhiteSpace(config.ApiKeyName) =>
            [new KeyValueItem { Key = config.ApiKeyName!, Value = config.ApiKeyValue ?? "" }],

        _ => [],
    };

    private static string OAuthTokenVariable(AuthConfig config) =>
        string.IsNullOrWhiteSpace(config.AccessTokenVariable) ? AuthDefaults.AccessTokenVariable : config.AccessTokenVariable!;
}
