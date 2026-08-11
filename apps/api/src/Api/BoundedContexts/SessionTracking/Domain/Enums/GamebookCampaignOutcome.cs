namespace Api.BoundedContexts.SessionTracking.Domain.Enums;

/// <summary>
/// Terminal outcome of a libro-game campaign, set by the play-evening-end 3-way
/// selector (issue #2639 SI-8). A <c>null</c> outcome means the campaign is still
/// open/resumable ("Archivia" leaves it null); the two members are the terminal
/// choices. There is no <c>None = 0</c> member on purpose — "open" is represented
/// by the nullable column being NULL, so the resume picker can filter on
/// <c>outcome IS NULL</c>.
/// </summary>
public enum GamebookCampaignOutcome
{
    /// <summary>"Completa" — the campaign was finished. Terminal.</summary>
    Completed = 1,

    /// <summary>"Abbandona" — the campaign was dropped. Terminal.</summary>
    Abandoned = 2,
}
