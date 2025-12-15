using Api.Infrastructure.Entities;
using Api.Models;

namespace Api.BoundedContexts.GameManagement.Application.Mappers;

/// <summary>
/// Mapper for RuleSpecCommentEntity to RuleCommentDto.
/// Centralizes mapping logic to eliminate duplication across handlers.
/// </summary>
internal static class RuleCommentMapper
{
    /// <summary>
    /// Maps a RuleSpecCommentEntity to RuleCommentDto with recursive reply mapping.
    /// </summary>
    public static RuleCommentDto ToDto(this RuleSpecCommentEntity entity)
    {
        return new RuleCommentDto(
            Id: entity.Id,
            GameId: entity.GameId.ToString(),
            Version: entity.Version,
            LineNumber: entity.LineNumber,
            LineContext: entity.LineContext,
            ParentCommentId: entity.ParentCommentId,
            CommentText: entity.CommentText,
            UserId: entity.UserId.ToString(),
            UserDisplayName: entity.User?.DisplayName ?? "Unknown",
            IsResolved: entity.IsResolved,
            ResolvedByUserId: entity.ResolvedByUserId?.ToString(),
            ResolvedByDisplayName: entity.ResolvedByUser?.DisplayName,
            ResolvedAt: entity.ResolvedAt,
            MentionedUserIds: entity.MentionedUserIds.Select(id => id.ToString()).ToList(),
            Replies: entity.Replies?.Select(ToDto).ToList() ?? new List<RuleCommentDto>(),
            CreatedAt: entity.CreatedAt,
            UpdatedAt: entity.UpdatedAt
        );
    }
}
