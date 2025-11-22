using Api.BoundedContexts.WorkflowIntegration.Application.Commands;
using Api.BoundedContexts.WorkflowIntegration.Application.DTOs;
using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Handlers;

public class LogWorkflowErrorCommandHandler : ICommandHandler<LogWorkflowErrorCommand, WorkflowErrorLogDto>
{
    private readonly IWorkflowErrorLogRepository _errorLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    public LogWorkflowErrorCommandHandler(
        IWorkflowErrorLogRepository errorLogRepository,
        IUnitOfWork unitOfWork)
    {
        _errorLogRepository = errorLogRepository ?? throw new ArgumentNullException(nameof(errorLogRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<WorkflowErrorLogDto> Handle(LogWorkflowErrorCommand command, CancellationToken cancellationToken)
    {
        var errorLog = new WorkflowErrorLog(
            id: Guid.NewGuid(),
            workflowId: command.WorkflowId,
            executionId: command.ExecutionId,
            errorMessage: command.ErrorMessage,
            nodeName: command.NodeName,
            stackTrace: command.StackTrace
        );

        await _errorLogRepository.AddAsync(errorLog, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new WorkflowErrorLogDto(
            Id: errorLog.Id,
            WorkflowId: errorLog.WorkflowId,
            ExecutionId: errorLog.ExecutionId,
            ErrorMessage: errorLog.ErrorMessage,
            NodeName: errorLog.NodeName,
            RetryCount: errorLog.RetryCount,
            CreatedAt: errorLog.CreatedAt
        );
    }
}
