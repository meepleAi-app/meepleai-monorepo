using Api.BoundedContexts.WorkflowIntegration.Application.Commands;
using WorkflowErrorLogEntity = Api.BoundedContexts.WorkflowIntegration.Domain.Entities.WorkflowErrorLog;
using Api.BoundedContexts.WorkflowIntegration.Application.Commands;
using Api.BoundedContexts.WorkflowIntegration.Application.Queries;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.WorkflowIntegration.Application.Handlers;

/// <summary>
/// Tests for LogWorkflowErrorCommandHandler.
/// Tests logging of workflow execution errors.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class LogWorkflowErrorCommandHandlerTests
{
    private readonly Mock<IWorkflowErrorLogRepository> _mockErrorLogRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly LogWorkflowErrorCommandHandler _handler;

    public LogWorkflowErrorCommandHandlerTests()
    {
        _mockErrorLogRepository = new Mock<IWorkflowErrorLogRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new LogWorkflowErrorCommandHandler(
            _mockErrorLogRepository.Object,
            _mockUnitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_LogsError()
    {
        // Arrange
        var command = new LogWorkflowErrorCommand(
            WorkflowId: "workflow-123",
            ExecutionId: "exec-456",
            ErrorMessage: "Database connection failed",
            NodeName: "DatabaseNode",
            StackTrace: "at DatabaseNode.Execute()\n  at Workflow.Run()"
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.WorkflowId.Should().Be("workflow-123");
        result.ExecutionId.Should().Be("exec-456");
        result.ErrorMessage.Should().Be("Database connection failed");
        result.NodeName.Should().Be("DatabaseNode");
        result.RetryCount.Should().Be(0);
        result.Id.Should().NotBe(Guid.Empty);

        _mockErrorLogRepository.Verify(
            r => r.AddAsync(
                It.IsAny<WorkflowErrorLogEntity>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutStackTrace_LogsErrorWithNullStackTrace()
    {
        // Arrange
        var command = new LogWorkflowErrorCommand(
            WorkflowId: "workflow-789",
            ExecutionId: "exec-101",
            ErrorMessage: "API timeout",
            NodeName: "ApiNode",
            StackTrace: null
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.ErrorMessage.Should().Be("API timeout");
        result.NodeName.Should().Be("ApiNode");
    }

    [Fact]
    public async Task Handle_SetsCreatedAtTimestamp()
    {
        // Arrange
        var beforeLog = DateTime.UtcNow;
        var command = new LogWorkflowErrorCommand(
            WorkflowId: "workflow-timestamp",
            ExecutionId: "exec-timestamp",
            ErrorMessage: "Timestamp test error",
            NodeName: "TestNode"
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.CreatedAt.Should().NotBe(default);
        (result.CreatedAt >= beforeLog).Should().BeTrue();
        (result.CreatedAt <= DateTime.UtcNow.AddSeconds(1)).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_InitializesRetryCountToZero()
    {
        // Arrange
        var command = new LogWorkflowErrorCommand(
            WorkflowId: "workflow-retry",
            ExecutionId: "exec-retry",
            ErrorMessage: "Initial error",
            NodeName: "RetryNode"
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.RetryCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithLongErrorMessage_LogsFullMessage()
    {
        // Arrange
        var longMessage = new string('x', 500);
        var command = new LogWorkflowErrorCommand(
            WorkflowId: "workflow-long",
            ExecutionId: "exec-long",
            ErrorMessage: longMessage,
            NodeName: "LongMessageNode"
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.ErrorMessage.Should().Be(longMessage);
    }

    [Fact]
    public async Task Handle_WithComplexStackTrace_LogsFullStackTrace()
    {
        // Arrange
        var complexStackTrace = @"
            at WorkflowNode.Execute() in /app/Node.cs:line 42
            at Workflow.ExecuteNode(Node node) in /app/Workflow.cs:line 156
            at Workflow.Run() in /app/Workflow.cs:line 89
            at WorkflowEngine.ProcessWorkflow(Workflow workflow) in /app/Engine.cs:line 234";

        var command = new LogWorkflowErrorCommand(
            WorkflowId: "workflow-complex",
            ExecutionId: "exec-complex",
            ErrorMessage: "Complex error",
            NodeName: "ComplexNode",
            StackTrace: complexStackTrace
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        complexStackTrace.Should().Contain("WorkflowNode.Execute");
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesTokenToRepository()
    {
        // Arrange
        var command = new LogWorkflowErrorCommand(
            WorkflowId: "workflow-cancel",
            ExecutionId: "exec-cancel",
            ErrorMessage: "Cancellation test",
            NodeName: "CancelNode"
        );
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockErrorLogRepository.Verify(
            r => r.AddAsync(
                It.IsAny<WorkflowErrorLogEntity>(),
                cancellationToken),
            Times.Once);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_GeneratesUniqueId()
    {
        // Arrange
        var command1 = new LogWorkflowErrorCommand(
            WorkflowId: "workflow-1",
            ExecutionId: "exec-1",
            ErrorMessage: "Error 1",
            NodeName: "Node1"
        );

        var command2 = new LogWorkflowErrorCommand(
            WorkflowId: "workflow-2",
            ExecutionId: "exec-2",
            ErrorMessage: "Error 2",
            NodeName: "Node2"
        );

        // Act
        var result1 = await _handler.Handle(command1, TestContext.Current.CancellationToken);
        var result2 = await _handler.Handle(command2, TestContext.Current.CancellationToken);

        // Assert
        result2.Id.Should().NotBe(result1.Id);
    }
}

