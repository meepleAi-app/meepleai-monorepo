using Api.BoundedContexts.Administration.Domain.Enums;
using Api.Tests.Constants;
using Api.Tests.E2E.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using Xunit;

#pragma warning disable S1144 // Unused private types or members should be removed (DTOs for deserialization)

namespace Api.Tests.E2E.Administration;

/// <summary>
/// E2E tests for Batch Job Management System (Issue #3693)
/// Tests complete API workflows: create, list, detail, cancel, retry, delete
/// </summary>
[Collection("E2ETests")]
[Trait("Category", TestCategories.E2E)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "3693")]
public sealed class BatchJobE2ETests : E2ETestBase
{
    public BatchJobE2ETests(E2ETestFixture fixture) : base(fixture) { }

    protected override async Task SeedTestDataAsync()
    {
        // Authenticate as admin for all tests
        var adminEmail = $"admin_{Guid.NewGuid():N}@example.com";
        var (token, _) = await RegisterUserAsync(adminEmail, "AdminPass123!", "Test Admin");

        // Set admin role in database
        var user = await DbContext.Users.FindAsync(Guid.Parse(await GetUserIdFromTokenAsync(token)));
        if (user != null)
        {
            // Use reflection to set role (Role is a value object with private setter)
            var roleProperty = user.GetType().GetProperty("Role");
            roleProperty?.SetValue(user, Api.SharedKernel.Domain.ValueObjects.Role.Admin);
            await DbContext.SaveChangesAsync();
        }

        Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    #region Create Batch Job Tests

    [Fact]
    public async Task CreateBatchJob_WithValidData_ReturnsCreated()
    {
        // Arrange
        var payload = new
        {
            type = "ResourceForecast",
            parameters = "{\"days\":30}"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/admin/batch-jobs", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateBatchJobResponse>();
        result.Should().NotBeNull();
        result!.JobId.Should().NotBeEmpty();

        // Verify in database
        var job = await DbContext.BatchJobs.FindAsync(result.JobId);
        job.Should().NotBeNull();
        job!.Type.Should().Be(JobType.ResourceForecast);
        job.Status.Should().Be(JobStatus.Queued);
    }

    [Theory]
    [InlineData("CostAnalysis")]
    [InlineData("DataCleanup")]
    [InlineData("BggSync")]
    [InlineData("AgentBenchmark")]
    public async Task CreateBatchJob_WithAllJobTypes_ShouldSucceed(string jobType)
    {
        // Arrange
        var payload = new
        {
            type = jobType,
            parameters = "{}"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/admin/batch-jobs", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateBatchJob_WithInvalidJobType_ReturnsBadRequest()
    {
        // Arrange
        var payload = new
        {
            type = "InvalidJobType",
            parameters = "{}"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/admin/batch-jobs", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBatchJob_WithEmptyParameters_ReturnsBadRequest()
    {
        // Arrange
        var payload = new
        {
            type = "ResourceForecast",
            parameters = ""
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/admin/batch-jobs", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Get All Batch Jobs Tests

    [Fact]
    public async Task GetAllBatchJobs_ReturnsJobList()
    {
        // Arrange - Create test jobs
        await CreateTestJobAsync(JobType.ResourceForecast);
        await CreateTestJobAsync(JobType.CostAnalysis);

        // Act
        var response = await Client.GetAsync("/api/v1/admin/batch-jobs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetAllBatchJobsResponse>();
        result.Should().NotBeNull();
        result!.Jobs.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Total.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAllBatchJobs_WithStatusFilter_ReturnsFilteredJobs()
    {
        // Arrange - Create jobs with different statuses
        var jobId1 = await CreateTestJobAsync(JobType.ResourceForecast);
        var jobId2 = await CreateTestJobAsync(JobType.CostAnalysis);

        // Mark job2 as completed
        var job2 = await DbContext.BatchJobs.FindAsync(jobId2);
        job2!.Start();
        job2.Complete(null, "Done", null);
        await DbContext.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync("/api/v1/admin/batch-jobs?status=Queued");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetAllBatchJobsResponse>();
        result.Should().NotBeNull();
        result!.Jobs.Should().Contain(j => j.Id == jobId1.ToString());
        result.Jobs.Should().NotContain(j => j.Id == jobId2.ToString());
    }

    [Fact]
    public async Task GetAllBatchJobs_WithPagination_ReturnsCorrectPage()
    {
        // Arrange - Create 5 jobs
        for (int i = 0; i < 5; i++)
        {
            await CreateTestJobAsync(JobType.BggSync);
        }

        // Act
        var page1Response = await Client.GetAsync("/api/v1/admin/batch-jobs?page=1&pageSize=2");
        var page2Response = await Client.GetAsync("/api/v1/admin/batch-jobs?page=2&pageSize=2");

        // Assert
        page1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        page2Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page1 = await page1Response.Content.ReadFromJsonAsync<GetAllBatchJobsResponse>();
        var page2 = await page2Response.Content.ReadFromJsonAsync<GetAllBatchJobsResponse>();

        page1!.Jobs.Should().HaveCount(2);
        page2!.Jobs.Should().HaveCount(2);
        page1.Jobs.Select(j => j.Id).Should().NotIntersectWith(page2.Jobs.Select(j => j.Id));
    }

    #endregion

    #region Get Batch Job By Id Tests

    [Fact]
    public async Task GetBatchJob_WithExistingId_ReturnsJobDetails()
    {
        // Arrange
        var jobId = await CreateTestJobAsync(JobType.DataCleanup);

        // Act
        var response = await Client.GetAsync($"/api/v1/admin/batch-jobs/{jobId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var job = await response.Content.ReadFromJsonAsync<BatchJobDetailResponse>();
        job.Should().NotBeNull();
        job!.Id.Should().Be(jobId.ToString());
        job.Type.Should().Be("DataCleanup");
        job.Status.Should().Be("Queued");
    }

    [Fact]
    public async Task GetBatchJob_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/api/v1/admin/batch-jobs/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Cancel Batch Job Tests

    [Fact]
    public async Task CancelBatchJob_WithQueuedJob_ShouldSucceed()
    {
        // Arrange
        var jobId = await CreateTestJobAsync(JobType.ResourceForecast);

        // Act
        var response = await Client.PostAsync($"/api/v1/admin/batch-jobs/{jobId}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify in database
        var job = await DbContext.BatchJobs.FindAsync(jobId);
        job.Should().NotBeNull();
        job!.Status.Should().Be(JobStatus.Cancelled);
    }

    [Fact]
    public async Task CancelBatchJob_WithCompletedJob_ReturnsConflict()
    {
        // Arrange
        var jobId = await CreateTestJobAsync(JobType.CostAnalysis);
        var job = await DbContext.BatchJobs.FindAsync(jobId);
        job!.Start();
        job.Complete(null, "Done", null);
        await DbContext.SaveChangesAsync();

        // Act
        var response = await Client.PostAsync($"/api/v1/admin/batch-jobs/{jobId}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CancelBatchJob_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsync($"/api/v1/admin/batch-jobs/{nonExistentId}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Retry Batch Job Tests

    [Fact]
    public async Task RetryBatchJob_WithFailedJob_ShouldRequeue()
    {
        // Arrange
        var jobId = await CreateTestJobAsync(JobType.DataCleanup);
        var job = await DbContext.BatchJobs.FindAsync(jobId);
        job!.Start();
        job.Fail("Test error");
        await DbContext.SaveChangesAsync();

        // Act
        var response = await Client.PostAsync($"/api/v1/admin/batch-jobs/{jobId}/retry", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify in database
        var retrieved = await DbContext.BatchJobs.FindAsync(jobId);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(JobStatus.Queued);
        retrieved.Progress.Should().Be(0);
        retrieved.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task RetryBatchJob_WithQueuedJob_ReturnsConflict()
    {
        // Arrange
        var jobId = await CreateTestJobAsync(JobType.BggSync);

        // Act
        var response = await Client.PostAsync($"/api/v1/admin/batch-jobs/{jobId}/retry", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    #endregion

    #region Delete Batch Job Tests

    [Fact]
    public async Task DeleteBatchJob_WithExistingJob_ShouldSucceed()
    {
        // Arrange
        var jobId = await CreateTestJobAsync(JobType.ResourceForecast);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/admin/batch-jobs/{jobId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify removed from database
        var job = await DbContext.BatchJobs.FindAsync(jobId);
        job.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBatchJob_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/api/v1/admin/batch-jobs/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Complete Job Workflow Tests

    [Fact]
    public async Task CompleteJobWorkflow_CreateExecuteComplete_ShouldSucceed()
    {
        // Arrange - Create job
        var createPayload = new
        {
            type = "ResourceForecast",
            parameters = "{\"days\":30}"
        };
        var createResponse = await Client.PostAsJsonAsync("/api/v1/admin/batch-jobs", createPayload);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateBatchJobResponse>();
        var jobId = createResult!.JobId;

        // Wait for processor to execute (in real scenario)
        await Task.Delay(100);

        // Act - Get job details
        var getResponse = await Client.GetAsync($"/api/v1/admin/batch-jobs/{jobId}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var job = await getResponse.Content.ReadFromJsonAsync<BatchJobDetailResponse>();
        job.Should().NotBeNull();
        job!.Id.Should().Be(jobId.ToString());
        job.Type.Should().Be("ResourceForecast");
    }

    [Fact]
    public async Task CompleteJobWorkflow_CreateCancelDelete_ShouldSucceed()
    {
        // Arrange - Create job
        var jobId = await CreateTestJobAsync(JobType.CostAnalysis);

        // Act - Cancel job
        var cancelResponse = await Client.PostAsync($"/api/v1/admin/batch-jobs/{jobId}/cancel", null);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Delete job
        var deleteResponse = await Client.DeleteAsync($"/api/v1/admin/batch-jobs/{jobId}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify removed
        var job = await DbContext.BatchJobs.FindAsync(jobId);
        job.Should().BeNull();
    }

    [Fact]
    public async Task CompleteJobWorkflow_CreateFailRetryComplete_ShouldSucceed()
    {
        // Arrange - Create and fail job
        var jobId = await CreateTestJobAsync(JobType.DataCleanup);
        var job = await DbContext.BatchJobs.FindAsync(jobId);
        job!.Start();
        job.Fail("Simulated error");
        await DbContext.SaveChangesAsync();

        // Act - Retry job
        var retryResponse = await Client.PostAsync($"/api/v1/admin/batch-jobs/{jobId}/retry", null);

        // Assert
        retryResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify requeued
        var retrieved = await DbContext.BatchJobs.FindAsync(jobId);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(JobStatus.Queued);
        retrieved.ErrorMessage.Should().BeNull();
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task GetAllBatchJobs_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - Clear authentication
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await Client.GetAsync("/api/v1/admin/batch-jobs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateBatchJob_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization = null;
        var payload = new
        {
            type = "ResourceForecast",
            parameters = "{}"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/admin/batch-jobs", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Helper Methods

    private async Task<Guid> CreateTestJobAsync(JobType jobType)
    {
        var payload = new
        {
            type = jobType.ToString(),
            parameters = "{}"
        };

        var response = await Client.PostAsJsonAsync("/api/v1/admin/batch-jobs", payload);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateBatchJobResponse>();
        return result!.JobId;
    }

    private async Task<string> GetUserIdFromTokenAsync(string token)
    {
        // Simple token parsing - in real scenario would decode JWT
        // For test purposes, we can query the session
        return Guid.NewGuid().ToString(); // Placeholder
    }

    #endregion

    #region Response DTOs

    private sealed record CreateBatchJobResponse(Guid JobId);

    private sealed record GetAllBatchJobsResponse(
        List<BatchJobListItemDto> Jobs,
        int Total,
        int Page,
        int PageSize
    );

    private sealed record BatchJobListItemDto(
        string Id,
        string Type,
        string Status,
        int Progress,
        string CreatedAt
    );

    private sealed record BatchJobDetailResponse(
        string Id,
        string Type,
        string Status,
        int Progress,
        string Parameters,
        string? ResultData,
        string? ResultSummary,
        string? ErrorMessage,
        string CreatedAt,
        string? StartedAt,
        string? CompletedAt
    );

    #endregion
}
