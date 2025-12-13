using Api.BoundedContexts.Administration.Application.Queries.AlertRules;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Handlers.AlertRules;

public class GetAlertTemplatesQueryHandler : IRequestHandler<GetAlertTemplatesQuery, List<AlertTemplateDto>>
{
    public Task<List<AlertTemplateDto>> Handle(GetAlertTemplatesQuery request, CancellationToken ct)
    {
        var templates = new List<AlertTemplateDto>
        {
            new("High Error Rate", "HighErrorRate", "Critical", 5.0, "%", 5, "Triggers when error rate exceeds 5% for 5 minutes", "System"),
            new("High Latency P95", "HighLatency", "Warning", 1000.0, "ms", 10, "Triggers when P95 latency exceeds 1000ms for 10 minutes", "Performance"),
            new("Qdrant Down", "QdrantDown", "Critical", 1.0, "count", 1, "Triggers when Qdrant health check fails", "Infrastructure"),
            new("High CPU Usage", "HighCPU", "Warning", 80.0, "%", 15, "Triggers when CPU usage exceeds 80% for 15 minutes", "Infrastructure"),
            new("Low Disk Space", "LowDisk", "Error", 20.0, "%", 30, "Triggers when disk space falls below 20%", "Infrastructure"),
            new("High Memory Usage", "HighMemory", "Warning", 85.0, "%", 10, "Triggers when memory usage exceeds 85% for 10 minutes", "Infrastructure"),
            new("API Rate Limit Exceeded", "RateLimitExceeded", "Warning", 100.0, "count", 5, "Triggers when API rate limit is exceeded", "Security")
        };
        return Task.FromResult(templates);
    }
}
