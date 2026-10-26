using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JobsApi.Infrastructure.Messaging;

namespace JobsApi.IntegrationTests;

public sealed class JobsEndpointsTests(JobsApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _client = fixture.CreateClient();

    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync();
        await PurgeWorkerQueueAsync();
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Post_creates_pending_job_and_returns_location()
    {
        var response = await _client.PostAsJsonAsync("/api/jobs", ValidBody("Iris forest"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var job = await ReadJsonAsync(response);
        job.GetProperty("name").GetString().ShouldBe("Iris forest");
        job.GetProperty("dataset").GetString().ShouldBe("iris");
        job.GetProperty("algorithm").GetString().ShouldBe("random_forest");
        job.GetProperty("status").GetString().ShouldBe("pending");
        response.Headers.Location!.ToString().ShouldBe($"/api/jobs/{job.GetProperty("id").GetGuid()}");
    }

    [Fact]
    public async Task Post_publishes_job_requested_message_to_worker_queue()
    {
        var response = await _client.PostAsJsonAsync("/api/jobs", ValidBody("Message check"), Ct);
        var jobId = (await ReadJsonAsync(response)).GetProperty("id").GetGuid();

        var message = await GetWorkerMessageAsync();

        message.GetProperty("message_type").GetString().ShouldBe("job.requested");
        message.GetProperty("schema_version").GetInt32().ShouldBe(1);
        message.GetProperty("job_id").GetGuid().ShouldBe(jobId);
        message.GetProperty("dataset").GetString().ShouldBe("iris");
        message.GetProperty("test_size").GetDouble().ShouldBe(0.25);
    }

    [Fact]
    public async Task Get_returns_created_jobs_newest_first()
    {
        await _client.PostAsJsonAsync("/api/jobs", ValidBody("First job"), Ct);
        await _client.PostAsJsonAsync("/api/jobs", ValidBody("Second job"), Ct);

        var jobs = await _client.GetFromJsonAsync<JsonElement>("/api/jobs", Ct);

        jobs.EnumerateArray().Select(j => j.GetProperty("name").GetString())
            .ShouldBe(["Second job", "First job"]);
    }

    [Fact]
    public async Task Get_by_id_returns_job_or_404()
    {
        var created = await ReadJsonAsync(await _client.PostAsJsonAsync("/api/jobs", ValidBody("Lookup"), Ct));

        var found = await _client.GetAsync(new Uri($"/api/jobs/{created.GetProperty("id").GetGuid()}", UriKind.Relative), Ct);
        var missing = await _client.GetAsync(new Uri($"/api/jobs/{Guid.CreateVersion7()}", UriKind.Relative), Ct);

        found.StatusCode.ShouldBe(HttpStatusCode.OK);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_with_invalid_body_returns_validation_problem()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/jobs",
            new { name = "x", dataset = "iris", algorithm = "svm", testSize = 0.9 },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var errors = (await ReadJsonAsync(response)).GetProperty("errors");
        errors.TryGetProperty("Name", out _).ShouldBeTrue();
        errors.TryGetProperty("TestSize", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Post_with_name_too_short_after_trimming_returns_bad_request_not_server_error()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/jobs",
            new { name = "   ab   ", dataset = "iris", algorithm = "svm" },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("errors").TryGetProperty("Name", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Post_with_unknown_dataset_returns_bad_request()
    {
        using var content = new StringContent(
            """{ "name": "Unknown dataset", "dataset": "mnist", "algorithm": "svm" }""",
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync(new Uri("/api/jobs", UriKind.Relative), content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Readiness_reports_healthy_when_dependencies_are_up()
    {
        var response = await _client.GetAsync(new Uri("/health/ready", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static object ValidBody(string name) =>
        new { name, dataset = "iris", algorithm = "random_forest", testSize = 0.25 };

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private async Task PurgeWorkerQueueAsync()
    {
        // Ensure topology exists (the API declares it on first connection) before purging.
        await _client.GetAsync(new Uri("/health/ready", UriKind.Relative), Ct);
        await using var connection = await fixture.ConnectToBrokerAsync();
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        await channel.QueuePurgeAsync(RabbitMqTopology.WorkerQueue, Ct);
    }

    private async Task<JsonElement> GetWorkerMessageAsync()
    {
        await using var connection = await fixture.ConnectToBrokerAsync();
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var delivery = await channel.BasicGetAsync(RabbitMqTopology.WorkerQueue, autoAck: true, Ct);
            if (delivery is not null)
            {
                return JsonDocument.Parse(delivery.Body).RootElement.Clone();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), Ct);
        }

        throw new TimeoutException($"No message arrived on '{RabbitMqTopology.WorkerQueue}'.");
    }
}
