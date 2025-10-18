# CHAT-06: Message Editing and Deletion - BDD Plan & Implementation Guide

**Issue:** #421
**Feature:** Message editing and deletion for users
**Status:** In Progress (Database Migration Complete)
**Created:** 2025-01-18
**Architecture:** Soft Delete + Placeholder (Option 1C)

---

## Executive Summary

This document outlines the complete BDD implementation plan for CHAT-06, enabling users to edit and delete their own chat messages with proper audit trails, message invalidation, and soft delete patterns.

### Key Decisions (from Strategic Planning)

| Decision Area | Chosen Approach | Confidence | Rationale |
|---|---|---|---|
| **Delete Strategy** | Soft Delete + Placeholder | 9.0/10 | Maintains conversation flow, audit trail, compliance-ready |
| **Edit History** | UpdatedAt timestamp only | 8.5/10 | Simple, performant, sufficient for MVP audit needs |
| **Invalidation** | Cascade flag on subsequent AI messages | 8.0/10 | Clear UX, preserves data, supports future regeneration |
| **Authorization** | Users own messages only (MVP) | 7.5/10 | Secure by default, admin support deferred to Phase 2 |
| **Time Restrictions** | No time limit + "Edited" badge | 7.0/10 | User freedom with transparency |

---

## BDD Scenarios

### Feature: Message Editing

```gherkin
Feature: User Message Editing
  As a user
  I want to edit my chat messages
  So that I can correct mistakes without losing conversation context

Scenario: User successfully edits their own message
  Given I am logged in as "user@meepleai.dev"
  And I have a chat with ID "chat-123"
  And I have sent a message "What is checkmate?" at sequence 0
  And AI responded "Checkmate is when..." at sequence 1
  When I edit my message at sequence 0 to "What is en passant?"
  Then my message content is updated to "What is en passant?"
  And the message has UpdatedAt timestamp set
  And the UI shows "(edited)" badge
  And the AI response at sequence 1 is marked as IsInvalidated = true
  And I see a "Previous messages changed. Regenerate?" prompt

Scenario: User cannot edit AI-generated messages
  Given I am logged in as "user@meepleai.dev"
  And there is an AI message at sequence 1
  When I attempt to edit the message at sequence 1
  Then I receive a 400 Bad Request error
  And the error message states "AI-generated messages cannot be edited"
  And the UI does not show edit button for AI messages

Scenario: User cannot edit another user's message
  Given I am logged in as "user1@meepleai.dev"
  And "user2@meepleai.dev" has sent a message at sequence 0
  When I attempt to edit the message at sequence 0
  Then I receive a 403 Forbidden error
  And the error message states "You can only edit your own messages"

Scenario: Editing a message invalidates subsequent AI responses only
  Given I have a chat with the following messages:
    | Sequence | Level     | Content          | UserId     |
    | 0        | user      | Original Q1      | user-123   |
    | 1        | assistant | AI Answer 1      | null       |
    | 2        | user      | Follow-up Q2     | user-123   |
    | 3        | assistant | AI Answer 2      | null       |
  When I edit message at sequence 0 to "Edited Q1"
  Then message at sequence 1 has IsInvalidated = true
  And message at sequence 2 has IsInvalidated = false (user message)
  And message at sequence 3 has IsInvalidated = true (cascaded from seq 1)
  And only AI messages with sequence > 0 are invalidated

Scenario: Audit log records message edits
  Given I am logged in as "user@meepleai.dev"
  And I have sent a message "Original content"
  When I edit the message to "Updated content"
  Then an audit log entry is created with:
    | Field         | Value               |
    | action        | message_updated     |
    | userId        | user-123            |
    | entityType    | ChatLog             |
    | originalContent | Original content |
    | newContent    | Updated content     |
```

### Feature: Message Deletion

