using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fubar.Studio.Core.Auth;
using Fubar.Studio.Core.Models;

namespace Fubar.Studio.UI.ViewModels;

/// <summary>
/// Backs the Auth tab (RequestEditorPane.md §5): inheritance selector (Inherit from parent folder /
/// None / Bearer / API Key / Basic / OAuth 2.0 / a named workspace <see cref="AuthProfile"/>) plus
/// the fields for whichever inline scheme is selected. <see cref="AvailableProfiles"/> is populated
/// asynchronously by the owning <c>RequestEditorViewModel</c> once it can reach
/// <c>IWorkspaceService.LoadAuthProfilesAsync</c>. Applying any of this to an outgoing request
/// remains a separate, not-yet-implemented <c>IRequestExecutor</c> concern - this view model is
/// pure editable state.
/// </summary>
public partial class RequestAuthViewModel : ViewModelBase
{
    public static IReadOnlyList<AuthType> TypeOptions { get; } = Enum.GetValues<AuthType>();

    public static IReadOnlyList<ApiKeyLocation> ApiKeyLocationOptions { get; } = Enum.GetValues<ApiKeyLocation>();

    public static IReadOnlyList<OAuth2GrantType> OAuth2GrantOptions { get; } = Enum.GetValues<OAuth2GrantType>();

    public static IReadOnlyList<OAuth2ClientAuth> OAuth2ClientAuthOptions { get; } = Enum.GetValues<OAuth2ClientAuth>();

    [ObservableProperty]
    public partial AuthType Type { get; set; } = AuthType.Inherit;

    public bool IsBearerVisible => Type == AuthType.Bearer;

    public bool IsApiKeyVisible => Type == AuthType.ApiKey;

    public bool IsBasicVisible => Type == AuthType.Basic;

    public bool IsOAuth2Visible => Type == AuthType.OAuth2;

    public bool IsProfileVisible => Type == AuthType.Profile;

    partial void OnTypeChanged(AuthType value)
    {
        OnPropertyChanged(nameof(IsBearerVisible));
        OnPropertyChanged(nameof(IsApiKeyVisible));
        OnPropertyChanged(nameof(IsBasicVisible));
        OnPropertyChanged(nameof(IsOAuth2Visible));
        OnPropertyChanged(nameof(IsProfileVisible));
    }

    [ObservableProperty]
    public partial string Token { get; set; } = "";

    [ObservableProperty]
    public partial string ApiKeyName { get; set; } = "";

    [ObservableProperty]
    public partial string ApiKeyValue { get; set; } = "";

    [ObservableProperty]
    public partial ApiKeyLocation ApiKeyLocation { get; set; } = ApiKeyLocation.Header;

    [ObservableProperty]
    public partial string Username { get; set; } = "";

    [ObservableProperty]
    public partial string Password { get; set; } = "";

    // --- OAuth2 (client credentials / refresh token). Any field may be a literal or {{variable}}. ------

    [ObservableProperty]
    public partial OAuth2GrantType OAuth2Grant { get; set; } = OAuth2GrantType.ClientCredentials;

    public bool IsRefreshGrant => OAuth2Grant == OAuth2GrantType.RefreshToken;

    partial void OnOAuth2GrantChanged(OAuth2GrantType value) => OnPropertyChanged(nameof(IsRefreshGrant));

    [ObservableProperty]
    public partial string TokenUrl { get; set; } = "";

    [ObservableProperty]
    public partial string ClientId { get; set; } = "";

    [ObservableProperty]
    public partial string ClientSecret { get; set; } = "";

    [ObservableProperty]
    public partial string Scopes { get; set; } = "";

    [ObservableProperty]
    public partial string RefreshToken { get; set; } = "";

    [ObservableProperty]
    public partial OAuth2ClientAuth ClientAuthentication { get; set; } = OAuth2ClientAuth.Body;

