using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Commands.Operations;
using Api.BoundedContexts.Administration.Application.Queries.Operations;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.Unit.Administration.Operations;

/// <summary>
/// Unit tests for GetSentEmailsQueryHandler.
/// Issue #3696: Operations - Service Control Panel.
/// </summary>
public sealed class GetSentEmailsQueryHandlerTests
{
    private readonly Mock<IAuditLogRepository> _mockAuditLogRepository;
    private readonly Mock<ILogger<GetSentEmailsQueryHandler>> _mockLogger;
    private readonly GetSentEmailsQueryHandler _handler;

    public GetSentEmailsQueryHandlerTests()
    {
        _mockAuditLogRepository = new Mock<IAuditLogRepository>();
        _mockLogger = new Mock<ILogger<GetSentEmailsQueryHandler>>();

        _handler = new GetSentEmailsQueryHandler(
            _mockAuditLogRepository.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public async Task Handle_WithEmailLogs_ReturnsEmailDtos()
    {
        // Arrange
        var query = new GetSentEmailsQuery(50, 0, null, null, null);

        var emailLog1 = CreateEmailLog(
            to: "user@test.com",
            subject: "Welcome Email",
            result: "success"
        );

        var emailLog2 = CreateEmailLog(
            to: "admin@test.com",
            subject: "Password Reset",
            result: "success"
        );

        var logs = new List<AuditLog> { emailLog1, emailLog2 };

        _mockAuditLogRepository
            .Setup(x => x.GetEmailSentLogsAsync(50, 0, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        _mockAuditLogRepository
            .Setup(x => x.CountEmailSentLogsAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Emails.Count.Should().Be(2);
        result.Total.Should().Be(2);
        result.Emails[0].To.Should().Be("user@test.com");
        result.Emails[0].Subject.Should().Be("Welcome Email");
        result.Emails[0].Status.Should().Be("success");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public async Task Handle_WithFailedEmail_IncludesErrorMessage()
    {
        // Arrange
        var query = new GetSentEmailsQuery(50, 0, null, null, "failed");

        var failedEmailLog = CreateEmailLog(
            to: "user@test.com",
            subject: "Failed Email",
            result: "failed",
            error: "SMTP connection timeout"
        );

        var logs = new List<AuditLog> { failedEmailLog };

        _mockAuditLogRepository
            .Setup(x => x.GetEmailSentLogsAsync(50, 0, null, null, "failed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        _mockAuditLogRepository
            .Setup(x => x.CountEmailSentLogsAsync(null, null, "failed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Emails.Should().ContainSingle();
        result.Emails[0].Status.Should().Be("failed");
        result.Emails[0].ErrorMessage.Should().Be("SMTP connection timeout");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public async Task Handle_WithPagination_ReturnsCorrectLimitAndOffset()
    {
        // Arrange
        var query = new GetSentEmailsQuery(25, 50, null, null, null);

        _mockAuditLogRepository
            .Setup(x => x.GetEmailSentLogsAsync(25, 50, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLog>());

        _mockAuditLogRepository
            .Setup(x => x.CountEmailSentLogsAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(150);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Total.Should().Be(150);
        result.Limit.Should().Be(25);
        result.Offset.Should().Be(50);
    }

    private static AuditLog CreateEmailLog(
        string to,
        string subject,
        string result,
        string? error = null)
    {
        var details = System.Text.Json.JsonSerializer.Serialize(new
        {
            to,
            subject,
            preview = "Email content preview...",
            error
        });

        return new AuditLog(
            id: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            action: "email_sent",
            resource: "Email",
            result: result,
            resourceId: null,
            details: details,
            ipAddress: "127.0.0.1"
        );
    }
}
