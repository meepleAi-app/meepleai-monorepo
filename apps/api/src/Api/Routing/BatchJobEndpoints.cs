using Api.BoundedContexts.Administration.Application.Commands.BatchJobs;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.BatchJobs;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Batch Job System endpoints for enterprise operations management (Issue #3693)
/// </summary>
internal static class BatchJobEndpoints
{
    public static RouteGroupBuilder MapBatchJobEndpoints(this RouteGroupBuilder group)
    {
        var batchJobsGroup = group.MapGroup("/admin/operations/batch-jobs")
            .WithTags("Admin - Batch Jobs");

        // GET /api/v1/admin/operations/batch-jobs - List batch jobs
        batchJobsGroup.MapGet("/", HandleGetAllBatchJobs)
            .WithName("GetAllBatchJobs")
            .WithSummary("Get all batch jobs with optional filtering and pagination")
            .Produces<BatchJobListDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/operations/batch-jobs/{id} - Get batch job details
        batchJobsGroup.MapGet("/{id:guid}", HandleGetBatchJob)
            .WithName("GetBatchJob")
            .WithSummary("Get batch job details by ID")
            .Produces<BatchJobDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // POST /api/v1/admin/operations/batch-jobs - Create batch job
        batchJobsGroup.MapPost("/", HandleCreateBatchJob)
            .WithName("CreateBatchJob")
            .WithSummary("Create a new batch job")
            .Produces<CreateBatchJobResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // PUT /api/v1/admin/operations/batch-jobs/{id}/cancel - Cancel batch job
        batchJobsGroup.MapPut("/{id:guid}/cancel", HandleCancelBatchJob)
            .WithName("CancelBatchJob")
            .WithSummary("Cancel a running or queued batch job")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // PUT /api/v1/admin/operations/batch-jobs/{id}/retry - Retry batch job
        batchJobsGroup.MapPut("/{id:guid}/retry", HandleRetryBatchJob)
            .WithName("RetryBatchJob")
            .WithSummary("Retry a failed batch job")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // DELETE /api/v1/admin/operations/batch-jobs/{id} - Delete batch job
        batchJobsGroup.MapDelete("/{id:guid}", HandleDeleteBatchJob)
            .WithName("DeleteBatchJob")
            .WithSummary("Delete a batch job")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return group;
    }

    private static async Task<IResult> HandleGetAllBatchJobs(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct,
        string? status = null,
        int page = 1,
        int pageSize = 20)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        JobStatus? statusEnum = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<JobStatus>(status, true, out var parsed))
        {
            statusEnum = parsed;
        }

        var query = new GetAllBatchJobsQuery(statusEnum, page, pageSize);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetBatchJob(
        Guid id,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetBatchJobQuery(id);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return result == null
            ? Results.NotFound(new { message = $"Batch job with ID {id} not found" })
            : Results.Ok(result);
    }

    private static async Task<IResult> HandleCreateBatchJob(
        CreateBatchJobRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        if (!Enum.TryParse<JobType>(request.Type, true, out var jobType))
        {
            return Results.BadRequest(new { message = "Invalid job type" });
        }

        var command = new CreateBatchJobCommand(jobType, request.Parameters, session!.User!.Id);
        var jobId = await mediator.Send(command, ct).ConfigureAwait(false);

        return Results.Created($"/api/v1/admin/operations/batch-jobs/{jobId}", new CreateBatchJobResponse(jobId));
    }

    private static async Task<IResult> HandleCancelBatchJob(
        Guid id,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new CancelBatchJobCommand(id);
        await mediator.Send(command, ct).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> HandleRetryBatchJob(
        Guid id,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new RetryBatchJobCommand(id);
        await mediator.Send(command, ct).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> HandleDeleteBatchJob(
        Guid id,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new DeleteBatchJobCommand(id);
        await mediator.Send(command, ct).ConfigureAwait(false);

        return Results.NoContent();
    }
}
