using System.Text.Json;
using JobsApi.Domain;
using JobsApi.Features.Jobs;
using JobsApi.Infrastructure;

namespace JobsApi.UnitTests.Features.Jobs;

public sealed class JobResponseTests
{
    // Mirrors ConfigureHttpJsonOptions in Program.cs.
    private static readonly JsonSerializerOptions HttpJson =
        new(JsonSerializerDefaults.Web) { Converters = { SnakeCase.CreateEnumConverter() } };

    [Fact]
    public void Maps_all_job_fields()
    {
        var job = TestData.PendingJob("Mapping test");

        var response = JobResponse.From(job);

        response.Id.ShouldBe(job.Id);
        response.Name.ShouldBe("Mapping test");
        response.Dataset.ShouldBe(Dataset.Iris);
        response.Algorithm.ShouldBe(Algorithm.RandomForest);
        response.TestSize.ShouldBe(0.2);
        response.Status.ShouldBe(JobStatus.Pending);
        response.Metrics.ShouldBeNull();
        response.CreatedAt.ShouldBe(TestData.Now);
    }

    [Fact]
    public void Http_response_uses_camel_case_properties_and_snake_case_enum_values()
    {
        var job = Job.Create("Cancer LR", Dataset.BreastCancer, Algorithm.LogisticRegression, 0.2, TestData.Now);

        var json = JsonSerializer.Serialize(JobResponse.From(job), HttpJson);

        json.ShouldContain("\"dataset\":\"breast_cancer\"");
        json.ShouldContain("\"algorithm\":\"logistic_regression\"");
        json.ShouldContain("\"status\":\"pending\"");
        json.ShouldContain("\"testSize\":0.2");
    }
}