    [ObservableProperty]
    public partial string AccessTokenVariable { get; set; } = "";

    [ObservableProperty]
    public partial string ExpiryVariable { get; set; } = "";

    /// <summary>Set by the owning <c>RequestEditorViewModel</c> so the Test button can acquire a token via
    /// the <c>IAuthProvider</c> against the current workspace/environment.</summary>
    public Func<AuthConfig, Task<AuthOutcome>>? TestAuthHandler { get; set; }

    /// <summary>Set by the owning <c>RequestEditorViewModel</c> so "Verify request" can preview the token
    /// request (resolved, secrets masked) without sending it.</summary>
    public Func<AuthConfig, string>? PreviewHandler { get; set; }

    [ObservableProperty]
    public partial string? TestStatus { get; set; }

    [ObservableProperty]
    public partial string? RequestPreview { get; set; }

    [RelayCommand]
    private async Task TestAuthAsync()
    {
        if (TestAuthHandler is null)
        {
            return;
        }

        TestStatus = "Requesting token...";
        var outcome = await TestAuthHandler(ToModel());
        TestStatus = outcome.Message;
    }

    [RelayCommand]
    private void VerifyRequest() => RequestPreview = PreviewHandler?.Invoke(ToModel());

    /// <summary>Workspace-level reusable auth profiles, offered when <see cref="Type"/> is <see cref="AuthType.Profile"/>.</summary>
    public ObservableCollection<AuthProfile> AvailableProfiles { get; } = [];

    [ObservableProperty]
    public partial AuthProfile? SelectedProfile { get; set; }

    public AuthConfig ToModel() => new()
    {
        Type = Type,
        Token = string.IsNullOrEmpty(Token) ? null : Token,
        ApiKeyName = string.IsNullOrEmpty(ApiKeyName) ? null : ApiKeyName,
        ApiKeyValue = string.IsNullOrEmpty(ApiKeyValue) ? null : ApiKeyValue,
        ApiKeyLocation = ApiKeyLocation,
        Username = string.IsNullOrEmpty(Username) ? null : Username,
        Password = string.IsNullOrEmpty(Password) ? null : Password,
        OAuth2Grant = OAuth2Grant,
        TokenUrl = string.IsNullOrEmpty(TokenUrl) ? null : TokenUrl,
        ClientId = string.IsNullOrEmpty(ClientId) ? null : ClientId,
        ClientSecret = string.IsNullOrEmpty(ClientSecret) ? null : ClientSecret,
        Scopes = string.IsNullOrEmpty(Scopes) ? null : Scopes,
        RefreshToken = string.IsNullOrEmpty(RefreshToken) ? null : RefreshToken,
        ClientAuthentication = ClientAuthentication,
        AccessTokenVariable = string.IsNullOrEmpty(AccessTokenVariable) ? null : AccessTokenVariable,
        ExpiryVariable = string.IsNullOrEmpty(ExpiryVariable) ? null : ExpiryVariable,
    };

    public static RequestAuthViewModel FromModel(AuthConfig auth) => new()
    {
        Type = auth.Type,
        Token = auth.Token ?? "",
        ApiKeyName = auth.ApiKeyName ?? "",
        ApiKeyValue = auth.ApiKeyValue ?? "",
        ApiKeyLocation = auth.ApiKeyLocation,
        Username = auth.Username ?? "",
        Password = auth.Password ?? "",
        OAuth2Grant = auth.OAuth2Grant,
        TokenUrl = auth.TokenUrl ?? "",
        ClientId = auth.ClientId ?? "",
        ClientSecret = auth.ClientSecret ?? "",
        Scopes = auth.Scopes ?? "",
        RefreshToken = auth.RefreshToken ?? "",
        ClientAuthentication = auth.ClientAuthentication,
        AccessTokenVariable = auth.AccessTokenVariable ?? "",
        ExpiryVariable = auth.ExpiryVariable ?? "",
    };
}
