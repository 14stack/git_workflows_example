using JobsApi.Domain;

namespace JobsApi.UnitTests.Domain;

public sealed class JobTests
{
    [Fact]
    public void Create_returns_pending_job_with_trimmed_name()
    {
        var job = Job.Create("  Wine forest  ", Dataset.Wine, Algorithm.RandomForest, 0.25, TestData.Now);

        job.Name.ShouldBe("Wine forest");
        job.Dataset.ShouldBe(Dataset.Wine);
        job.Algorithm.ShouldBe(Algorithm.RandomForest);
        job.TestSize.ShouldBe(0.25);
        job.Status.ShouldBe(JobStatus.Pending);
        job.CreatedAt.ShouldBe(TestData.Now);
        job.Metrics.ShouldBeNull();
        job.Error.ShouldBeNull();
        job.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public void Create_assigns_time_ordered_version_7_id()
    {
        var job = TestData.PendingJob();

        job.Id.Version.ShouldBe(7);
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.55)]
    public void Create_rejects_test_size_outside_supported_range(double testSize)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Job.Create("Valid name", Dataset.Iris, Algorithm.Svm, testSize, TestData.Now))
            .ParamName.ShouldBe("testSize");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_name(string name)
    {
        Should.Throw<ArgumentException>(() => Job.Create(name, Dataset.Iris, Algorithm.Svm, 0.2, TestData.Now));
    }

    [Fact]
    public void Create_rejects_name_that_is_too_short_after_trimming()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Job.Create("  ab  ", Dataset.Iris, Algorithm.Svm, 0.2, TestData.Now))
            .ParamName.ShouldBe("name");
    }

    [Fact]
    public void MarkDispatchFailed_moves_pending_job_to_failed()
    {
        var job = TestData.PendingJob();
        var failedAt = TestData.Now.AddSeconds(1);

        job.MarkDispatchFailed("broker down", failedAt);

        job.Status.ShouldBe(JobStatus.Failed);
        job.Error.ShouldBe("broker down");
        job.CompletedAt.ShouldBe(failedAt);
    }

    [Fact]
    public void MarkDispatchFailed_rejects_job_that_is_no_longer_pending()
    {
        var job = TestData.PendingJob();
        job.MarkDispatchFailed("broker down", TestData.Now);

        var act = () => job.MarkDispatchFailed("again", TestData.Now);

        act.ShouldThrow<InvalidOperationException>();
    }
}
