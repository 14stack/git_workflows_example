using JobsApi.Domain;
using JobsApi.Features.Jobs;
using JobsApi.Infrastructure.Messaging;
using JobsApi.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace JobsApi.UnitTests.Features.Jobs;

public sealed class CreateJobHandlerTests
{
    private readonly IJobRepository _repository = Substitute.For<IJobRepository>();
    private readonly IJobDispatcher _dispatcher = Substitute.For<IJobDispatcher>();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private readonly CreateJobHandler _handler;

    public CreateJobHandlerTests()
    {
        _handler = new CreateJobHandler(_repository, _dispatcher, _time, NullLogger<CreateJobHandler>.Instance);
    }

    [Fact]
    public async Task Persists_pending_job_and_dispatches_matching_message()
    {
        var result = await _handler.HandleAsync(Request(), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(CreateJobOutcome.Dispatched);
        result.Job.Status.ShouldBe(JobStatus.Pending);

        _repository.Received(1).Add(result.Job);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).DispatchAsync(
            Arg.Is<JobRequestedMessage>(m =>
                m.JobId == result.Job.Id &&
                m.Name == "Wine SVM" &&
                m.Dataset == Dataset.Wine &&
                m.Algorithm == Algorithm.Svm &&
                m.TestSize == 0.3 &&
                m.MessageType == JobRequestedMessage.Type &&
                m.SchemaVersion == JobRequestedMessage.Version),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Saves_job_before_dispatching_so_the_worker_never_sees_an_unknown_job()
    {
        await _handler.HandleAsync(Request(), TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            _repository.SaveChangesAsync(Arg.Any<CancellationToken>());
            _dispatcher.DispatchAsync(Arg.Any<JobRequestedMessage>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Marks_job_failed_when_broker_is_unavailable()
    {
        _dispatcher
            .DispatchAsync(Arg.Any<JobRequestedMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("connection refused"));

        var result = await _handler.HandleAsync(Request(), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(CreateJobOutcome.DispatchFailed);
        result.Job.Status.ShouldBe(JobStatus.Failed);
        result.Job.Error.ShouldBe(CreateJobHandler.DispatchFailedError);
        await _repository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Does_not_swallow_cancellation()
    {
        _dispatcher
            .DispatchAsync(Arg.Any<JobRequestedMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _handler.HandleAsync(Request(), TestContext.Current.CancellationToken);

        await act.ShouldThrowAsync<OperationCanceledException>();
    }

    private static CreateJobRequest Request() => new()
    {
        Name = "Wine SVM",
        Dataset = Dataset.Wine,
        Algorithm = Algorithm.Svm,
        TestSize = 0.3,
    };
}
