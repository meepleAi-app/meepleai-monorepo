using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles generating a RuleSpec from a PDF document's extracted rules.
/// </summary>
public class GenerateRuleSpecFromPdfCommandHandler : ICommandHandler<GenerateRuleSpecFromPdfCommand, RuleSpecDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly RuleAtomParsingDomainService _parsingService;
    private readonly TimeProvider _timeProvider;

    public GenerateRuleSpecFromPdfCommandHandler(
        MeepleAiDbContext dbContext,
        RuleAtomParsingDomainService parsingService,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _parsingService = parsingService;
        _timeProvider = timeProvider;
    }

    public async Task<RuleSpecDto> Handle(GenerateRuleSpecFromPdfCommand command, CancellationToken cancellationToken)
    {
        var pdf = await _dbContext.PdfDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == command.PdfDocumentId, cancellationToken);

        if (pdf is null)
        {
            throw new InvalidOperationException($"PDF document {command.PdfDocumentId} not found");
        }

        // Parse rules from PDF (try AtomicRules first, then ExtractedText)
        var rules = _parsingService.ParseAtomicRulesFromJson(pdf.AtomicRules);

        if (rules.Count == 0)
        {
            rules = _parsingService.ParseRulesFromExtractedText(pdf.ExtractedText);
        }

        if (rules.Count == 0)
        {
            throw new InvalidOperationException($"PDF document {command.PdfDocumentId} does not contain any parsed rules");
        }

        // Create RuleAtoms
        var atoms = new List<RuleAtomDto>();
        for (int index = 0; index < rules.Count; index++)
        {
            atoms.Add(_parsingService.CreateRuleAtom(rules[index], index + 1));
        }

        // Generate version
        var timestamp = _timeProvider.GetUtcNow().UtcDateTime;
        var version = $"ingest-{timestamp:yyyyMMddHHmmss}";

        return new RuleSpecDto(
            Id: Guid.NewGuid(), // Temporary - will be assigned by DB
            GameId: pdf.GameId,
            Version: version,
            CreatedAt: timestamp,
            CreatedByUserId: null,
            ParentVersionId: null,
            Atoms: atoms
        );
    }
}
