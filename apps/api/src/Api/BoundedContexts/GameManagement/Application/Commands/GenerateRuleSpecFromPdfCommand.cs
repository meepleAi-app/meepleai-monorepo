using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to generate a RuleSpec from a PDF document's extracted rules.
/// </summary>
internal record GenerateRuleSpecFromPdfCommand(
    Guid PdfDocumentId
) : ICommand<RuleSpecDto>;