```gherkin
Feature: User Message Deletion
  As a user
  I want to delete my chat messages
  So that I can remove irrelevant questions without starting a new conversation

Scenario: User successfully soft-deletes their own message
  Given I am logged in as "user@meepleai.dev"
  And I have sent a message "What is stalemate?" at sequence 0
  When I click "Delete" on the message
  And I confirm the deletion in the modal dialog
  Then the message is soft-deleted (IsDeleted = true)
  And DeletedAt timestamp is set to current UTC time
  And DeletedByUserId is set to my user ID
  And the UI shows "[Message deleted by user]" placeholder
  And the original message content is preserved in the database

Scenario: Deleted message invalidates subsequent AI responses
  Given I have a chat with:
    | Sequence | Level     | Content          |
    | 0        | user      | Question 1       |
    | 1        | assistant | AI Answer 1      |
    | 2        | user      | Question 2       |
    | 3        | assistant | AI Answer 2      |
  When I delete message at sequence 0
  Then message at sequence 1 has IsInvalidated = true
  And message at sequence 3 has IsInvalidated = true
  And the conversation flow remains intact with placeholders

Scenario: Confirmation dialog prevents accidental deletion
  Given I have sent a message
  When I click "Delete" on the message
  Then a confirmation modal appears
  And the modal states "Deleting this message will invalidate subsequent AI responses. Are you sure?"
  And I can click "Delete" to confirm or "Cancel" to abort
  When I click "Cancel"
  Then the message is not deleted
  And the modal closes

Scenario: User cannot delete another user's message
  Given I am logged in as "user1@meepleai.dev"
  And "user2@meepleai.dev" has sent a message
  When I attempt to delete that message
  Then I receive a 403 Forbidden error

Scenario: Admin can delete any message (future enhancement)
  Given I am logged in as an admin user
  And "user@meepleai.dev" has sent a message
  When I delete that message
  Then the message is soft-deleted
  And DeletedByUserId is set to my admin user ID (not the original author)
  And an audit log entry records the admin deletion
```

### Feature: Message Invalidation

```gherkin
Feature: Message Invalidation on Edit/Delete
  As a user
  I want subsequent AI responses invalidated when I edit or delete a message
  So that I understand the conversation context has changed

Scenario: Invalidation cascade stops at next user message
  Given I have a chat with alternating user/AI messages up to sequence 10
  When I edit message at sequence 4
  Then AI messages at sequences 5, 7, 9 are invalidated
  But user messages at sequences 6, 8, 10 are NOT invalidated

Scenario: Invalidated messages show regeneration prompt
  Given AI messages at sequences 1, 3, 5 are invalidated
  When I view the chat history
  Then I see a banner below the edited message
  And the banner states "Previous messages changed. Regenerate?"
  And I can click "Regenerate" to trigger new AI responses
```

---

## Implementation Checklist

### Phase 1: Database & Entity Layer ✅ COMPLETE

- [x] Create migration `20250118_AddChatMessageEditDeleteSupport.cs`
- [x] Add columns: `user_id`, `sequence_number`, `updated_at`, `is_deleted`, `deleted_at`, `deleted_by_user_id`, `is_invalidated`
- [x] Add foreign keys to `users` table
- [x] Add indexes: `idx_chat_logs_user_id`, `idx_chat_logs_deleted_at`, `idx_chat_logs_chat_id_sequence_role`
- [x] Add check constraints for data integrity
- [x] Update `ChatLogEntity.cs` with new properties
- [x] Update `MeepleAiDbContext.cs` with global query filter and relationships
- [ ] Run migration: `dotnet ef database update --project src/Api`
- [ ] Verify migration in dev database

### Phase 2: Backend Service Layer (IN PROGRESS)

- [ ] Create `IChatService` interface with `UpdateMessageAsync()`, `DeleteMessageAsync()`, `InvalidateSubsequentMessagesAsync()`
- [ ] Implement `ChatService.UpdateMessageAsync()`:
  - [ ] Authorization check (userId == message.UserId)
  - [ ] Update content, set UpdatedAt
  - [ ] Call InvalidateSubsequentMessagesAsync()
  - [ ] AuditService integration
  - [ ] SaveChangesAsync() atomic transaction
- [ ] Implement `ChatService.DeleteMessageAsync()`:
  - [ ] Authorization check (userId == message.UserId OR isAdmin)
  - [ ] Soft delete (IsDeleted, DeletedAt, DeletedByUserId)
  - [ ] Call InvalidateSubsequentMessagesAsync()
  - [ ] AuditService integration
  - [ ] SaveChangesAsync() atomic transaction
- [ ] Implement `ChatService.InvalidateSubsequentMessagesAsync()`:
  - [ ] Bulk update query with ExecuteUpdateAsync()
  - [ ] WHERE chat_id = X AND sequence_number > Y AND Level = 'assistant'
  - [ ] Return count of invalidated messages

### Phase 3: DTOs & API Contracts

- [ ] Create `UpdateMessageRequest` record in `Models/Contracts.cs`
- [ ] Create `ChatMessageResponse` record (extend existing `ChatMessageDto`)
- [ ] Update existing `ChatMessageDto` to include new fields:
  - [ ] `UpdatedAt`, `IsDeleted`, `DeletedAt`, `DeletedByUserId`, `IsInvalidated`, `SequenceNumber`

### Phase 4: API Endpoints

