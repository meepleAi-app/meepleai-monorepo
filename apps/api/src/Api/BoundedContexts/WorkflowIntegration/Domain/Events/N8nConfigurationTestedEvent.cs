using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.WorkflowIntegration.Domain.Events;

/// <summary>
/// Domain event raised when an N8N configuration connection test is performed.
/// </summary>
public sealed class N8NConfigurationTestedEvent : DomainEventBase
{
    public Guid ConfigurationId { get; }
    public bool TestSuccess { get; }
    public string TestResult { get; }
    public DateTime TestedAt { get; }

    public N8NConfigurationTestedEvent(
        Guid configurationId,
        bool testSuccess,
        string testResult,
        DateTime testedAt)
    {
        ConfigurationId = configurationId;
        TestSuccess = testSuccess;
        TestResult = testResult;
        TestedAt = testedAt;
    }
}
