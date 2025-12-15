namespace Api.Infrastructure.Entities;

internal class WorkflowErrorLogEntity
{
    public Guid Id { get; set; }
    public required string WorkflowId { get; set; }
    public required string ExecutionId { get; set; }
    public required string ErrorMessage { get; set; }
    public string? NodeName { get; set; }
    public int RetryCount { get; set; }
    public string? StackTrace { get; set; }
    public DateTime CreatedAt { get; set; }
}
