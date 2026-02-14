/**
 * Comment types for RuleSpec comments system
 * Issue #4368: Extracted from deleted chat.schemas.ts
 */

export interface RuleSpecComment {
  id: string;
  userId: string;
  userDisplayName: string;
  commentText: string;
  atomId: string | null;
  lineNumber: number | null;
  lineContext: string | null;
  isResolved: boolean;
  resolvedAt: string | null;
  resolvedByDisplayName: string | null;
  mentionedUserIds: string[] | null;
  createdAt: string;
  updatedAt: string | null;
  replies: RuleSpecComment[];
}

export interface RuleSpecCommentsResponse {
  comments: RuleSpecComment[];
}

export interface CreateRuleSpecCommentRequest {
  atomId: string | null;
  lineNumber: number | null;
  commentText: string;
}

export interface UpdateRuleSpecCommentRequest {
  commentText: string;
}

export interface CreateReplyRequest {
  commentText: string;
}
