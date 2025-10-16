using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Unit tests for PromptManagementService using SQLite in-memory database.
/// Tests cover all CRUD operations, version management, rollback, audit logging, and edge cases.
/// </summary>
public class PromptManagementServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly PromptManagementService _service;
    private readonly string _testUserId = "test-user-123";

    public PromptManagementServiceTests()
    {
        // Create in-memory SQLite database
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new MeepleAiDbContext(options);
        _db.Database.EnsureCreated();

        // Seed test user
        var testUser = new UserEntity
        {
            Id = _testUserId,
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "fake-hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(testUser);
        _db.SaveChanges();

        _timeProvider = TimeProvider.System;
        _service = new PromptManagementService(_db, NullLogger<PromptManagementService>.Instance, _timeProvider);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreatePromptTemplateAsync_ValidRequest_CreatesTemplateAndInitialVersion()
    {
        // Arrange
        var request = new CreatePromptTemplateRequest
        {
            Name = "test-template",
            Description = "Test description",
            Category = "test",
            InitialContent = "Initial prompt content",
            Metadata = JsonSerializer.Serialize(new { model = "gpt-4" })
        };

        // Act
        var response = await _service.CreatePromptTemplateAsync(request, _testUserId);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Template);
        Assert.NotNull(response.InitialVersion);
        Assert.Equal("test-template", response.Template.Name);
        Assert.Equal("Test description", response.Template.Description);
        Assert.Equal("test", response.Template.Category);
        Assert.Equal(1, response.Template.VersionCount);
        Assert.Equal(1, response.Template.ActiveVersionNumber);
        Assert.Equal(1, response.InitialVersion.VersionNumber);
        Assert.Equal("Initial prompt content", response.InitialVersion.Content);
        Assert.True(response.InitialVersion.IsActive);

        // Verify database state
        var template = await _db.PromptTemplates.Include(t => t.Versions).FirstAsync(t => t.Id == response.Template.Id);
        Assert.Single(template.Versions);
        Assert.True(template.Versions.First().IsActive);

        // Verify audit logs
        var auditLogs = await _db.PromptAuditLogs.Where(a => a.TemplateId == response.Template.Id).ToListAsync();
        Assert.Equal(2, auditLogs.Count); // template_created + version_created
        Assert.Contains(auditLogs, a => a.Action == "template_created");
        Assert.Contains(auditLogs, a => a.Action == "version_created");
    }

    [Fact]
    public async Task CreatePromptTemplateAsync_DuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        var request1 = new CreatePromptTemplateRequest
        {
            Name = "duplicate-template",
            InitialContent = "Content 1"
        };

        var request2 = new CreatePromptTemplateRequest
        {
            Name = "duplicate-template",
            InitialContent = "Content 2"
        };

        await _service.CreatePromptTemplateAsync(request1, _testUserId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreatePromptTemplateAsync(request2, _testUserId));

        Assert.Contains("already exists", exception.Message);
    }

    [Theory]
    [InlineData(null, "Content")]
    [InlineData("", "Content")]
    [InlineData("  ", "Content")]
    [InlineData("Name", null)]
    [InlineData("Name", "")]
    [InlineData("Name", "  ")]
    public async Task CreatePromptTemplateAsync_InvalidInput_ThrowsArgumentException(string? name, string? content)
    {
        // Arrange
        var request = new CreatePromptTemplateRequest
        {
            Name = name!,
            InitialContent = content!
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreatePromptTemplateAsync(request, _testUserId));
    }

    [Fact]
    public async Task CreatePromptVersionAsync_ValidRequest_CreatesNewVersion()
    {
        // Arrange
        var createTemplateRequest = new CreatePromptTemplateRequest
        {
            Name = "version-test-template",
            InitialContent = "Version 1 content"
        };

        var templateResponse = await _service.CreatePromptTemplateAsync(createTemplateRequest, _testUserId);

        // Wait a moment for distinct timestamps
        await Task.Delay(10);

        var createVersionRequest = new CreatePromptVersionRequest
        {
            Content = "Version 2 content",
            Metadata = JsonSerializer.Serialize(new { improved = true }),
            ActivateImmediately = false
        };

        // Act
        var version = await _service.CreatePromptVersionAsync(
            templateResponse.Template.Id,
            createVersionRequest,
            _testUserId);

        // Assert
        Assert.NotNull(version);
        Assert.Equal(2, version.VersionNumber);
        Assert.Equal("Version 2 content", version.Content);
        Assert.False(version.IsActive); // Not activated immediately
        Assert.NotNull(version.Metadata);

        // Verify version 1 is still active
        var version1 = await _service.GetVersionAsync(templateResponse.Template.Id, 1);
        Assert.NotNull(version1);
        Assert.True(version1.IsActive);

        // Verify audit log
        var auditLogs = await _db.PromptAuditLogs
            .Where(a => a.VersionId == version.Id && a.Action == "version_created")
            .ToListAsync();
        Assert.Single(auditLogs);
    }

    [Fact]
    public async Task CreatePromptVersionAsync_ActivateImmediately_DeactivatesOtherVersions()
    {
        // Arrange
        var createTemplateRequest = new CreatePromptTemplateRequest
        {
            Name = "activate-test-template",
            InitialContent = "Version 1 content"
        };

        var templateResponse = await _service.CreatePromptTemplateAsync(createTemplateRequest, _testUserId);

        var createVersionRequest = new CreatePromptVersionRequest
        {
            Content = "Version 2 content",
            ActivateImmediately = true
        };

        // Act
        var version2 = await _service.CreatePromptVersionAsync(
            templateResponse.Template.Id,
            createVersionRequest,
            _testUserId);

        // Assert
        Assert.True(version2.IsActive);

        // Verify version 1 is now inactive
        var version1 = await _service.GetVersionAsync(templateResponse.Template.Id, 1);
        Assert.NotNull(version1);
        Assert.False(version1.IsActive);

        // Verify audit logs include activation and deactivation
        var auditLogs = await _db.PromptAuditLogs
            .Where(a => a.TemplateId == templateResponse.Template.Id)
            .OrderBy(a => a.ChangedAt)
            .ToListAsync();

        Assert.Contains(auditLogs, a => a.Action == "version_deactivated" && a.VersionId == templateResponse.InitialVersion.Id);
        Assert.Contains(auditLogs, a => a.Action == "version_activated" && a.VersionId == version2.Id);
    }

    [Fact]
    public async Task CreatePromptVersionAsync_NonExistentTemplate_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new CreatePromptVersionRequest
        {
            Content = "Test content"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreatePromptVersionAsync("non-existent-id", request, _testUserId));

        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task GetActiveVersionAsync_ValidTemplateName_ReturnsActiveVersion()
    {
        // Arrange
        var createTemplateRequest = new CreatePromptTemplateRequest
        {
            Name = "active-version-test",
            InitialContent = "Active content"
        };

        var templateResponse = await _service.CreatePromptTemplateAsync(createTemplateRequest, _testUserId);

        // Act
        var activeVersion = await _service.GetActiveVersionAsync("active-version-test");

        // Assert
        Assert.NotNull(activeVersion);
        Assert.Equal(templateResponse.InitialVersion.Id, activeVersion.Id);
        Assert.Equal(1, activeVersion.VersionNumber);
        Assert.True(activeVersion.IsActive);
    }

    [Fact]
    public async Task GetActiveVersionAsync_NoActiveVersion_ReturnsNull()
    {
        // Arrange
        var createTemplateRequest = new CreatePromptTemplateRequest
        {
            Name = "no-active-version-test",
            InitialContent = "Content"
        };

        var templateResponse = await _service.CreatePromptTemplateAsync(createTemplateRequest, _testUserId);

        // Manually deactivate the version
        var version = await _db.PromptVersions.FirstAsync(v => v.Id == templateResponse.InitialVersion.Id);
        version.IsActive = false;
        await _db.SaveChangesAsync();

        // Act
        var activeVersion = await _service.GetActiveVersionAsync("no-active-version-test");

        // Assert
        Assert.Null(activeVersion);
    }

    [Fact]
    public async Task GetActiveVersionAsync_NonExistentTemplate_ReturnsNull()
    {
        // Act
        var activeVersion = await _service.GetActiveVersionAsync("non-existent-template");

        // Assert
        Assert.Null(activeVersion);
    }

    [Fact]
    public async Task ActivateVersionAsync_ValidVersion_ActivatesAndDeactivatesOthers()
    {
        // Arrange
        var createTemplateRequest = new CreatePromptTemplateRequest
        {
            Name = "rollback-test-template",
            InitialContent = "Version 1 content"
        };

        var templateResponse = await _service.CreatePromptTemplateAsync(createTemplateRequest, _testUserId);

        // Create version 2 and activate it
        var createVersion2Request = new CreatePromptVersionRequest
        {
            Content = "Version 2 content",
            ActivateImmediately = true
        };

        var version2 = await _service.CreatePromptVersionAsync(
            templateResponse.Template.Id,
            createVersion2Request,
            _testUserId);

        // Create version 3 and activate it
        var createVersion3Request = new CreatePromptVersionRequest
        {
            Content = "Version 3 content",
            ActivateImmediately = true
        };

        var version3 = await _service.CreatePromptVersionAsync(
            templateResponse.Template.Id,
            createVersion3Request,
            _testUserId);

        // Wait a moment for distinct timestamps
        await Task.Delay(10);

        // Act - Rollback to version 2
        var activatedVersion = await _service.ActivateVersionAsync(
            templateResponse.Template.Id,
            version2.Id,
            _testUserId,
            "Rollback due to issues with version 3");

        // Assert
        Assert.NotNull(activatedVersion);
        Assert.Equal(2, activatedVersion.VersionNumber);
        Assert.True(activatedVersion.IsActive);

        // Verify version 3 is now inactive
        var inactiveVersion3 = await _service.GetVersionAsync(templateResponse.Template.Id, 3);
        Assert.NotNull(inactiveVersion3);
        Assert.False(inactiveVersion3.IsActive);

        // Verify audit log for rollback
        var rollbackAuditLog = await _db.PromptAuditLogs
            .Where(a => a.VersionId == version2.Id && a.Action == "version_activated")
            .OrderByDescending(a => a.ChangedAt)
            .FirstAsync();

        Assert.Contains("Rollback", rollbackAuditLog.Details!);
    }

    [Fact]
    public async Task ActivateVersionAsync_AlreadyActive_ReturnsWithoutChanges()
    {
        // Arrange
        var createTemplateRequest = new CreatePromptTemplateRequest
        {
            Name = "already-active-test",
            InitialContent = "Content"
        };

        var templateResponse = await _service.CreatePromptTemplateAsync(createTemplateRequest, _testUserId);

        var initialAuditLogCount = await _db.PromptAuditLogs.CountAsync();

        // Act
        var result = await _service.ActivateVersionAsync(
            templateResponse.Template.Id,
            templateResponse.InitialVersion.Id,
            _testUserId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsActive);

        // No new audit logs should be created
        var finalAuditLogCount = await _db.PromptAuditLogs.CountAsync();
        Assert.Equal(initialAuditLogCount, finalAuditLogCount);
    }

    [Fact]
    public async Task ActivateVersionAsync_NonExistentVersion_ThrowsInvalidOperationException()
    {
        // Arrange
        var createTemplateRequest = new CreatePromptTemplateRequest
        {
            Name = "test-template",
            InitialContent = "Content"
        };

        var templateResponse = await _service.CreatePromptTemplateAsync(createTemplateRequest, _testUserId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ActivateVersionAsync(templateResponse.Template.Id, "non-existent-version-id", _testUserId));

        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task GetVersionHistoryAsync_MultipleVersions_ReturnsOrderedHistory()
    {
        // Arrange
        var createTemplateRequest = new CreatePromptTemplateRequest
        {
            Name = "history-test-template",
            InitialContent = "Version 1 content"
        };

        var templateResponse = await _service.CreatePromptTemplateAsync(createTemplateRequest, _testUserId);

        // Create versions 2 and 3
        await _service.CreatePromptVersionAsync(
            templateResponse.Template.Id,
            new CreatePromptVersionRequest { Content = "Version 2 content" },
            _testUserId);

        await _service.CreatePromptVersionAsync(
            templateResponse.Template.Id,
            new CreatePromptVersionRequest { Content = "Version 3 content" },
            _testUserId);

        // Act
        var history = await _service.GetVersionHistoryAsync(templateResponse.Template.Id);

        // Assert
        Assert.NotNull(history);
        Assert.Equal("history-test-template", history.Template.Name);
        Assert.Equal(3, history.TotalCount);
        Assert.Equal(3, history.Versions.Count);

        // Verify descending order
        Assert.Equal(3, history.Versions[0].VersionNumber);
        Assert.Equal(2, history.Versions[1].VersionNumber);
        Assert.Equal(1, history.Versions[2].VersionNumber);
    }

    [Fact]
    public async Task GetVersionHistoryAsync_NonExistentTemplate_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GetVersionHistoryAsync("non-existent-id"));
    }

    [Fact]
    public async Task GetAuditLogAsync_MultipleActions_ReturnsOrderedAuditLog()
    {
        // Arrange
        var createTemplateRequest = new CreatePromptTemplateRequest
        {
            Name = "audit-test-template",
            InitialContent = "Version 1 content"
        };

        var templateResponse = await _service.CreatePromptTemplateAsync(createTemplateRequest, _testUserId);

        // Create and activate version 2
        var version2 = await _service.CreatePromptVersionAsync(
            templateResponse.Template.Id,
            new CreatePromptVersionRequest { Content = "Version 2 content", ActivateImmediately = true },
            _testUserId);

        // Act
        var auditLog = await _service.GetAuditLogAsync(templateResponse.Template.Id);

        // Assert
        Assert.NotNull(auditLog);
        Assert.Equal("audit-test-template", auditLog.Template.Name);
        Assert.True(auditLog.TotalCount >= 5); // template_created, version_created (v1), version_created (v2), version_deactivated (v1), version_activated (v2)
        Assert.True(auditLog.Logs.Count >= 5);

        // Verify logs are ordered by timestamp descending (most recent first)
        for (int i = 0; i < auditLog.Logs.Count - 1; i++)
        {
            Assert.True(auditLog.Logs[i].ChangedAt >= auditLog.Logs[i + 1].ChangedAt);
        }

        // Verify action types
        var actions = auditLog.Logs.Select(l => l.Action).ToList();
        Assert.Contains("template_created", actions);
        Assert.Contains("version_created", actions);
        Assert.Contains("version_activated", actions);
        Assert.Contains("version_deactivated", actions);
    }

    [Fact]
    public async Task GetAuditLogAsync_WithLimit_ReturnsLimitedResults()
    {
        // Arrange
        var createTemplateRequest = new CreatePromptTemplateRequest
        {
            Name = "audit-limit-test",
            InitialContent = "Content"
        };

        var templateResponse = await _service.CreatePromptTemplateAsync(createTemplateRequest, _testUserId);

        // Create multiple versions to generate audit logs
        for (int i = 2; i <= 10; i++)
        {
            await _service.CreatePromptVersionAsync(
                templateResponse.Template.Id,
                new CreatePromptVersionRequest { Content = $"Version {i} content" },
                _testUserId);
        }

        // Act
        var auditLog = await _service.GetAuditLogAsync(templateResponse.Template.Id, limit: 5);

        // Assert
        Assert.Equal(5, auditLog.Logs.Count);
        Assert.True(auditLog.TotalCount > 5); // Total is more than limit
    }

    [Fact]
    public async Task ListTemplatesAsync_NoFilter_ReturnsAllTemplates()
    {
        // Arrange
        await _service.CreatePromptTemplateAsync(
            new CreatePromptTemplateRequest { Name = "template-1", InitialContent = "Content 1" },
            _testUserId);

        await _service.CreatePromptTemplateAsync(
            new CreatePromptTemplateRequest { Name = "template-2", InitialContent = "Content 2" },
            _testUserId);

        // Act
        var result = await _service.ListTemplatesAsync();

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Templates.Count);
    }

    [Fact]
    public async Task ListTemplatesAsync_WithCategoryFilter_ReturnsFilteredTemplates()
    {
        // Arrange
        await _service.CreatePromptTemplateAsync(
            new CreatePromptTemplateRequest { Name = "qa-template", Category = "qa", InitialContent = "QA Content" },
            _testUserId);

        await _service.CreatePromptTemplateAsync(
            new CreatePromptTemplateRequest { Name = "explain-template", Category = "explain", InitialContent = "Explain Content" },
            _testUserId);

        await _service.CreatePromptTemplateAsync(
            new CreatePromptTemplateRequest { Name = "qa-template-2", Category = "qa", InitialContent = "QA Content 2" },
            _testUserId);

        // Act
        var result = await _service.ListTemplatesAsync(category: "qa");

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Templates.Count);
        Assert.All(result.Templates, t => Assert.Equal("qa", t.Category));
    }

    [Fact]
    public async Task GetTemplateAsync_ExistingTemplate_ReturnsTemplate()
    {
        // Arrange
        var createRequest = new CreatePromptTemplateRequest
        {
            Name = "get-template-test",
            Description = "Test description",
            InitialContent = "Content"
        };

        var created = await _service.CreatePromptTemplateAsync(createRequest, _testUserId);

        // Act
        var template = await _service.GetTemplateAsync(created.Template.Id);

        // Assert
        Assert.NotNull(template);
        Assert.Equal(created.Template.Id, template.Id);
        Assert.Equal("get-template-test", template.Name);
        Assert.Equal("Test description", template.Description);
    }

    [Fact]
    public async Task GetTemplateAsync_NonExistentTemplate_ReturnsNull()
    {
        // Act
        var template = await _service.GetTemplateAsync("non-existent-id");

        // Assert
        Assert.Null(template);
    }

    [Fact]
    public async Task ConcurrentActivation_MultipleVersions_MaintainsConsistency()
    {
        // Arrange
        var createTemplateRequest = new CreatePromptTemplateRequest
        {
            Name = "concurrent-test-template",
            InitialContent = "Version 1"
        };

        var templateResponse = await _service.CreatePromptTemplateAsync(createTemplateRequest, _testUserId);

        var version2 = await _service.CreatePromptVersionAsync(
            templateResponse.Template.Id,
            new CreatePromptVersionRequest { Content = "Version 2" },
            _testUserId);

        var version3 = await _service.CreatePromptVersionAsync(
            templateResponse.Template.Id,
            new CreatePromptVersionRequest { Content = "Version 3" },
            _testUserId);

        // Act - Simulate concurrent activation attempts (sequential in test, but tests transaction logic)
        await _service.ActivateVersionAsync(templateResponse.Template.Id, version2.Id, _testUserId);
        await _service.ActivateVersionAsync(templateResponse.Template.Id, version3.Id, _testUserId);

        // Assert - Only one version should be active
        var allVersions = await _db.PromptVersions
            .Where(v => v.TemplateId == templateResponse.Template.Id)
            .ToListAsync();

        var activeVersions = allVersions.Where(v => v.IsActive).ToList();
        Assert.Single(activeVersions);
        Assert.Equal(3, activeVersions[0].VersionNumber);
    }

    [Fact]
    public async Task VersionSequenceNumbers_AfterMultipleCreations_AreSequential()
    {
        // Arrange
        var createTemplateRequest = new CreatePromptTemplateRequest
        {
            Name = "sequence-test-template",
            InitialContent = "Version 1"
        };

        var templateResponse = await _service.CreatePromptTemplateAsync(createTemplateRequest, _testUserId);

        // Act - Create versions 2 through 5
        var versions = new List<PromptVersionDto> { templateResponse.InitialVersion };
        for (int i = 2; i <= 5; i++)
        {
            var version = await _service.CreatePromptVersionAsync(
                templateResponse.Template.Id,
                new CreatePromptVersionRequest { Content = $"Version {i}" },
                _testUserId);
            versions.Add(version);
        }

        // Assert
        Assert.Equal(5, versions.Count);
        for (int i = 0; i < versions.Count; i++)
        {
            Assert.Equal(i + 1, versions[i].VersionNumber);
        }
    }

    [Fact]
    public async Task AuditLog_RecordsUserInformation_Correctly()
    {
        // Arrange
        var createTemplateRequest = new CreatePromptTemplateRequest
        {
            Name = "audit-user-test",
            InitialContent = "Content"
        };

        var templateResponse = await _service.CreatePromptTemplateAsync(createTemplateRequest, _testUserId);

        // Act
        var auditLog = await _service.GetAuditLogAsync(templateResponse.Template.Id);

        // Assert
        Assert.All(auditLog.Logs, log =>
        {
            Assert.Equal(_testUserId, log.ChangedByUserId);
            Assert.Equal("test@example.com", log.ChangedByEmail);
        });
    }

    [Fact]
    public async Task VersionMetadata_PersistsCorrectly()
    {
        // Arrange
        var metadata = JsonSerializer.Serialize(new
        {
            model = "gpt-4-turbo",
            temperature = 0.7,
            maxTokens = 1000,
            tags = new[] { "production", "optimized" }
        });

        var createTemplateRequest = new CreatePromptTemplateRequest
        {
            Name = "metadata-test-template",
            InitialContent = "Content",
            Metadata = metadata
        };

        var templateResponse = await _service.CreatePromptTemplateAsync(createTemplateRequest, _testUserId);

        // Act
        var version = await _service.GetVersionAsync(templateResponse.Template.Id, 1);

        // Assert
        Assert.NotNull(version);
        Assert.NotNull(version.Metadata);
        Assert.Equal(metadata, version.Metadata);

        // Verify deserialization works
        var deserializedMetadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(version.Metadata);
        Assert.NotNull(deserializedMetadata);
        Assert.Equal("gpt-4-turbo", deserializedMetadata["model"].GetString());
    }
}
