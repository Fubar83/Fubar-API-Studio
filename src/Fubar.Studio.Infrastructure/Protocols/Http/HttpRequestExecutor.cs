using System.Diagnostics;
using System.Text;
using Fubar.Studio.Core.Models;
using Fubar.Studio.Core.Protocols;
using Fubar.Studio.Core.Variables;

namespace Fubar.Studio.Infrastructure.Protocols.Http;

/// <summary>
/// The first (and for now, only) registered <see cref="IRequestExecutor"/>: builds an
/// <see cref="HttpRequestMessage"/> from a <see cref="RequestModel"/>, sends it via
/// <see cref="IHttpClientFactory"/>, and measures timing/size. <c>{{key}}</c> tokens in the URL,
/// headers, and body are substituted via <see cref="IVariableResolver"/> against the caller's
/// <see cref="RequestExecutionContext"/> (active workspace + environment) - see
/// RequestEditorPane.md §1.3, "Environment-Only Variables".
/// </summary>
public sealed class HttpRequestExecutor : IRequestExecutor
{
    /// <summary>Named <see cref="IHttpClientFactory"/> client configured (in DI) with a shared
    /// <see cref="System.Net.CookieContainer"/>, so <c>Set-Cookie</c> from one request is sent on the
    /// next - the session cookie jar an API client needs for login-then-call flows.</summary>
    public const string HttpClientName = "fubar";

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(100);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IVariableResolver _variableResolver;

    public HttpRequestExecutor(IHttpClientFactory httpClientFactory, IVariableResolver variableResolver)
    {
        _httpClientFactory = httpClientFactory;
        _variableResolver = variableResolver;
    }

    public RequestKind Kind => RequestKind.Http;

    public async Task<ExecutionResult> ExecuteAsync(RequestModel request, RequestExecutionContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var timeout = request.TimeoutSeconds is int s and > 0 ? TimeSpan.FromSeconds(s) : DefaultTimeout;
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var url = BuildUrl(request, context);
            using var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method), url);

            foreach (var header in request.Headers.Where(h => h.Enabled && !string.IsNullOrWhiteSpace(h.Key)))
            {
                httpRequest.Headers.TryAddWithoutValidation(header.Key, Resolve(header.Value, context));
            }

            httpRequest.Content = await BuildContentAsync(request.Body, context, linked.Token);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(httpRequest, linked.Token);
            var bodyBytes = await response.Content.ReadAsByteArrayAsync(linked.Token);
            var body = Encoding.UTF8.GetString(bodyBytes);

            var headers = response.Headers
                .Concat(response.Content.Headers)
                .Select(h => new KeyValueItem { Key = h.Key, Value = string.Join(", ", h.Value) })
                .ToList();

            return new ExecutionResult
            {
                StatusCode = (int)response.StatusCode,
                ReasonPhrase = response.ReasonPhrase,
                Headers = headers,
                Body = body,
                BodyBytes = bodyBytes,
                ContentType = response.Content.Headers.ContentType?.MediaType,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                SizeBytes = bodyBytes.Length,
            };
        }
        // A timeout fires the linked token via timeoutCts while the caller's own token stays unset.
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult
            {
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ErrorMessage = $"Request timed out after {timeout.TotalSeconds:0.#} s.",
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult
            {
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ErrorMessage = "Request cancelled.",
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message,
            };
        }
    }

    private string BuildUrl(RequestModel request, RequestExecutionContext context)
    {
        var url = Resolve(request.Url, context);

        var enabledParams = request.QueryParams.Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Key)).ToList();
        if (enabledParams.Count == 0)
        {
            return url;
        }

        var separator = url.Contains('?') ? "&" : "?";
        var query = string.Join('&', enabledParams.Select(p =>
            $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(Resolve(p.Value, context))}"));
        return $"{url}{separator}{query}";
    }

    private string Resolve(string? input, RequestExecutionContext context) =>
        _variableResolver.Substitute(input, context.Workspace, context.ActiveEnvironment);

    /// <summary>
    /// Builds the outgoing <see cref="HttpContent"/> for every <see cref="BodyType"/> the Body tab
    /// offers - previously only Json/RawText were handled here, so picking FormData/UrlEncoded/
    /// BinaryFile in the UI silently sent no body at all.
    /// </summary>
    private async Task<HttpContent?> BuildContentAsync(RequestBody body, RequestExecutionContext context, CancellationToken cancellationToken)
    {
        switch (body.Type)
        {
            case BodyType.Json or BodyType.RawText when !string.IsNullOrEmpty(body.Raw):
                var contentType = body.Type == BodyType.Json ? "application/json" : "text/plain";
                return new StringContent(Resolve(body.Raw, context), Encoding.UTF8, contentType);

            case BodyType.FormData:
                var multipart = new MultipartFormDataContent();
                foreach (var field in body.FormData.Where(f => f.Enabled && !string.IsNullOrWhiteSpace(f.Key)))
                {
                    multipart.Add(new StringContent(Resolve(field.Value, context)), field.Key);
                }
                return multipart;

            case BodyType.UrlEncoded:
                var pairs = body.UrlEncoded
                    .Where(f => f.Enabled && !string.IsNullOrWhiteSpace(f.Key))
                    .Select(f => new KeyValuePair<string, string>(f.Key, Resolve(f.Value, context)));
                return new FormUrlEncodedContent(pairs);

            case BodyType.BinaryFile when !string.IsNullOrWhiteSpace(body.BinaryFilePath):
                var bytes = await File.ReadAllBytesAsync(body.BinaryFilePath, cancellationToken);
                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                return fileContent;

            default:
                return null;
        }
    }
}
