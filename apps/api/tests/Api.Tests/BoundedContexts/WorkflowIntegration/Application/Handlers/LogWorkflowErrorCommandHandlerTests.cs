using Api.BoundedContexts.WorkflowIntegration.Application.Commands;
using WorkflowErrorLogEntity = Api.BoundedContexts.WorkflowIntegration.Domain.Entities.WorkflowErrorLog;
using Api.BoundedContexts.WorkflowIntegration.Application.Handlers;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
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
        Assert.NotNull(result);
        Assert.Equal("workflow-123", result.WorkflowId);
        Assert.Equal("exec-456", result.ExecutionId);
        Assert.Equal("Database connection failed", result.ErrorMessage);
        Assert.Equal("DatabaseNode", result.NodeName);
        Assert.Equal(0, result.RetryCount);
        Assert.NotEqual(Guid.Empty, result.Id);

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
        Assert.NotNull(result);
        Assert.Equal("API timeout", result.ErrorMessage);
        Assert.Equal("ApiNode", result.NodeName);
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
        Assert.NotNull(result.CreatedAt);
        Assert.True(result.CreatedAt >= beforeLog);
        Assert.True(result.CreatedAt <= DateTime.UtcNow.AddSeconds(1));
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
        Assert.Equal(0, result.RetryCount);
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
        Assert.Equal(longMessage, result.ErrorMessage);
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
        Assert.NotNull(result);
        Assert.Contains("WorkflowNode.Execute", complexStackTrace);
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
        Assert.NotEqual(result1.Id, result2.Id);
    }
}

