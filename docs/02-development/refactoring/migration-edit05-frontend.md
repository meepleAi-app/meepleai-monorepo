# Frontend Migration Guide: Legacy SimpleRuleComment → EDIT-05 Enhanced Comments

**Status**: 🔴 **REQUIRED MIGRATION**
**Priority**: High
**Impact**: Breaking Changes
**Last Updated**: 2025-12-13T10:59:23.970Z

---

## Executive Summary

The backend legacy `SimpleRuleComment` system has been **removed** in favor of the enhanced EDIT-05 comment system with threading, resolution, and @mention support. The frontend currently uses the legacy endpoints **which no longer exist** and must be migrated.

**Backend Status**: ✅ **Complete** (Legacy code removed, ~987 lines deleted)
**Frontend Status**: 🔴 **Pending Migration** (Still uses legacy endpoints)

---

## 📋 Table of Contents

1. [Endpoint Changes](#endpoint-changes)
2. [Type System Changes](#type-system-changes)
3. [API Client Migration](#api-client-migration)
4. [Component Migration](#component-migration)
5. [Test Migration](#test-migration)
6. [Validation Checklist](#validation-checklist)

---

## 1. Endpoint Changes

### ❌ Legacy Endpoints (REMOVED)

```typescript
// GET Comments
GET /api/v1/games/{gameId}/rulespec/versions/{version}/comments
→ Returns: RuleSpecCommentsResponse

// POST Comment
POST /api/v1/games/{gameId}/rulespec/versions/{version}/comments
Body: { atomId: string | null, commentText: string }
→ Returns: RuleSpecComment

// PUT Update Comment
PUT /api/v1/games/{gameId}/rulespec/comments/{commentId}
Body: { commentText: string }
→ Returns: RuleSpecComment

// DELETE Comment
DELETE /api/v1/games/{gameId}/rulespec/comments/{commentId}
→ Returns: void
```

### ✅ New EDIT-05 Endpoints

```typescript
// GET Comments (with threading and resolution)
GET /api/v1/rulespecs/{gameId}/{version}/comments?includeResolved={bool}
→ Returns: RuleCommentDto[] (hierarchical with replies)

// POST Top-Level Comment
POST /api/v1/rulespecs/{gameId}/{version}/comments
Body: { lineNumber: number | null, commentText: string }
→ Returns: RuleCommentDto

// POST Reply to Comment (NEW - Threading)
POST /api/v1/comments/{commentId}/replies
Body: { commentText: string }
→ Returns: RuleCommentDto

// POST Resolve Comment (NEW)
POST /api/v1/comments/{commentId}/resolve?resolveReplies={bool}
→ Returns: RuleCommentDto

// POST Unresolve Comment (NEW)
POST /api/v1/comments/{commentId}/unresolve?unresolveParent={bool}
→ Returns: RuleCommentDto

// GET Comments for Specific Line (NEW)
GET /api/v1/rulespecs/{gameId}/{version}/lines/{lineNumber}/comments
→ Returns: RuleCommentDto[]
```

**Note**: Update and Delete for individual comments are handled through the new `RuleComment` CRUD handlers (not yet exposed as endpoints - see backend handlers in `GameManagement/Application/Handlers/UpdateRuleCommentCommandHandler.cs` and `DeleteRuleCommentCommandHandler.cs`).

---

## 2. Type System Changes

### ❌ Legacy Types (REMOVE)

```typescript
// apps/web/src/types/api.ts
export interface RuleSpecCommentsResponse {
  gameId: string;
  version: string;
  comments: RuleSpecComment[];
  totalComments: number;
  totalCount?: number;
}

export interface CreateRuleSpecCommentRequest {
  atomId: string | null;
  commentText: string;
}

export interface UpdateRuleSpecCommentRequest {
  commentText: string;
}

// apps/web/src/types/domain.ts
export interface RuleSpecComment {
  id: string;
  gameId: string;
  version: string;
  atomId: string | null;  // Legacy - replaced by lineNumber
  userId: string;
  userDisplayName: string;
  commentText: string;
  createdAt: string;
  updatedAt: string | null;
}

// apps/web/src/lib/api.ts (duplicated definitions)
export interface RuleSpecComment { ... }
export interface RuleSpecCommentsResponse { ... }
export interface CreateRuleSpecCommentRequest { ... }
export interface UpdateRuleSpecCommentRequest { ... }
```

### ✅ New EDIT-05 Types (ADD)

```typescript
// apps/web/src/types/domain.ts
export interface RuleCommentDto {
  id: string;
  gameId: string;
  version: string;
  lineNumber: number | null;        // Replaces atomId
  parentCommentId: string | null;   // NEW - Threading support
  userId: string;
  userDisplayName: string;
  commentText: string;
  mentionedUserIds: string[];       // NEW - @mention support
  isResolved: boolean;              // NEW - Resolution tracking
  resolvedAt: string | null;        // NEW
  resolvedByUserId: string | null;  // NEW
  resolvedByUserName: string | null; // NEW
  replies: RuleCommentDto[];        // NEW - Nested replies (recursive)
  createdAt: string;
  updatedAt: string | null;
}

// apps/web/src/types/api.ts
export interface CreateCommentRequest {
  lineNumber: number | null;
  commentText: string;
}

export interface CreateReplyRequest {
  commentText: string;
}

export interface ResolveCommentRequest {
  resolveReplies: boolean;
}

export interface UnresolveCommentRequest {
  unresolveParent: boolean;
}
```

---

## 3. API Client Migration

### File: `apps/web/src/lib/api.ts`

#### ❌ Current Implementation (Lines 538-568)

```typescript
ruleSpecComments: {
  async getComments(
    gameId: string,
    version: string,
    includeResolved = true
  ): Promise<RuleSpecCommentsResponse> {
    const resolvedParam = includeResolved ? 'true' : 'false';
    return api.get<RuleSpecCommentsResponse>(
      `/api/v1/games/${gameId}/rulespec/versions/${version}/comments?includeResolved=${resolvedParam}`
    );
  },

  async createComment(
    gameId: string,
    version: string,
    request: CreateRuleSpecCommentRequest
  ): Promise<RuleSpecComment> {
    return api.post<RuleSpecComment>(
      `/api/v1/games/${gameId}/rulespec/versions/${version}/comments`,
      request
    );
  },

  async updateComment(
    gameId: string,
    commentId: string,
    request: UpdateRuleSpecCommentRequest
  ): Promise<RuleSpecComment> {
    return api.put<RuleSpecComment>(
      `/api/v1/games/${gameId}/rulespec/comments/${commentId}`,
      request
    );
  },

  async deleteComment(gameId: string, commentId: string): Promise<void> {
    return api.delete(`/api/v1/games/${gameId}/rulespec/comments/${commentId}`);
  },
},
```

#### ✅ New Implementation

```typescript
ruleSpecComments: {
  // Get all comments for a RuleSpec version (hierarchical with threading)
  async getComments(
    gameId: string,
    version: string,
    includeResolved = true
  ): Promise<RuleCommentDto[]> {
    const resolvedParam = includeResolved ? 'true' : 'false';
    return api.get<RuleCommentDto[]>(
      `/api/v1/rulespecs/${gameId}/${version}/comments?includeResolved=${resolvedParam}`
    );
  },

  // Get comments for a specific line
  async getCommentsForLine(
    gameId: string,
    version: string,
    lineNumber: number
  ): Promise<RuleCommentDto[]> {
    return api.get<RuleCommentDto[]>(
      `/api/v1/rulespecs/${gameId}/${version}/lines/${lineNumber}/comments`
    );
  },

  // Create a top-level comment
  async createComment(
    gameId: string,
    version: string,
    request: CreateCommentRequest
  ): Promise<RuleCommentDto> {
    return api.post<RuleCommentDto>(
      `/api/v1/rulespecs/${gameId}/${version}/comments`,
      request
    );
  },

  // Create a reply to an existing comment (NEW - Threading)
  async createReply(
    commentId: string,
    commentText: string
  ): Promise<RuleCommentDto> {
    return api.post<RuleCommentDto>(
      `/api/v1/comments/${commentId}/replies`,
      { commentText }
    );
  },

  // Resolve a comment (NEW)
  async resolveComment(
    commentId: string,
    resolveReplies = false
  ): Promise<RuleCommentDto> {
    return api.post<RuleCommentDto>(
      `/api/v1/comments/${commentId}/resolve?resolveReplies=${resolveReplies}`,
      {}
    );
  },

  // Unresolve a comment (NEW)
  async unresolveComment(
    commentId: string,
    unresolveParent = false
  ): Promise<RuleCommentDto> {
    return api.post<RuleCommentDto>(
      `/api/v1/comments/${commentId}/unresolve?unresolveParent=${unresolveParent}`,
      {}
    );
  },

  // NOTE: Update and Delete are not yet exposed as endpoints
  // Backend handlers exist but need endpoint registration:
  // - UpdateRuleCommentCommand
  // - DeleteRuleCommentCommand
  // These will be added in a future sprint
},
```

---

## 4. Component Migration

### Components to Update

#### 1. **CommentItem.tsx**
**Location**: `apps/web/src/components/CommentItem.tsx`

**Changes Needed**:
- Update prop type from `RuleSpecComment` to `RuleCommentDto`
- Add support for `replies` array (nested comments)
- Add resolution UI (show/hide resolved comments)
- Add resolve/unresolve buttons for admin/editors
- Add reply button for threading
- Handle `mentionedUserIds` for @mention highlighting
- Replace `atomId` with `lineNumber`

**New Features to Implement**:
```typescript
interface CommentItemProps {
  comment: RuleCommentDto;
  onReply?: (commentId: string) => void;      // NEW
  onResolve?: (commentId: string) => void;    // NEW
  onUnresolve?: (commentId: string) => void;  // NEW
  depth?: number;                              // NEW - for threading UI
  maxDepth?: number;                           // NEW - default 5
}
```

#### 2. **CommentThread.tsx**
**Location**: `apps/web/src/components/CommentThread.tsx`

**Changes Needed**:
- Handle hierarchical comment structure (recursive rendering)
- Add filtering for resolved/unresolved comments
- Implement max thread depth visual indicator
- Add "Show resolved" toggle
- Update state management for nested replies

#### 3. **Version Detail Page** (if exists)
**Location**: TBD (wherever RuleSpec comments are displayed)

**Changes Needed**:
- Update API calls to use new endpoints
- Handle new response structure (array instead of object with `totalComments`)
- Implement threading UI
- Add resolution workflow

---

## 5. Test Migration

### E2E Tests: `apps/web/e2e/versions.spec.ts`

#### Lines to Update

**Lines 340-345**: GET comments mock
```typescript
// OLD
await page.route(`${apiBase}/api/v1/games/demo-chess/rulespec/versions/v2/comments`, ...);

// NEW
await page.route(`${apiBase}/api/v1/rulespecs/demo-chess/v2/comments?includeResolved=true`, ...);
```

**Lines 370-372, 434, 444**: Comment CRUD mocks
```typescript
// OLD
await page.route(new RegExp(`${apiBase}/api/v1/games/demo-chess/rulespec/comments/.*`), ...);

// NEW - These endpoints don't exist yet, need backend implementation
// Temporarily mock new structure or wait for backend endpoints
```

**Lines 420, 498**: Comment creation/fetch
```typescript
// OLD
await page.waitForResponse(`${apiBase}/api/v1/games/demo-chess/rulespec/versions/v2/comments`);

// NEW
await page.waitForResponse(`${apiBase}/api/v1/rulespecs/demo-chess/v2/comments`);
```

### Unit Tests: `apps/web/src/lib/__tests__/api.test.ts`

**Lines 301, 323**: Update URLs in assertions
```typescript
// OLD
`http://localhost:8080/api/v1/games/${gameId}/rulespec/versions/${version}/comments?includeResolved=true`

// NEW
`http://localhost:8080/api/v1/rulespecs/${gameId}/${version}/comments?includeResolved=true`
```

---

## 6. Validation Checklist

### Pre-Migration

- [ ] Review all components using `ruleSpecComments` API
- [ ] Identify all places where `RuleSpecComment` type is used
- [ ] Plan UI for new threading features
- [ ] Plan UI for resolution workflow
- [ ] Backup current implementation (create feature branch)

### Migration Steps

#### Phase 1: Types
- [ ] Add new `RuleCommentDto` type to `types/domain.ts`
- [ ] Add new request types to `types/api.ts`
- [ ] Remove legacy types after migration complete

#### Phase 2: API Client
- [ ] Update `api.ts` to use new endpoints
- [ ] Add new methods: `createReply`, `resolveComment`, `unresolveComment`, `getCommentsForLine`
- [ ] Update return types from `RuleSpecCommentsResponse` to `RuleCommentDto[]`

#### Phase 3: Components
- [ ] Update `CommentItem.tsx` for threading support
- [ ] Update `CommentThread.tsx` for hierarchical rendering
- [ ] Add resolution UI (buttons, status indicators)
- [ ] Add reply UI (nested comment forms)
- [ ] Implement max depth validation (5 levels)

#### Phase 4: Tests
- [ ] Update E2E tests in `versions.spec.ts`
- [ ] Update unit tests in `api.test.ts`
- [ ] Add new tests for threading features
- [ ] Add new tests for resolution features

### Post-Migration Validation

- [ ] All comment operations work without errors
- [ ] Threading displays correctly (nested replies)
- [ ] Resolution workflow works (resolve/unresolve)
- [ ] No console errors related to type mismatches
- [ ] E2E tests pass
- [ ] Unit tests pass
- [ ] Visual regression tests pass (if applicable)

### Cleanup

- [ ] Remove all legacy type definitions
- [ ] Remove commented-out legacy code
- [ ] Update documentation
- [ ] Remove this migration guide (once complete)

---

## 🔄 Migration Phases (Recommended)

### Phase 1: Backend Endpoint Completion (Backend Team)
**Status**: ⏳ Pending

Add missing endpoints for comment update/delete:
- `PUT /api/v1/comments/{commentId}` → `UpdateRuleCommentCommand`
- `DELETE /api/v1/comments/{commentId}` → `DeleteRuleCommentCommand`

**Files to modify**:
- `apps/api/src/Api/Routing/RuleSpecEndpoints.cs`

### Phase 2: Frontend Types (Frontend Team)
**Status**: 🔴 Required

Add new types, keep legacy types temporarily for backward compatibility during migration.

### Phase 3: Frontend API Client (Frontend Team)
**Status**: 🔴 Required

Update `api.ts` to use new endpoints. Implement feature flag to switch between legacy/new.

### Phase 4: Frontend Components (Frontend Team)
**Status**: 🔴 Required

Update components to handle new data structures and features.

### Phase 5: Tests (Frontend Team)
**Status**: 🔴 Required

Update all tests to use new endpoints and data structures.

### Phase 6: Cleanup (Frontend Team)
**Status**: ⏳ After Phase 5

Remove legacy types and code once migration is complete and validated.

---

## 📞 Support & Questions

**Backend Migration Complete**: ✅ Completed 2025-11-17
**Frontend Migration Status**: 🔴 Awaiting implementation

**Issues & Questions**:
- Backend handlers: See `apps/api/src/Api/BoundedContexts/GameManagement/Application/Handlers/`
- Backend tests: See `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/`
- Domain events: `CreateRuleCommentCommand`, `ResolveRuleCommentCommand`, `UnresolveRuleCommentCommand`, `ReplyToRuleCommentCommand`

---

## 🎯 Key Benefits of EDIT-05 System

1. **Threading**: Nested replies with depth validation (max 5 levels)
2. **Resolution**: Mark comments as resolved with recursive resolution
3. **@Mentions**: Tag users in comments with automatic extraction
4. **Line Anchoring**: Attach comments to specific line numbers
5. **Authorization**: Granular owner/admin permissions
6. **Circular Protection**: Prevents infinite loops in reply chains
7. **Domain Events**: Full audit trail for all comment operations

---

## 📚 Related Documentation

- Backend ADR: `docs/01-architecture/adr/adr-edit-05-enhanced-comments.md` (if exists)
- Backend Tests: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/*RuleComment*.cs`
- Backend Handlers: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Handlers/*RuleComment*.cs`
- API Specification: `docs/03-api/board-game-ai-api-specification.md` (update needed)

---

**End of Migration Guide**

