using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get the audit log for a prompt template.
/// </summary>
public record GetPromptAuditLogQuery(
    string TemplateId,
    int Limit = 100
) : IQuery<PromptAuditLogResponse>;
