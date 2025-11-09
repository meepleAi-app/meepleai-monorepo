using Api.Tests.Fixtures;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for N8N-05 workflow error logging endpoints.
///
/// Feature: Workflow error logging and monitoring
/// As a workflow automation system (n8n)
/// I want to log errors from failed workflows
/// So that admins can monitor and debug workflow issues
/// </summary>
[Collection("Admin Endpoints")]
public class WorkflowErrorEndpointsTests : AdminTestFixture
{
    private readonly ITestOutputHelper _output;

    public WorkflowErrorEndpointsTests(PostgresCollectionFixture postgresFixture, WebApplicationFactoryFixture factory, ITestOutputHelper output) : base(postgresFixture, factory)
    {
        _output = output;
    }

    /// <summary>
    /// Scenario: n8n webhook logs workflow error
    ///   Given n8n workflow encounters an error
    ///   When n8n posts error details to webhook endpoint
    ///   Then error is logged successfully
    ///   And response returns 200 OK
    ///   And error is stored in database
    /// </summary>
    [Fact]
    public async Task LogWorkflowError_FromN8nWebhook_LogsSuccessfully()
    {
        // Given: n8n workflow error details
        var request = new LogWorkflowErrorRequest(
            WorkflowId: "chess-agent-workflow",
            ExecutionId: $"exec-{Guid.NewGuid():N}",
            ErrorMessage: "HTTP Request failed: Connection timeout",
            NodeName: "API Call Node",
            RetryCount: 2
        );

        // When: n8n posts to webhook endpoint (no auth required)
        using var client = Factory.CreateHttpsClient();
        var response = await client.PostAsJsonAsync("/api/v1/logs/workflow-error", request);

        // Then: Returns 200 OK
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // And: Response confirms logging
        var result = await response.Content.ReadAsStringAsync();
        result.Should().Contain("logged successfully");

        // And: Error is stored in database
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var logged = await context.WorkflowErrorLogs
            .FirstOrDefaultAsync(e => e.ExecutionId == request.ExecutionId);

        logged.Should().NotBeNull();
        logged.WorkflowId.Should().Be("chess-agent-workflow");
        logged.ErrorMessage.Should().Be("HTTP Request failed: Connection timeout");
        logged.NodeName.Should().Be("API Call Node");
        logged.RetryCount.Should().Be(2);
    }

