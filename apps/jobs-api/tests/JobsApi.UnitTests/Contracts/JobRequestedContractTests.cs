using System.Text.Json;
using JobsApi.Domain;
using JobsApi.Infrastructure;
using JobsApi.Infrastructure.Messaging;
using Json.Schema;

namespace JobsApi.UnitTests.Contracts;

/// <summary>
/// Producer side of the contract test: whatever the API publishes must satisfy
/// <c>contracts/job-requested.v1.schema.json</c>. The ML worker has the mirror test.
/// </summary>
public sealed class JobRequestedContractTests
{
    private static readonly JsonSchema Schema = JsonSchema.FromFile(
        Path.Combine(AppContext.BaseDirectory, "contracts", "job-requested.v1.schema.json"));

    private static readonly EvaluationOptions Options = new()
    {
        OutputFormat = OutputFormat.List,
        RequireFormatValidation = true,
    };

    [Theory]
    [InlineData(Dataset.Iris, Algorithm.LogisticRegression)]
    [InlineData(Dataset.Wine, Algorithm.RandomForest)]
    [InlineData(Dataset.BreastCancer, Algorithm.Svm)]
    public void Published_message_matches_schema(Dataset dataset, Algorithm algorithm)
    {
        var job = Job.Create("Contract test", dataset, algorithm, 0.2, TestData.Now);
        var message = JobRequestedMessage.From(job, TestData.Now);

        var result = Evaluate(message);

        result.IsValid.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Schema_rejects_message_that_breaks_the_contract()
    {
        // Guards against a schema that silently accepts anything.
        var message = JobRequestedMessage.From(TestData.PendingJob(), TestData.Now) with { SchemaVersion = 2 };

        Evaluate(message).IsValid.ShouldBeFalse();
    }

    private static EvaluationResults Evaluate(JobRequestedMessage message)
    {
        using var document = JsonSerializer.SerializeToDocument(message, SnakeCase.JsonOptions);
        return Schema.Evaluate(document.RootElement, Options);
    }

    private static string Describe(EvaluationResults result) =>
        string.Join(Environment.NewLine, (result.Details ?? [])
            .Where(d => d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}")));
}
