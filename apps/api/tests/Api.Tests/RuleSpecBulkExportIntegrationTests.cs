using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net;
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

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for RuleSpec bulk export endpoint.
///
/// Feature: Bulk RuleSpec Export (EDIT-07)
/// As an editor or admin
/// I want to export multiple rule specs as a ZIP file
/// So that I can back up or migrate game rules efficiently
/// </summary>
public class RuleSpecBulkExportIntegrationTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RuleSpecBulkExportIntegrationTests(WebApplicationFactoryFixture factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
    }

    /// <summary>
    /// Scenario: Editor exports a single rule spec
    ///   Given an editor user with one rule spec
    ///   When user requests export of that rule spec
    ///   Then a ZIP file is returned with valid content
    /// </summary>
    [Fact]
    public async Task BulkExport_WithSingleRuleSpec_ReturnsValidZip()
    {
        // Given: An editor user with one rule spec
        var user = await CreateTestUserAsync($"editor-{TestRunId}@example.com", UserRole.Editor);
        var game = await CreateTestGameAsync("chess-export");

        // Create rule spec with atoms manually
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var spec = new RuleSpecEntity
            {
                GameId = game.Id,
                Version = "v1",
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = user.Id
            };
            spec.Atoms.Add(new RuleAtomEntity
            {
                RuleSpec = spec,
                Key = "r1",
                Text = "Two players",
                Section = "Basics",
                SortOrder = 1
            });
            spec.Atoms.Add(new RuleAtomEntity
            {
                RuleSpec = spec,
                Key = "r2",
                Text = "White moves first",
                Section = "Basics",
                SortOrder = 2
            });
            db.RuleSpecs.Add(spec);
            await db.SaveChangesAsync();
            TrackRuleSpecId(spec.Id.ToString());
        }

        var cookies = await AuthenticateUserAsync(user.Email);
        using var client = Factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("Cookie", cookies);

        var request = new BulkExportRequest
        {
            RuleSpecIds = new List<string> { game.Id }
        };

        // When: User requests export of that rule spec
        var response = await client.PostAsJsonAsync("/api/v1/rulespecs/bulk/export", request);

        // Then: A ZIP file is returned with valid content
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/zip");

        var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        fileName.Should().NotBeNull();
        fileName.Should().Contain("meepleai-rulespecs-");
        fileName.Should().EndWith(".zip");

        var zipBytes = await response.Content.ReadAsByteArrayAsync();
        zipBytes.Length > 0.Should().BeTrue();

        // Verify ZIP structure
        using var memoryStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

        archive.Entries.Should().ContainSingle();
        var entry = archive.Entries[0];
        entry.Name.Should().Contain("chess-export");
        entry.Name.Should().EndWith(".json");

        // Verify JSON content
        using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream);
        var json = await reader.ReadToEndAsync();
        var exportedSpec = JsonSerializer.Deserialize<RuleSpec>(json, JsonOptions);

        exportedSpec.Should().NotBeNull();
        exportedSpec!.gameId.Should().Be(game.Id);
        exportedSpec.version.Should().Be("v1");
        exportedSpec.rules.Count.Should().Be(2);
    }

    /// <summary>
    /// Scenario: Admin exports multiple rule specs
    ///   Given an admin user with three rule specs
    ///   When user requests export of all three
    ///   Then a ZIP file with all three specs is returned
    /// </summary>
    [Fact]
    public async Task BulkExport_WithMultipleRuleSpecs_ReturnsZipWithAllSpecs()
    {
        // Given: An admin user with three rule specs
        var user = await CreateTestUserAsync($"admin-{TestRunId}@example.com", UserRole.Admin);
        var game1 = await CreateTestGameAsync("game1");
        var game2 = await CreateTestGameAsync("game2");
        var game3 = await CreateTestGameAsync("game3");

        // Create rule specs with atoms
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            var spec1 = new RuleSpecEntity { GameId = game1.Id, Version = "v1", CreatedAt = DateTime.UtcNow, CreatedByUserId = user.Id };
            spec1.Atoms.Add(new RuleAtomEntity { RuleSpec = spec1, Key = "r1", Text = "Rule 1", SortOrder = 1 });

            var spec2 = new RuleSpecEntity { GameId = game2.Id, Version = "v1", CreatedAt = DateTime.UtcNow, CreatedByUserId = user.Id };
            spec2.Atoms.Add(new RuleAtomEntity { RuleSpec = spec2, Key = "r1", Text = "Rule 2", SortOrder = 1 });

            var spec3 = new RuleSpecEntity { GameId = game3.Id, Version = "v1", CreatedAt = DateTime.UtcNow, CreatedByUserId = user.Id };
            spec3.Atoms.Add(new RuleAtomEntity { RuleSpec = spec3, Key = "r1", Text = "Rule 3", SortOrder = 1 });

            db.RuleSpecs.AddRange(spec1, spec2, spec3);
            await db.SaveChangesAsync();
            TrackRuleSpecId(spec1.Id.ToString());
            TrackRuleSpecId(spec2.Id.ToString());
            TrackRuleSpecId(spec3.Id.ToString());
        }

        var cookies = await AuthenticateUserAsync(user.Email);
        using var client = Factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("Cookie", cookies);

        var request = new BulkExportRequest
        {
            RuleSpecIds = new List<string> { game1.Id, game2.Id, game3.Id }
        };

        // When: User requests export of all three
        var response = await client.PostAsJsonAsync("/api/v1/rulespecs/bulk/export", request);

        // Then: A ZIP file with all three specs is returned
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var zipBytes = await response.Content.ReadAsByteArrayAsync();
        using var memoryStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

        archive.Entries.Count.Should().Be(3);
        e => e.Name.Contains("game1").Should().Contain(archive.Entries);
        e => e.Name.Contains("game2").Should().Contain(archive.Entries);
        e => e.Name.Contains("game3").Should().Contain(archive.Entries);
    }

    /// <summary>
    /// Scenario: Regular user attempts to export rule specs
    ///   Given a regular user without editor/admin role
    ///   When user requests export
    ///   Then request is forbidden
    /// </summary>
    [Fact]
    public async Task BulkExport_WithRegularUser_ReturnsForbidden()
    {
        // Given: A regular user without editor/admin role
        var user = await CreateTestUserAsync($"user-{TestRunId}@example.com", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        using var client = Factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("Cookie", cookies);

        var request = new BulkExportRequest
        {
            RuleSpecIds = new List<string> { "game1" }
        };

        // When: User requests export
        var response = await client.PostAsJsonAsync("/api/v1/rulespecs/bulk/export", request);

        // Then: Request is forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Scenario: Unauthenticated user attempts to export
    ///   Given an unauthenticated request
    ///   When requesting export
    ///   Then request is unauthorized
    /// </summary>
    [Fact]
    public async Task BulkExport_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Given: An unauthenticated request
        using var client = Factory.CreateHttpsClient();

        var request = new BulkExportRequest
        {
            RuleSpecIds = new List<string> { "game1" }
        };

        // When: Requesting export
        var response = await client.PostAsJsonAsync("/api/v1/rulespecs/bulk/export", request);

        // Then: Request is unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Scenario: Export with empty rule spec list
    ///   Given an editor user
    ///   When user requests export with empty list
    ///   Then request is rejected as bad request
    /// </summary>
    [Fact]
    public async Task BulkExport_WithEmptyRuleSpecIds_ReturnsBadRequest()
    {
        // Given: An editor user
        var user = await CreateTestUserAsync($"editor-empty-{TestRunId}@example.com", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(user.Email);

        using var client = Factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("Cookie", cookies);

        var request = new BulkExportRequest
        {
            RuleSpecIds = new List<string>()
        };

        // When: User requests export with empty list
        var response = await client.PostAsJsonAsync("/api/v1/rulespecs/bulk/export", request);

        // Then: Request is rejected as bad request
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Scenario: Export with non-existent rule spec IDs
    ///   Given an editor user
    ///   When user requests export with non-existent IDs
    ///   Then request returns internal server error
    /// </summary>
    [Fact]
    public async Task BulkExport_WithNonExistentIds_ReturnsError()
    {
        // Given: An editor user
        var user = await CreateTestUserAsync($"editor-missing-{TestRunId}@example.com", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(user.Email);

        using var client = Factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("Cookie", cookies);

        var request = new BulkExportRequest
        {
            RuleSpecIds = new List<string> { "non-existent-game-id" }
        };

        // When: User requests export with non-existent IDs
        var response = await client.PostAsJsonAsync("/api/v1/rulespecs/bulk/export", request);

        // Then: Request returns internal server error
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

}