- [ ] Implement `PUT /api/v1/chats/{chatId}/messages/{messageId}`:
  - [ ] Extract userId from session claims
  - [ ] Call `chatService.UpdateMessageAsync()`
  - [ ] Return 200 OK with `ChatMessageResponse`
  - [ ] Error handling: 401, 403, 404, 400, 500
  - [ ] OpenAPI documentation
- [ ] Implement `DELETE /api/v1/chats/{chatId}/messages/{messageId}`:
  - [ ] Extract userId and role from session claims
  - [ ] Call `chatService.DeleteMessageAsync()`
  - [ ] Return 204 No Content on success
  - [ ] Error handling: 401, 403, 404, 500
  - [ ] OpenAPI documentation

### Phase 5: Backend Testing

- [ ] Unit tests for `ChatService.UpdateMessageAsync()`:
  - [ ] Successful edit updates content and timestamp
  - [ ] Editing by non-owner throws UnauthorizedAccessException
  - [ ] Editing AI message throws InvalidOperationException
  - [ ] Editing non-existent message throws KeyNotFoundException
  - [ ] Invalidation cascade works correctly
- [ ] Unit tests for `ChatService.DeleteMessageAsync()`:
  - [ ] Successful delete sets soft delete flags
  - [ ] Deleting by non-owner throws UnauthorizedAccessException
  - [ ] Admin can delete any message
  - [ ] Double-delete is idempotent
  - [ ] Invalidation cascade works correctly
- [ ] Integration tests for PUT/DELETE endpoints:
  - [ ] Successful edit returns 200 OK
  - [ ] Unauthorized returns 401
  - [ ] Forbidden returns 403
  - [ ] Not found returns 404
  - [ ] Successful delete returns 204
  - [ ] Audit logs are created

### Phase 6: Frontend Components

- [ ] Update `ChatMessage` component in `pages/chat.tsx`:
  - [ ] Add `isEditing` state
  - [ ] Render inline editor (textarea) when `isEditing = true`
  - [ ] Show "Save" and "Cancel" buttons during edit
  - [ ] Call `PUT /api/v1/chats/{chatId}/messages/{messageId}` on save
  - [ ] Revert to original content on cancel
- [ ] Add "Edit" button visible on hover for user messages
- [ ] Add "Delete" button visible on hover for user messages
- [ ] Create `DeleteConfirmationModal` component:
  - [ ] Warning text: "Deleting this message will invalidate subsequent AI responses. Are you sure?"
  - [ ] "Delete" button calls DELETE endpoint
  - [ ] "Cancel" button closes modal
- [ ] Display "(edited)" badge when `message.updatedAt != null`
- [ ] Display "[Message deleted by user]" placeholder when `message.isDeleted = true`
- [ ] Display invalidation banner when `message.isInvalidated = true`:
  - [ ] Text: "Previous messages changed. Regenerate?"
  - [ ] "Regenerate" button (deferred to future phase)

### Phase 7: Frontend Testing

- [ ] Jest unit tests for edit/delete UI:
  - [ ] Edit button shows only for user's own messages
  - [ ] Edit mode displays textarea with current content
  - [ ] Save button calls API correctly
  - [ ] Cancel button reverts changes
  - [ ] Delete button opens confirmation modal
  - [ ] Confirm delete calls DELETE API
  - [ ] "(edited)" badge displays correctly
- [ ] Playwright E2E tests:
  - [ ] User logs in, sends message, edits it, sees "Edited" badge
  - [ ] User edits message, AI response shows "invalidated" state
  - [ ] User deletes message, sees placeholder
  - [ ] Conversation flow remains intact after delete

### Phase 8: Local Testing & Verification

- [ ] Run backend tests: `dotnet test --verbosity normal`
- [ ] Run frontend tests: `pnpm test`
- [ ] Run E2E tests: `pnpm test:e2e`
- [ ] Verify coverage: Backend >90%, Frontend >90%
- [ ] Manual QA testing in dev environment
- [ ] Load testing with 10k-message chats (verify p95 < 300ms)

---

## API Specification

### PUT /api/v1/chats/{chatId}/messages/{messageId}

**Request:**
```json
{
  "content": "Updated message content"
}
```

**Response (200 OK):**
```json
{
  "id": "msg-123",
  "chatId": "chat-456",
  "userId": "user-789",
  "role": "user",
  "content": "Updated message content",
  "sequenceNumber": 2,
  "createdAt": "2025-01-18T10:00:00Z",
  "updatedAt": "2025-01-18T10:30:00Z",
  "isDeleted": false,
  "deletedAt": null,
  "deletedByUserId": null,
  "isInvalidated": false
}
```

