using JobsApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JobsApi.Features.Jobs;

public static class JobEndpoints
{
    public const int ListLimit = 100;

    public static IServiceCollection AddJobsFeature(this IServiceCollection services)
    {
        services.AddScoped<CreateJobHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var jobs = app.MapGroup("/api/jobs").WithTags("Jobs");

        jobs.MapGet("/", GetJobsAsync)
            .WithName("GetJobs")
            .WithSummary($"Lists the {ListLimit} most recent jobs, newest first.");

        jobs.MapGet("/{id:guid}", GetJobByIdAsync)
            .WithName("GetJobById")
            .WithSummary("Gets a single job.");

        jobs.MapPost("/", CreateJobAsync)
            .WithName("CreateJob")
            .WithSummary("Creates a job and dispatches it to the ML worker.");

        return app;
    }

    internal static async Task<Ok<JobResponse[]>> GetJobsAsync(IJobRepository repository, CancellationToken cancellationToken)
    {
        var jobs = await repository.ListAsync(ListLimit, cancellationToken);
        return TypedResults.Ok(jobs.Select(JobResponse.From).ToArray());
    }

    internal static async Task<Results<Ok<JobResponse>, NotFound>> GetJobByIdAsync(
        Guid id, IJobRepository repository, CancellationToken cancellationToken)
    {
        var job = await repository.FindAsync(id, cancellationToken);
        return job is null ? TypedResults.NotFound() : TypedResults.Ok(JobResponse.From(job));
    }

    internal static async Task<Results<Created<JobResponse>, ProblemHttpResult>> CreateJobAsync(
        CreateJobRequest request, CreateJobHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request, cancellationToken);
        var response = JobResponse.From(result.Job);

        return result.Outcome switch
        {
            CreateJobOutcome.Dispatched => TypedResults.Created($"/api/jobs/{response.Id}", response),
            _ => TypedResults.Problem(
                title: "Job dispatch failed",
                detail: CreateJobHandler.DispatchFailedError,
                statusCode: StatusCodes.Status503ServiceUnavailable,
                extensions: new Dictionary<string, object?> { ["jobId"] = response.Id }),
        };
    }
}
