using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for CompareAnalysesQuery — computes a diff between two rulebook analyses.
/// Issue #5461: Analysis comparison tool.
/// </summary>
internal sealed class CompareAnalysesQueryHandler
    : IQueryHandler<CompareAnalysesQuery, AnalysisComparisonDto>
{
    private readonly IRulebookAnalysisRepository _repository;

    public CompareAnalysesQueryHandler(IRulebookAnalysisRepository repository)
    {
        _repository = repository;
    }

    public async Task<AnalysisComparisonDto> Handle(
        CompareAnalysesQuery query,
        CancellationToken cancellationToken)
    {
        var left = await _repository.GetByIdAsync(query.LeftAnalysisId, cancellationToken)
            .ConfigureAwait(false);
        var right = await _repository.GetByIdAsync(query.RightAnalysisId, cancellationToken)
            .ConfigureAwait(false);

        if (left is null)
            throw new KeyNotFoundException($"Analysis {query.LeftAnalysisId} not found");
        if (right is null)
            throw new KeyNotFoundException($"Analysis {query.RightAnalysisId} not found");

        var mechanicsDiff = ComputeListDiff(
            left.KeyMechanics.ToList(),
            right.KeyMechanics.ToList());

        var questionsDiff = ComputeListDiff(
            left.CommonQuestions.ToList(),
            right.CommonQuestions.ToList());

        var keyConceptsDiff = ComputeListDiff(
            left.KeyConcepts.Select(kc => $"{kc.Term}: {kc.Definition}").ToList(),
            right.KeyConcepts.Select(kc => $"{kc.Term}: {kc.Definition}").ToList());

        var faqDiff = ComputeFaqDiff(
            left.GeneratedFaqs.ToList(),
            right.GeneratedFaqs.ToList());

        return new AnalysisComparisonDto(
            LeftId: left.Id,
            RightId: right.Id,
            LeftVersion: left.Version,
            RightVersion: right.Version,
            LeftAnalyzedAt: left.AnalyzedAt,
            RightAnalyzedAt: right.AnalyzedAt,
            ConfidenceScoreDelta: right.ConfidenceScore - left.ConfidenceScore,
            MechanicsDiff: mechanicsDiff,
            CommonQuestionsDiff: questionsDiff,
            KeyConceptsDiff: keyConceptsDiff,
            FaqDiff: faqDiff,
            SummaryChanged: !string.Equals(left.Summary, right.Summary, StringComparison.Ordinal),
            LeftSummary: left.Summary,
            RightSummary: right.Summary
        );
    }

    private static ListDiffDto<string> ComputeListDiff(List<string> left, List<string> right)
    {
        var leftSet = new HashSet<string>(left, StringComparer.OrdinalIgnoreCase);
        var rightSet = new HashSet<string>(right, StringComparer.OrdinalIgnoreCase);

        var added = right.Where(item => !leftSet.Contains(item)).ToList();
        var removed = left.Where(item => !rightSet.Contains(item)).ToList();
        var unchanged = left.Where(item => rightSet.Contains(item)).ToList();

        return new ListDiffDto<string>(added, removed, unchanged);
    }

    private static FaqDiffDto ComputeFaqDiff(
        List<Domain.Entities.GeneratedFaq> left,
        List<Domain.Entities.GeneratedFaq> right)
    {
        var leftByQuestion = left.ToDictionary(
            f => f.Question, f => f, StringComparer.OrdinalIgnoreCase);
        var rightByQuestion = right.ToDictionary(
            f => f.Question, f => f, StringComparer.OrdinalIgnoreCase);

        var added = new List<FaqDiffItemDto>();
        var removed = new List<FaqDiffItemDto>();
        var modified = new List<FaqModifiedDto>();
        var unchanged = new List<FaqDiffItemDto>();

        foreach (var faq in left)
        {
            if (!rightByQuestion.TryGetValue(faq.Question, out var rightFaq))
            {
                removed.Add(new FaqDiffItemDto(faq.Question, faq.Answer, faq.Confidence));
            }
            else if (!string.Equals(faq.Answer, rightFaq.Answer, StringComparison.Ordinal) || faq.Confidence != rightFaq.Confidence)
            {
                modified.Add(new FaqModifiedDto(
                    faq.Question,
                    faq.Answer,
                    rightFaq.Answer,
                    faq.Confidence,
                    rightFaq.Confidence));
            }
            else
            {
                unchanged.Add(new FaqDiffItemDto(faq.Question, faq.Answer, faq.Confidence));
            }
        }

        foreach (var faq in right)
        {
            if (!leftByQuestion.ContainsKey(faq.Question))
            {
                added.Add(new FaqDiffItemDto(faq.Question, faq.Answer, faq.Confidence));
            }
        }

        return new FaqDiffDto(added, removed, modified, unchanged);
    }
}
