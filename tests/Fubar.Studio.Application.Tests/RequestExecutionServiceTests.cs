using Fubar.Studio.Application.Requests;
using Fubar.Studio.Core.Auth;
using Fubar.Studio.Core.History;
using Fubar.Studio.Core.Models;
using Fubar.Studio.Core.Protocols;
using Fubar.Studio.Core.Testing;

namespace Fubar.Studio.Application.Tests;

public class RequestExecutionServiceTests
{
    private static readonly Workspace Ws = new() { RootPath = "root", Manifest = new AppManifest { Name = "t" } };

    private static RequestModel RequestWithTests() => new()
    {
        Name = "r",
        Assertions = [new Assertion { Source = ResponseField.StatusCode, Operator = AssertionOperator.Equals, Expected = "200" }],
        Captures = [new CaptureRule { VariableName = "id", Source = ResponseField.JsonBody, Expression = "$.id" }],
    };

    [Fact]
    public async Task On_success_runs_captures_assertions_and_records_history()
    {
        var executor = new FakeExecutorRegistry(new ExecutionResult { StatusCode = 200, Body = "{\"id\":1}" });
        var tests = new FakeTestService();
        var history = new FakeHistoryService();
        var sut = new RequestExecutionService(new FakeAuthProvider(), executor, tests, history);

        var result = await sut.RunAsync(new RequestRun(RequestWithTests(), Ws, null, EffectiveAuth: null));

        Assert.True(tests.CapturesApplied);
        Assert.True(tests.AssertionsRun);
        Assert.NotNull(result.HistorySnapshot);
        Assert.Equal(1, history.AppendCount);
        Assert.Null(result.HistoryError);
    }

    [Fact]
    public async Task On_transport_error_skips_tests_but_still_records_history()
    {
        var executor = new FakeExecutorRegistry(new ExecutionResult { ErrorMessage = "boom" });
        var tests = new FakeTestService();
        var history = new FakeHistoryService();
        var sut = new RequestExecutionService(new FakeAuthProvider(), executor, tests, history);

        var result = await sut.RunAsync(new RequestRun(RequestWithTests(), Ws, null, EffectiveAuth: null));

        Assert.False(tests.CapturesApplied);
        Assert.False(tests.AssertionsRun);
        Assert.NotNull(result.HistorySnapshot); // error sends are still history
        Assert.Equal(1, history.AppendCount);
    }

    [Fact]
    public async Task Ensures_auth_only_when_effective_auth_is_supplied()
    {
        var auth = new FakeAuthProvider();
        var sut = new RequestExecutionService(auth, new FakeExecutorRegistry(new ExecutionResult { StatusCode = 200 }), new FakeTestService(), new FakeHistoryService());

        await sut.RunAsync(new RequestRun(new RequestModel { Name = "r" }, Ws, null, EffectiveAuth: null));
        Assert.Equal(0, auth.EnsureCount);

        await sut.RunAsync(new RequestRun(new RequestModel { Name = "r" }, Ws, null, new AuthConfig { Type = AuthType.Bearer }));
        Assert.Equal(1, auth.EnsureCount);
    }

    [Fact]
    public async Task History_persistence_failure_is_surfaced_not_thrown()
    {
        var sut = new RequestExecutionService(new FakeAuthProvider(), new FakeExecutorRegistry(new ExecutionResult { StatusCode = 200 }), new FakeTestService(), new ThrowingHistoryService());

        var result = await sut.RunAsync(new RequestRun(new RequestModel { Name = "r" }, Ws, null, null));

        Assert.Null(result.HistorySnapshot);
        Assert.Equal("disk full", result.HistoryError);
    }

    // --- fakes -------------------------------------------------------------------------------------

    private sealed class FakeAuthProvider : IAuthProvider
    {
        public int EnsureCount { get; private set; }

        public Task<AuthOutcome> EnsureAsync(AuthConfig auth, Workspace workspace, WorkspaceEnvironment? env, CancellationToken ct = default)
        {
            EnsureCount++;
            return Task.FromResult(new AuthOutcome(true, ""));
        }

        public string PreviewTokenRequest(AuthConfig auth, Workspace workspace, WorkspaceEnvironment? env) => "";
    }

    private sealed class FakeExecutorRegistry(ExecutionResult result) : IExecutorRegistry, IRequestExecutor
    {
        public RequestKind Kind => RequestKind.Http;

        public IRequestExecutor Resolve(RequestKind kind) => this;

        public Task<ExecutionResult> ExecuteAsync(RequestModel request, RequestExecutionContext context, CancellationToken ct = default) =>
            Task.FromResult(result);
    }

    private sealed class FakeTestService : IResponseTestService
    {
        public bool AssertionsRun { get; private set; }

        public bool CapturesApplied { get; private set; }

        public IReadOnlyList<AssertionResult> RunAssertions(IReadOnlyList<Assertion> assertions, ExecutionResult result)
        {
            AssertionsRun = true;
            return [new AssertionResult(true, "ok", "200")];
        }

        public Task<IReadOnlyList<CaptureResult>> ApplyCapturesAsync(IReadOnlyList<CaptureRule> captures, ExecutionResult result, Workspace workspace, WorkspaceEnvironment? env, CancellationToken ct = default)
        {
            CapturesApplied = true;
            return Task.FromResult<IReadOnlyList<CaptureResult>>([new CaptureResult(true, "id", "1", "session", null)]);
        }
    }

    private sealed class FakeHistoryService : IHistoryService
    {
        public int AppendCount { get; private set; }

        public Task<IReadOnlyList<ExecutionSnapshot>> LoadAsync(string root, string id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExecutionSnapshot>>([]);

        public Task AppendAsync(string root, string id, ExecutionSnapshot snapshot, CancellationToken ct = default)
        {
            AppendCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHistoryService : IHistoryService
    {
        public Task<IReadOnlyList<ExecutionSnapshot>> LoadAsync(string root, string id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExecutionSnapshot>>([]);

        public Task AppendAsync(string root, string id, ExecutionSnapshot snapshot, CancellationToken ct = default) =>
            throw new IOException("disk full");
    }
}
