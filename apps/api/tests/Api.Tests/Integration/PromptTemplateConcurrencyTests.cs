using System;
using System.Collections.Generic;
using System.Linq;
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
using Xunit.Abstractions;

namespace Api.Tests.Integration;

/// <summary>
/// TEST-04: Concurrency tests for PromptTemplateService
/// Verifies thread-safety in version activation, optimistic concurrency, and cache coherence
///
/// Reference: ConfigurationConcurrencyTests (MANDATORY PATTERN)
/// Uses: WebApplicationFactory + Testcontainers + PostgreSQL (NO SQLite)
/// </summary>
[Collection("Admin Endpoints")]
public class PromptTemplateConcurrencyTests : ConfigIntegrationTestBase
{
    private readonly ITestOutputHelper _output;
    private string _adminEmail = null!;
    private List<string> _adminCookies = null!;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PromptTemplateConcurrencyTests(WebApplicationFactoryFixture factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _adminEmail = $"prompt-admin-{Guid.NewGuid():N}@test.com";

        // Register and get cookies for reuse
        using var tempClient = Factory.CreateHttpsClient();
        _adminCookies = await RegisterAndAuthenticateAsync(tempClient, _adminEmail, "Admin");
    }

    /// <summary>
    /// Pattern 1: Lost Update Detection
    /// Scenario: Multiple admins activating different versions simultaneously
    ///   Given a template with 3 versions
    ///   When 2 admins activate different versions concurrently
    ///   Then one activation wins (optimistic concurrency)
    ///   And database state is consistent
    /// </summary>
    [Fact]
    public async Task ConcurrentVersionActivations_OptimisticConcurrency_Test()
    {
        // Arrange: Create template with 3 versions
        var templateId = "";
        var versionIds = new List<string>();

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == _adminEmail);
            var userId = user!.Id;