**Error Responses:**
- `401 Unauthorized`: No active session
- `403 Forbidden`: User does not own the message
- `404 Not Found`: Message does not exist
- `400 Bad Request`: AI message cannot be edited, or validation error
- `500 Internal Server Error`: Unexpected error

### DELETE /api/v1/chats/{chatId}/messages/{messageId}

**Response (204 No Content):** Empty body

**Error Responses:**
- `401 Unauthorized`: No active session
- `403 Forbidden`: User does not own the message (and is not admin)
- `404 Not Found`: Message does not exist
- `500 Internal Server Error`: Unexpected error

---

## Performance Targets

| Operation | Target Latency (p95) | Notes |
|-----------|---------------------|-------|
| Edit message (100-msg chat) | < 200ms | Single transaction, bulk invalidation |
| Edit message (1000-msg chat) | < 300ms | Index on (chat_id, sequence_number, Level) |
| Delete message (100-msg chat) | < 200ms | Soft delete + bulk invalidation |
| Chat history retrieval | < 500ms | Global query filter for soft-deleted |

---

## Security Considerations

1. **Authorization**: Session-based auth, verify `userId` from claims matches `message.UserId`
2. **Audit Trail**: Every edit/delete logged to `audit_logs` table with original content
3. **Soft Delete**: No data loss, preserves messages for investigation
4. **Input Validation**: Content length 1-10000 chars, required
5. **Concurrency**: EF Core optimistic concurrency (RowVersion) prevents lost updates
6. **Admin Override**: Admins can delete any message (deferred to Phase 2)

---

## Testing Strategy

### Unit Tests (xUnit + SQLite in-memory)
- **Target Coverage**: 95%
- **Focus**: Service layer business logic, authorization, invalidation cascade
- **Pattern**: BDD-style naming (`UpdateMessageAsync_WhenUserOwnsMessage_UpdatesContentAndTimestamp`)

### Integration Tests (xUnit + Testcontainers)
- **Target Coverage**: Key endpoints (PUT/DELETE)
- **Focus**: End-to-end flow with real Postgres, auth checks, audit logging
- **Pattern**: HTTP client tests with `WebApplicationFactory<Program>`

### Frontend Tests (Jest + React Testing Library)
- **Target Coverage**: 90%
- **Focus**: Component behavior, state management, API integration
- **Pattern**: Arrange-Act-Assert

### E2E Tests (Playwright)
- **Coverage**: Critical user journeys
- **Focus**: Edit message → see badge, Delete message → see placeholder, Invalidation → regenerate prompt
- **Pattern**: Real browser automation

---

## Rollout Plan

### Phase 1: Backend Foundation (Complete)
- ✅ Database migration
- ✅ Entity updates
- ⏳ Service layer implementation
- ⏳ API endpoints

### Phase 2: Frontend UI (Next)
- ⏳ Edit/delete buttons with hover actions
- ⏳ Inline editing component
- ⏳ Delete confirmation modal
- ⏳ Edited/deleted badges

### Phase 3: Testing & QA
- ⏳ Comprehensive test coverage
- ⏳ Manual QA testing
- ⏳ Load testing

### Phase 4: Production Deployment
- ⏳ Run migration on staging
- ⏳ Verify backward compatibility
- ⏳ Deploy to production
- ⏳ Monitor metrics (edit latency, delete latency, invalidation count)

---

## Future Enhancements (Post-MVP)

1. **Admin Moderation**: Admins can delete any message with audit trail
2. **Edit History UI**: Show "View Edit History" dropdown for users
3. **Regeneration Flow**: Implement "Regenerate" button to create new AI responses from invalidation point
4. **Time Restrictions**: Optional 15-minute edit window (configurable)
5. **Undo Delete**: Allow users to restore soft-deleted messages within X minutes
6. **Bulk Operations**: Delete multiple messages at once

---

## References

- **Strategic Planning**: See agent output from strategic-advisor (Section Step 2-4)
- **Architecture Design**: See agent output from system-architect (Complete design artifacts)
- **Discovery Report**: See agent output from Explore agent (Codebase patterns)
- **Framework Docs**: See agent output from doc-researcher-optimizer (ASP.NET Core, Next.js best practices)
- **Reference Implementation**: `RuleSpecCommentService.cs` (soft delete + edit tracking patterns)

---

**Status:** Ready for implementation continuation
**Next Steps:**
1. Run migration: `dotnet ef database update`
2. Implement ChatService methods
3. Add API endpoints
4. Build frontend UI
5. Write comprehensive tests