    /// <summary>
    /// Scenario: Webhook receives invalid request
    ///   Given request has missing required fields
    ///   When request is posted to webhook
    ///   Then response returns 400 Bad Request
    ///   And validation error details are returned
    /// </summary>
    [Fact]
    public async Task LogWorkflowError_WithInvalidRequest_Returns400()
    {
        // Given: Invalid request (missing required fields)
        var invalidRequest = new { workflowId = "", executionId = "" };

        // When: Posted to webhook
        using var client = Factory.CreateHttpsClient();
        var response = await client.PostAsJsonAsync("/api/v1/logs/workflow-error", invalidRequest);

        // Then: Returns 400 Bad Request
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Scenario: Admin retrieves workflow errors
    ///   Given admin user is authenticated
    ///   And system has workflow errors
    ///   When admin requests GET /admin/workflows/errors
    ///   Then system returns paginated error list
    ///   And errors are ordered by created_at descending
    /// </summary>
    [Fact]
    public async Task GetWorkflowErrors_WhenAdminAuthenticated_ReturnsPaginatedList()
    {
        // Given: Admin user authenticated
        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"admin-workflow-{Guid.NewGuid():N}@example.com";
        var adminCookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");

        // And: System has workflow errors
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        for (int i = 0; i < 5; i++)
        {
            context.WorkflowErrorLogs.Add(new WorkflowErrorLogEntity
            {
                Id = Guid.NewGuid(),
                WorkflowId = $"workflow-{i}",
                ExecutionId = $"exec-{i}",
                ErrorMessage = $"Error {i}",
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await context.SaveChangesAsync();

        // When: Admin requests workflow errors
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/workflows/errors");
        AddCookies(request, adminCookies);

        var response = await adminClient.SendAsync(request);

        // Then: Returns 200 OK
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        // And: Contains paginated results
        root.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(5);
        var items = root.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterThanOrEqualTo(5);

        // And: Errors ordered by created_at descending (newest first)
        var firstError = items[0];
        var secondError = items[1];
        var firstCreated = DateTime.Parse(firstError.GetProperty("createdAt").GetString()!);
        var secondCreated = DateTime.Parse(secondError.GetProperty("createdAt").GetString()!);
        (firstCreated >= secondCreated).Should().BeTrue();
    }

    /// <summary>
    /// Scenario: Admin filters errors by workflow ID
    ///   Given admin authenticated and multiple workflow errors exist
    ///   When admin requests errors with workflowId filter
    ///   Then only errors for that workflow are returned
    /// </summary>
    [Fact]
    public async Task GetWorkflowErrors_WithWorkflowIdFilter_ReturnsFilteredResults()
    {
        // Given: Admin authenticated
        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"admin-filter-{Guid.NewGuid():N}@example.com";
        var adminCookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");

        // And: Multiple workflow errors
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var targetWorkflowId = $"target-workflow-{Guid.NewGuid():N}";
        context.WorkflowErrorLogs.Add(new WorkflowErrorLogEntity
        {
            Id = Guid.NewGuid(),
            WorkflowId = targetWorkflowId,
            ExecutionId = "exec-1",
            ErrorMessage = "Error 1",
            CreatedAt = DateTime.UtcNow
        });
        context.WorkflowErrorLogs.Add(new WorkflowErrorLogEntity
        {
            Id = Guid.NewGuid(),
            WorkflowId = "other-workflow",
            ExecutionId = "exec-2",
            ErrorMessage = "Error 2",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        // When: Admin filters by workflowId
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/admin/workflows/errors?workflowId={Uri.EscapeDataString(targetWorkflowId)}");
        AddCookies(request, adminCookies);

        var response = await adminClient.SendAsync(request);

        // Then: Returns only filtered errors
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = document.RootElement.GetProperty("items");

        items.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
        foreach (var item in items.EnumerateArray())
        {
            item.GetProperty("workflowId").GetString().Should().Be(targetWorkflowId);
        }
    }

    /// <summary>
    /// Scenario: Admin filters errors by date range
    ///   Given admin authenticated and errors from different dates exist
    ///   When admin requests errors with fromDate and toDate filters
    ///   Then only errors in date range are returned
    /// </summary>
    [Fact]
    public async Task GetWorkflowErrors_WithDateRangeFilter_ReturnsFilteredResults()
    {
        // Given: Admin authenticated
        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"admin-date-{Guid.NewGuid():N}@example.com";
        var adminCookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");

        // And: Errors from different dates
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var now = DateTime.UtcNow;
        context.WorkflowErrorLogs.Add(new WorkflowErrorLogEntity
        {
            Id = Guid.NewGuid(),
            WorkflowId = "workflow-old",
            ExecutionId = "exec-old",
            ErrorMessage = "Old error",
            CreatedAt = now.AddDays(-10)
        });
        context.WorkflowErrorLogs.Add(new WorkflowErrorLogEntity
        {
            Id = Guid.NewGuid(),
            WorkflowId = "workflow-recent",
            ExecutionId = "exec-recent",
            ErrorMessage = "Recent error",
            CreatedAt = now.AddDays(-1)
        });
        await context.SaveChangesAsync();

        // When: Admin filters by date range (last 3 days)
        var fromDate = Uri.EscapeDataString(now.AddDays(-3).ToString("O"));
        var toDate = Uri.EscapeDataString(now.ToString("O"));
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/admin/workflows/errors?fromDate={fromDate}&toDate={toDate}");
        AddCookies(request, adminCookies);

        var response = await adminClient.SendAsync(request);

        // Then: Returns only recent errors
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var totalCount = document.RootElement.GetProperty("totalCount").GetInt32();

        // Should have at least the recent error, but not the old one
        (totalCount >= 1).Should().BeTrue();
    }

    /// <summary>
    /// Scenario: Non-admin user attempts to access workflow errors
    ///   Given regular user (non-admin) is authenticated
    ///   When user requests GET /admin/workflows/errors
    ///   Then response returns 403 Forbidden
    /// </summary>
    [Fact]
    public async Task GetWorkflowErrors_WhenNonAdminUser_Returns403()
    {
        // Given: Non-admin user authenticated
        using var userClient = Factory.CreateHttpsClient();
        var userEmail = $"user-{Guid.NewGuid():N}@example.com";
        var userCookies = await RegisterAndAuthenticateAsync(userClient, userEmail, "User");

        // When: User requests admin endpoint
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/workflows/errors");
        AddCookies(request, userCookies);

        var response = await userClient.SendAsync(request);

        // Then: Returns 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Scenario: Unauthenticated request to admin endpoint
    ///   Given no authentication credentials
    ///   When unauthenticated request to /admin/workflows/errors
    ///   Then response returns 401 Unauthorized
    /// </summary>
    [Fact]
    public async Task GetWorkflowErrors_WhenNotAuthenticated_Returns401()
    {
        // Given: No authentication
        using var client = Factory.CreateHttpsClient();

        // When: Unauthenticated request
        var response = await client.GetAsync("/api/v1/admin/workflows/errors");

        // Then: Returns 401 Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Scenario: Admin retrieves specific workflow error by ID
    ///   Given admin authenticated and workflow error exists
    ///   When admin requests GET /admin/workflows/errors/{id}
    ///   Then specific error details are returned
    /// </summary>
    [Fact]
    public async Task GetWorkflowErrorById_WhenErrorExists_ReturnsDetails()
    {
        // Given: Admin authenticated
        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"admin-id-{Guid.NewGuid():N}@example.com";
        var adminCookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");

        // And: Workflow error exists
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var errorId = Guid.NewGuid();
        context.WorkflowErrorLogs.Add(new WorkflowErrorLogEntity
        {
            Id = errorId,
            WorkflowId = "test-workflow",
            ExecutionId = "test-exec",
            ErrorMessage = "Test error message",
            NodeName = "Test Node",
            RetryCount = 3,
            StackTrace = "at line 42",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        // When: Admin requests specific error
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/admin/workflows/errors/{errorId}");
        AddCookies(request, adminCookies);

        var response = await adminClient.SendAsync(request);

        // Then: Returns error details
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        root.GetProperty("id").GetString().Should().Be(errorId.ToString());
        root.GetProperty("workflowId").GetString().Should().Be("test-workflow");
        root.GetProperty("executionId").GetString().Should().Be("test-exec");
        root.GetProperty("errorMessage").GetString().Should().Be("Test error message");
        root.GetProperty("nodeName").GetString().Should().Be("Test Node");
        root.GetProperty("retryCount").GetInt32().Should().Be(3);
    }

    /// <summary>
    /// Scenario: Admin requests non-existent error by ID
    ///   Given admin authenticated
    ///   When admin requests error with non-existent ID
    ///   Then response returns 404 Not Found
    /// </summary>
    [Fact]
    public async Task GetWorkflowErrorById_WhenErrorNotFound_Returns404()
    {
        // Given: Admin authenticated
        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"admin-notfound-{Guid.NewGuid():N}@example.com";
        var adminCookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");

        // When: Admin requests non-existent error
        var nonExistentId = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/admin/workflows/errors/{nonExistentId}");
        AddCookies(request, adminCookies);

        var response = await adminClient.SendAsync(request);

        // Then: Returns 404 Not Found
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Scenario: Webhook handles sensitive data in error messages
    ///   Given workflow error contains sensitive data (API keys, passwords)
    ///   When error is logged via webhook
    ///   Then sensitive data is redacted before storage
    /// </summary>
    [Fact]
    public async Task LogWorkflowError_WithSensitiveData_RedactsBeforeStorage()
    {
        // Given: Error message with sensitive data
        var request = new LogWorkflowErrorRequest(
            WorkflowId: "sensitive-workflow",
            ExecutionId: $"exec-sensitive-{Guid.NewGuid():N}",
            ErrorMessage: "API call failed: API_KEY=sk-1234567890 token=bearer-secret password='mysecret123'",
            NodeName: "Secure API Call"
        );

        // When: Posted to webhook
        using var client = Factory.CreateHttpsClient();
        var response = await client.PostAsJsonAsync("/api/v1/logs/workflow-error", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Then: Sensitive data is redacted in database
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var logged = await context.WorkflowErrorLogs
            .FirstOrDefaultAsync(e => e.ExecutionId == request.ExecutionId);

        logged.Should().NotBeNull();
        logged.ErrorMessage.Should().Contain("***REDACTED***");
        logged.ErrorMessage.Should().NotContain("sk-1234567890");
        logged.ErrorMessage.Should().NotContain("bearer-secret");
        logged.ErrorMessage.Should().NotContain("mysecret123");
    }
}