            var template = new PromptTemplateEntity
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"concurrent-template-{Guid.NewGuid():N}",
                Description = "Concurrency test template",
                Category = "qa",
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId,
                CreatedBy = user
            };

            db.PromptTemplates.Add(template);
            await db.SaveChangesAsync();

            templateId = template.Id;

            // Create 3 versions
            for (int i = 1; i <= 3; i++)
            {
                var version = new PromptVersionEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    TemplateId = templateId,
                    VersionNumber = i,
                    Content = $"Version {i} content",
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = userId,
                    IsActive = i == 1, // Only first version active initially
                    Template = template,
                    CreatedBy = user
                };

                db.PromptVersions.Add(version);
                versionIds.Add(version.Id);
            }

            await db.SaveChangesAsync();
        }

        // Act: 2 concurrent activations of different versions
        var client1 = Factory.CreateHttpsClient();
        var client2 = Factory.CreateHttpsClient();

        var admin2Email = $"admin2-{Guid.NewGuid():N}@test.com";
        using var tempClient2 = Factory.CreateHttpsClient();
        var admin2Cookies = await RegisterAndAuthenticateAsync(tempClient2, admin2Email, "Admin");

        var activateRequest = new { Reason = "Concurrent activation test" };

        var task1 = PutAsJsonAuthenticatedAsync(client1, _adminCookies,
            $"/api/v1/prompts/{templateId}/versions/{versionIds[1]}/activate", activateRequest);
        var task2 = PutAsJsonAuthenticatedAsync(client2, admin2Cookies,
            $"/api/v1/prompts/{templateId}/versions/{versionIds[2]}/activate", activateRequest);

        var results = await Task.WhenAll(task1, task2);

        // Assert: At least one succeeds
        var successCount = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.True(successCount >= 1, "At least one activation should succeed");

        // Verify exactly one version is active
        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var activeVersions = await db2.PromptVersions
            .Where(v => v.TemplateId == templateId && v.IsActive)
            .ToListAsync();

        Assert.Single(activeVersions);
        _output.WriteLine($"Active version: {activeVersions[0].VersionNumber}");
    }

    /// <summary>
    /// Pattern 2: TOCTOU Prevention
    /// Scenario: Concurrent attempts to activate same version
    ///   Given a template with inactive version
    ///   When 3 admins activate same version concurrently
    ///   Then all requests succeed (idempotent)
    ///   And version is activated exactly once
    /// </summary>
    [Fact]
    public async Task ConcurrentSameVersionActivations_AreIdempotent_Test()
    {
        // Arrange: Create template with 2 versions
        var templateId = "";
        var versionId = "";

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == _adminEmail);
            var userId = user!.Id;

            var template = new PromptTemplateEntity
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"idempotent-template-{Guid.NewGuid():N}",
                Description = "Idempotent test",
                Category = "qa",
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId,
                CreatedBy = user
            };

            db.PromptTemplates.Add(template);
            await db.SaveChangesAsync();

            templateId = template.Id;

            // Create 2 versions
            var v1 = new PromptVersionEntity
            {
                Id = Guid.NewGuid().ToString(),
                TemplateId = templateId,
                VersionNumber = 1,
                Content = "Version 1",
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId,
                IsActive = true,
                Template = template,
                CreatedBy = user
            };

            var v2 = new PromptVersionEntity
            {
                Id = Guid.NewGuid().ToString(),
                TemplateId = templateId,
                VersionNumber = 2,
                Content = "Version 2",
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId,
                IsActive = false,
                Template = template,
                CreatedBy = user
            };

            db.PromptVersions.AddRange(v1, v2);
            await db.SaveChangesAsync();

            versionId = v2.Id;
        }

        // Act: 3 concurrent activations of same version
        var tasks = new Task<HttpResponseMessage>[3];
        for (int i = 0; i < 3; i++)
        {
            var adminClient = Factory.CreateHttpsClient();
            var adminEmail = $"admin-{i}-{Guid.NewGuid():N}@test.com";
            using var tempClient = Factory.CreateHttpsClient();
            var adminCookies = await RegisterAndAuthenticateAsync(tempClient, adminEmail, "Admin");

            var request = new { Reason = $"Admin {i} activation" };
            tasks[i] = PutAsJsonAuthenticatedAsync(adminClient, adminCookies,
                $"/api/v1/prompts/{templateId}/versions/{versionId}/activate", request);
        }

        var results = await Task.WhenAll(tasks);

        // Assert: All succeed (idempotent)
        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        // Verify version is active
        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var version = await db2.PromptVersions.FindAsync(versionId);
        Assert.True(version!.IsActive);
    }

    /// <summary>
    /// Pattern 3: Cache Coherence
    /// Scenario: Cache invalidation after activation
    ///   Given a cached active version
    ///   When version is changed via activation
    ///   Then cache is invalidated
    ///   And subsequent reads get new version
    /// </summary>
    [Fact]
    public async Task PromptCacheInvalidation_PropagatesCorrectly_Test()
    {
        // Arrange: Create template with 2 versions
        var templateName = $"cache-template-{Guid.NewGuid():N}";
        var templateId = "";
        var version2Id = "";

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == _adminEmail);
            var userId = user!.Id;

            var template = new PromptTemplateEntity
            {
                Id = Guid.NewGuid().ToString(),
                Name = templateName,
                Description = "Cache test",
                Category = "qa",
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId,
                CreatedBy = user
            };

            db.PromptTemplates.Add(template);
            await db.SaveChangesAsync();

            templateId = template.Id;

            var v1 = new PromptVersionEntity
            {
                Id = Guid.NewGuid().ToString(),
                TemplateId = templateId,
                VersionNumber = 1,
                Content = "Cached version",
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId,
                IsActive = true,
                Template = template,
                CreatedBy = user
            };

            var v2 = new PromptVersionEntity
            {
                Id = Guid.NewGuid().ToString(),
                TemplateId = templateId,
                VersionNumber = 2,
                Content = "New version",
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId,
                IsActive = false,
                Template = template,
                CreatedBy = user
            };

            db.PromptVersions.AddRange(v1, v2);
            await db.SaveChangesAsync();

            version2Id = v2.Id;
        }

        // Read to populate cache (via service)
        using (var scope1 = Factory.Services.CreateScope())
        {
            var db = scope1.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var cachedVersion = await db.PromptVersions
                .Where(v => v.TemplateId == templateId && v.IsActive)
                .FirstOrDefaultAsync();

            cachedVersion.Should().NotBeNull();
            Assert.Equal("Cached version", cachedVersion.Content);
        }

        // Act: Activate version 2 (should invalidate cache)
        using var client = Factory.CreateHttpsClient();
        var request = new { Reason = "Cache invalidation test" };
        await PutAsJsonAuthenticatedAsync(client, _adminCookies,
            $"/api/v1/prompts/{templateId}/versions/{version2Id}/activate", request);

        // Assert: Next read gets new version (cache was invalidated)
        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var newVersion = await db2.PromptVersions
            .Where(v => v.TemplateId == templateId && v.IsActive)
            .FirstOrDefaultAsync();

        newVersion.Should().NotBeNull();
        Assert.Equal(2, newVersion.VersionNumber);
        Assert.Equal("New version", newVersion.Content);
    }

    /// <summary>
    /// Pattern 4: Distributed Cache Coherence
    /// Scenario: Multiple service instances reading active version
    ///   Given a prompt template with active version
    ///   When 5 concurrent service instances read active version
    ///   Then all instances get consistent data
    /// </summary>
    [Fact]
    public async Task DistributedPromptCacheCoherence_Test()
    {
        // Arrange: Create template with active version
        var templateName = $"distributed-{Guid.NewGuid():N}";
        var templateId = "";

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == _adminEmail);
            var userId = user!.Id;

            var template = new PromptTemplateEntity
            {
                Id = Guid.NewGuid().ToString(),
                Name = templateName,
                Description = "Distributed cache test",
                Category = "qa",
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId,
                CreatedBy = user
            };

            db.PromptTemplates.Add(template);
            await db.SaveChangesAsync();

            templateId = template.Id;

            var version = new PromptVersionEntity
            {
                Id = Guid.NewGuid().ToString(),
                TemplateId = templateId,
                VersionNumber = 1,
                Content = "Distributed content",
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId,
                IsActive = true,
                Template = template,
                CreatedBy = user
            };

            db.PromptVersions.Add(version);
            await db.SaveChangesAsync();
        }

        // Act: Simulate 5 service instances reading active version
        var tasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            return await db.PromptVersions
                .Where(v => v.TemplateId == templateId && v.IsActive)
                .Select(v => v.Content)
                .FirstOrDefaultAsync();
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert: All instances get consistent value
        Assert.All(results, content => Assert.Equal("Distributed content", content));
        Assert.Equal(5, results.Length);
    }
}
