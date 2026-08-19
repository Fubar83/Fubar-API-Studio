using Fubar.Studio.Core.Auth;
using Fubar.Studio.Core.History;
using Fubar.Studio.Core.Import;
using Fubar.Studio.Core.Json;
using Fubar.Studio.Core.Protocols;
using Fubar.Studio.Core.Secrets;
using Fubar.Studio.Core.Settings;
using Fubar.Studio.Core.Testing;
using Fubar.Studio.Core.Variables;
using Fubar.Studio.Core.Workspaces;
using Fubar.Studio.Infrastructure.Auth;
using Fubar.Studio.Infrastructure.History;
using Fubar.Studio.Infrastructure.Import;
using Fubar.Studio.Infrastructure.Json;
using Fubar.Studio.Infrastructure.Protocols;
using Fubar.Studio.Infrastructure.Protocols.Http;
using Fubar.Studio.Infrastructure.Secrets;
using Fubar.Studio.Infrastructure.Settings;
using Fubar.Studio.Infrastructure.Testing;
using Fubar.Studio.Infrastructure.Variables;
using Fubar.Studio.Infrastructure.Workspaces;
using Microsoft.Extensions.DependencyInjection;

namespace Fubar.Studio.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFubarInfrastructure(this IServiceCollection services)
    {
        // One WorkspaceService instance, surfaced under the aggregate and each focused role interface so
        // consumers can depend on the narrowest one they need (ISP) while sharing state/IO.
        services.AddSingleton<WorkspaceService>();
        services.AddSingleton<IWorkspaceService>(sp => sp.GetRequiredService<WorkspaceService>());
        services.AddSingleton<IWorkspaceStore>(sp => sp.GetRequiredService<WorkspaceService>());
        services.AddSingleton<IRequestStore>(sp => sp.GetRequiredService<WorkspaceService>());
        services.AddSingleton<IEnvironmentStore>(sp => sp.GetRequiredService<WorkspaceService>());
        services.AddSingleton<IAuthProfileStore>(sp => sp.GetRequiredService<WorkspaceService>());
        services.AddSingleton<IFolderConfigStore>(sp => sp.GetRequiredService<WorkspaceService>());
        services.AddSingleton<IInheritanceResolver>(sp => sp.GetRequiredService<WorkspaceService>());
        services.AddSingleton<IOpenApiImportService, OpenApiImportService>();
        services.AddSingleton<ICurlImportService, CurlImporter>();
        services.AddSingleton<ICurlExportService, CurlExporter>();
        services.AddSingleton<IPostmanImportService, PostmanImporter>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<ISecretStoreService, KeySharpSecretStoreService>();
        services.AddSingleton<ISessionVariableStore, SessionVariableStore>();
        services.AddSingleton<IVariableResolver, VariableResolver>();
        services.AddSingleton<IOAuthTokenService, OAuthTokenService>();
        services.AddSingleton<IAuthProvider, AuthProvider>();
        services.AddSingleton<IHistoryService, HistoryService>();
        services.AddSingleton<IResponseTestService, ResponseTestService>();
        services.AddSingleton<IJsonSchemaValidator, JsonSchemaValidator>();
        services.AddSingleton<IJsonPathEvaluator, JsonPathEvaluator>();

        // A process-wide cookie jar shared by the named HTTP client, so Set-Cookie from one request is
        // replayed on the next (session cookies for login-then-call flows).
        services.AddSingleton<System.Net.CookieContainer>();
        services.AddHttpClient(); // default client (used by e.g. the OAuth token service)
        services.AddHttpClient(HttpRequestExecutor.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(sp => new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = sp.GetRequiredService<System.Net.CookieContainer>(),
                AllowAutoRedirect = true,
            });

        services.AddSingleton<IProtocolProvider, HttpProtocolProvider>();
        services.AddSingleton<IProtocolRegistry, ProtocolRegistry>();

        services.AddSingleton<IRequestExecutor, HttpRequestExecutor>();
        services.AddSingleton<IExecutorRegistry, ExecutorRegistry>();

        return services;
    }
}
