using Api.BoundedContexts.Administration.Application.Attributes;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to delete a rule comment.
/// Only the comment author, or an editor/admin/superadmin, can delete a comment.
/// </summary>
/// <remarks>
/// ISSUE #3994 — why this is audited. The delete is irreversible (the entity has no
/// <c>IsDeleted</c>/<c>DeletedAt</c>) and it cascades: <c>DeleteRepliesRecursivelyAsync</c> removes
/// the whole subtree, while the ownership check runs only on the root comment. So one call can
/// destroy other users' replies, and until #3994 the admin override was dead code
/// (<c>RuleSpecEndpoints</c> compared <c>"Admin"</c> against a value that is lowercase by
/// construction), which made the absence of an audit trail harmless by accident.
/// <para>
/// Turning the override on is what makes the trail necessary, so it is added in the same change
/// rather than left as a follow-up. Best-effort and not <c>[AtomicAudit]</c>: a lost audit row must
/// not roll back a deletion the user already saw succeed, and the behaviour writes an Error audit
/// if the command itself fails.
/// </para>
/// </remarks>
[AuditableAction("RuleCommentDelete", "RuleSpecComment", Level = 1)]
internal record DeleteRuleCommentCommand(
    Guid CommentId,
    Guid UserId,
    bool IsAdmin
) : IRequest<bool>;
