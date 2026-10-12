namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;

/// <summary>Il sink dello spike: ricorda le proposte invece di salvarle.</summary>
internal sealed class RecordingProposalSink : IAssistantProposalSink
{
    private readonly List<ExpectedProposal> _proposals = [];

    public IReadOnlyList<ExpectedProposal> Proposals => _proposals;

    public Task<ProposalReceipt> ProposeScoreAsync(ScoreProposalDraft draft, CancellationToken cancellationToken)
    {
        _proposals.Add(new ExpectedProposal(ExpectedProposal.ScoreKind, draft.PlayerName, draft.Dimension, draft.Round, draft.Points));
        return Task.FromResult(new ProposalReceipt(Guid.NewGuid(), $"{draft.Points:+#;-#;0} a {draft.PlayerName} ({draft.Dimension}, round {draft.Round})"));
    }

    public Task<ProposalReceipt> ProposeTurnAsync(TurnProposalDraft draft, CancellationToken cancellationToken)
    {
        _proposals.Add(new ExpectedProposal(ExpectedProposal.AdvanceTurnKind, null, null, null, null));
        return Task.FromResult(new ProposalReceipt(Guid.NewGuid(), "passa il turno"));
    }
}
