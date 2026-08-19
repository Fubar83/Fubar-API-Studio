using Fubar.Studio.Core.Auth;
using Fubar.Studio.Core.History;
using Fubar.Studio.Core.Models;
using Fubar.Studio.Core.Protocols;
using Fubar.Studio.Core.Testing;

namespace Fubar.Studio.Application.Requests;

/// <inheritdoc cref="IRequestExecutionService"/>
public sealed class RequestExecutionService : IRequestExecutionService
{
    private readonly IAuthProvider _authProvider;
    private readonly IExecutorRegistry _executorRegistry;
    private readonly IResponseTestService _testService;
    private readonly IHistoryService _historyService;

    public RequestExecutionService(
        IAuthProvider authProvider,
        IExecutorRegistry executorRegistry,
        IResponseTestService testService,
        IHistoryService historyService)
    {
        _authProvider = authProvider;
        _executorRegistry = executorRegistry;
        _testService = testService;
        _historyService = historyService;
    }

    public async Task<RequestRunResult> RunAsync(RequestRun run, CancellationToken cancellationToken = default)
    {
        // 1. Ensure any dynamic auth (e.g. a fresh OAuth token in session variables) before the request's
        //    Authorization header (which references {{token}}) is resolved and sent.
        AuthOutcome? auth = null;
        if (run.EffectiveAuth is { } effectiveAuth)
        {
            auth = await _authProvider.EnsureAsync(effectiveAuth, run.Workspace, run.Environment, cancellationToken);
        }

        // 2. Execute via whichever protocol executor the request's kind resolves to.
        var context = new RequestExecutionContext(run.Workspace, run.Environment);
        var result = await _executorRegistry.Resolve(run.Request.Kind).ExecuteAsync(run.Request, context, cancellationToken);

        // 3. On a real response (not a transport error), apply captures then evaluate assertions.
        IReadOnlyList<CaptureResult> captures = [];
        IReadOnlyList<AssertionResult> assertions = [];
        if (result.IsSuccess)
        {
            if (run.Request.Captures.Count > 0)
            {
                captures = await _testService.ApplyCapturesAsync(run.Request.Captures, result, run.Workspace, run.Environment, cancellationToken);
            }

            if (run.Request.Assertions.Count > 0)
            {
                assertions = _testService.RunAssertions(run.Request.Assertions, result);
            }
        }

        // 4. Record history (a failed/error send is still useful history). A persistence failure is
        //    surfaced but never fails the run.
        ExecutionSnapshot? snapshot = null;
        string? historyError = null;
        if (run.RecordHistory)
        {
            var candidate = BuildSnapshot(run.Request, result);
            try
            {
                await _historyService.AppendAsync(run.Workspace.RootPath, run.Request.Id, candidate, cancellationToken);
                snapshot = candidate;
            }
            catch (Exception ex)
            {
                historyError = ex.Message;
            }
        }

        return new RequestRunResult(result, auth, assertions, captures, snapshot, historyError);
    }

    private static ExecutionSnapshot BuildSnapshot(RequestModel request, ExecutionResult result) => new()
    {
        Method = request.Method,
        Url = request.Url,
        Headers = request.Headers.Where(h => h.Enabled).ToList(),
        Body = request.Body.Raw,
        StatusCode = result.StatusCode,
        ReasonPhrase = result.ReasonPhrase,
        ElapsedMilliseconds = result.ElapsedMilliseconds,
        SizeBytes = result.SizeBytes,
        ErrorMessage = result.ErrorMessage,
    };
}
