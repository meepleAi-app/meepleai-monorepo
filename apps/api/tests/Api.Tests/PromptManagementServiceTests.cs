using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// Unit tests for PromptManagementService using SQLite in-memory database.
/// Tests cover all CRUD operations, version management, rollback, audit logging, and edge cases.
/// </summary>
public class PromptManagementServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly PromptManagementService _service;
    private readonly string _testUserId = "test-user-123";

    public PromptManagementServiceTests(ITestOutputHelper output)
    {
        _output = output;
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
        response.Should().NotBeNull();
        response.Template.Should().NotBeNull();
        response.InitialVersion.Should().NotBeNull();
        response.Template.Name.Should().Be("test-template");
        response.Template.Description.Should().Be("Test description");
        response.Template.Category.Should().Be("test");
        response.Template.VersionCount.Should().Be(1);
        response.Template.ActiveVersionNumber.Should().Be(1);
        response.InitialVersion.VersionNumber.Should().Be(1);
        response.InitialVersion.Content.Should().Be("Initial prompt content");
        response.InitialVersion.IsActive.Should().BeTrue();

        // Verify database state
        var template = await _db.PromptTemplates.Include(t => t.Versions).FirstAsync(t => t.Id == response.Template.Id);
        template.Versions.Should().ContainSingle();
        template.Versions.First().IsActive.Should().BeTrue();

        // Verify audit logs
        var auditLogs = await _db.PromptAuditLogs.Where(a => a.TemplateId == response.Template.Id).ToListAsync();
        auditLogs.Count.Should().Be(2); // template_created + version_created
        a => a.Action == "template_created".Should().Contain(auditLogs);
        a => a.Action == "version_created".Should().Contain(auditLogs);
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
        var act = async () => await _service.CreatePromptTemplateAsync(request2, _testUserId);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>().Subject;
        exception.Which.Message.Should().Contain("already exists");
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
        var act = async () => await _service.CreatePromptTemplateAsync(request, _testUserId);
        await act.Should().ThrowAsync<ArgumentException>();
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
        version.Should().NotBeNull();
        version.VersionNumber.Should().Be(2);
        version.Content.Should().Be("Version 2 content");
        version.IsActive.Should().BeFalse(); // Not activated immediately
        version.Metadata.Should().NotBeNull();

        // Verify version 1 is still active
        var version1 = await _service.GetVersionAsync(templateResponse.Template.Id, 1);
        version1.Should().NotBeNull();
        version1.IsActive.Should().BeTrue();

        // Verify audit log
        var auditLogs = await _db.PromptAuditLogs
            .Where(a => a.VersionId == version.Id && a.Action == "version_created")
            .ToListAsync();
        auditLogs.Should().ContainSingle();
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
        version2.IsActive.Should().BeTrue();

        // Verify version 1 is now inactive
        var version1 = await _service.GetVersionAsync(templateResponse.Template.Id, 1);
        version1.Should().NotBeNull();
        version1.IsActive.Should().BeFalse();

        // Verify audit logs include activation and deactivation
        var auditLogs = await _db.PromptAuditLogs
            .Where(a => a.TemplateId == templateResponse.Template.Id)
            .OrderBy(a => a.ChangedAt)
            .ToListAsync();

        a => a.Action == "version_deactivated" && a.VersionId == templateResponse.InitialVersion.Id.Should().Contain(auditLogs);
        a => a.Action == "version_activated" && a.VersionId == version2.Id.Should().Contain(auditLogs);
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
        var act = async () => await _service.CreatePromptVersionAsync("non-existent-id", request, _testUserId);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>().Subject;
        exception.Which.Message.Should().Contain("not found");
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
        activeVersion.Should().NotBeNull();
        activeVersion.Id.Should().Be(templateResponse.InitialVersion.Id);
        activeVersion.VersionNumber.Should().Be(1);
        activeVersion.IsActive.Should().BeTrue();
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
        activeVersion.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveVersionAsync_NonExistentTemplate_ReturnsNull()
    {
        // Act
        var activeVersion = await _service.GetActiveVersionAsync("non-existent-template");

        // Assert
        activeVersion.Should().BeNull();
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
        activatedVersion.Should().NotBeNull();
        activatedVersion.VersionNumber.Should().Be(2);
        activatedVersion.IsActive.Should().BeTrue();

        // Verify version 3 is now inactive
        var inactiveVersion3 = await _service.GetVersionAsync(templateResponse.Template.Id, 3);
        inactiveVersion3.Should().NotBeNull();
        inactiveVersion3.IsActive.Should().BeFalse();

        // Verify audit log for rollback
        var rollbackAuditLog = await _db.PromptAuditLogs
            .Where(a => a.VersionId == version2.Id && a.Action == "version_activated")
            .OrderByDescending(a => a.ChangedAt)
            .FirstAsync();

        rollbackAuditLog.Details!.Should().Contain("Rollback");
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
        result.Should().NotBeNull();
        result.IsActive.Should().BeTrue();

        // No new audit logs should be created
        var finalAuditLogCount = await _db.PromptAuditLogs.CountAsync();
        finalAuditLogCount.Should().Be(initialAuditLogCount);
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
        var act = async () => await _service.ActivateVersionAsync(templateResponse.Template.Id, "non-existent-version-id", _testUserId);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>().Subject;
        exception.Which.Message.Should().Contain("not found");
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
        history.Should().NotBeNull();
        history.Template.Name.Should().Be("history-test-template");
        history.TotalCount.Should().Be(3);
        history.Versions.Count.Should().Be(3);

        // Verify descending order
        history.Versions[0].VersionNumber.Should().Be(3);
        history.Versions[1].VersionNumber.Should().Be(2);
        history.Versions[2].VersionNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetVersionHistoryAsync_NonExistentTemplate_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var act = async () => await _service.GetVersionHistoryAsync("non-existent-id");
        await act.Should().ThrowAsync<InvalidOperationException>();
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
        auditLog.Should().NotBeNull();
        auditLog.Template.Name.Should().Be("audit-test-template");
        (auditLog.TotalCount >= 5).Should().BeTrue(); // template_created, version_created (v1), version_created (v2), version_deactivated (v1), version_activated (v2)
        (auditLog.Logs.Count >= 5).Should().BeTrue();

        // Verify logs are ordered by timestamp descending (most recent first)
        for (int i = 0; i < auditLog.Logs.Count - 1; i++)
        {
            auditLog.Logs[i].ChangedAt >= auditLog.Logs[i + 1].ChangedAt.Should().BeTrue();
        }

        // Verify action types
        var actions = auditLog.Logs.Select(l => l.Action).ToList();
        actions.Should().Contain("template_created");
        actions.Should().Contain("version_created");
        actions.Should().Contain("version_activated");
        actions.Should().Contain("version_deactivated");
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
        auditLog.Logs.Count.Should().Be(5);
        (auditLog.TotalCount > 5).Should().BeTrue(); // Total is more than limit
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
        result.TotalCount.Should().Be(2);
        result.Templates.Count.Should().Be(2);
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
        result.TotalCount.Should().Be(2);
        result.Templates.Count.Should().Be(2);
        result.Templates.Should().OnlyContain(t => t.Category == "qa");
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
        template.Should().NotBeNull();
        template.Id.Should().Be(created.Template.Id);
        template.Name.Should().Be("get-template-test");
        template.Description.Should().Be("Test description");
    }

    [Fact]
    public async Task GetTemplateAsync_NonExistentTemplate_ReturnsNull()
    {
        // Act
        var template = await _service.GetTemplateAsync("non-existent-id");

        // Assert
        template.Should().BeNull();
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
        activeVersions.Should().ContainSingle();
        activeVersions[0].VersionNumber.Should().Be(3);
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
        versions.Count.Should().Be(5);
        for (int i = 0; i < versions.Count; i++)
        {
            versions[i].VersionNumber.Should().Be(i + 1);
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
        auditLog.Logs.Should().OnlyContain(log =>
            log.ChangedByUserId == _testUserId &&
            log.ChangedByEmail == "test@example.com");
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
        version.Should().NotBeNull();
        version.Metadata.Should().NotBeNull();
        version.Metadata.Should().Be(metadata);

        // Verify deserialization works
        var deserializedMetadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(version.Metadata);
        deserializedMetadata.Should().NotBeNull();
        deserializedMetadata["model"].GetString().Should().Be("gpt-4-turbo");
    }
}
