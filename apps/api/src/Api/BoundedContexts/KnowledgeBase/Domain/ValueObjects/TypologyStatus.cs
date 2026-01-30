namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Agent typology approval workflow status.
/// Issue #3175
/// </summary>
public enum TypologyStatus
{
    /// <summary>
    /// Typology is being drafted and not yet submitted for approval.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Typology is pending admin approval.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Typology has been approved and is available for use.
    /// </summary>
    Approved = 2,

    /// <summary>
    /// Typology has been rejected and cannot be used.
    /// </summary>
    Rejected = 3
}
