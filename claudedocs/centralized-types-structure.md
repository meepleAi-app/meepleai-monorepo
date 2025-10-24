# Centralized Types Structure

**Created**: 2025-10-24
**Status**: Phase 1 Complete - Foundation Established
**Location**: `apps/web/src/types/`

## Overview

Centralized type definitions for the MeepleAI web application, providing a single source of truth for all TypeScript types across the codebase.

## Structure

```
src/types/
├── index.ts           # Central export point - import all types from here
├── auth.ts            # Authentication & session management types
├── domain.ts          # Core business domain types (Game, RuleSpec, etc.)
├── api.ts             # API request/response contracts
└── pdf.ts             # PDF processing types (existing, now integrated)
```

## Usage

### Import Types

```typescript
// ✅ Recommended: Import from central index
import { AuthUser, Game, RuleSpec, ApiError } from '@/types';

// ❌ Avoid: Direct imports from individual files
import { AuthUser } from '@/types/auth';
```

### Available Types

#### Authentication Types (`auth.ts`)
- `AuthUser` - Authenticated user information
- `AuthResponse` - Login response with user and expiration
- `SessionStatusResponse` - Session status (AUTH-05)
- `UserRole` - User roles enum type
- **Helpers**: `hasRole()`, `canEdit()` - Authorization helpers

#### Domain Types (`domain.ts`)
- `Game` - Game entity
- `Agent` - AI assistant for a game
- `Chat` - Chat session entity
- `ChatMessage` - Individual chat message (with CHAT-06 edit/delete support)
- `ChatWithHistory` - Chat with full message history
- `RuleAtom` - Individual rule from rulebook
- `RuleSpec` - Complete game rules specification
- `RuleSpecComment` - Comments on rule specs
- `Snippet` - RAG retrieval source snippet
- `Message` - UI message (combines user/assistant messages)
- `QaResponse` - Q&A agent response
- `SetupStep` - Setup guide step
- `SetupGuideResponse` - Complete setup guide

#### API Types (`api.ts`)
- `RuleSpecCommentsResponse` - RuleSpec comments list
- `CreateRuleSpecCommentRequest` - Create comment request
- `UpdateRuleSpecCommentRequest` - Update comment request
- `ChatMessageResponse` - Chat message from API
- `ExportFormat` - Chat export format type
- `ExportChatRequest` - Chat export request
- `TopQuestion` - Cache statistics top question
- `CacheStats` - Cache statistics (PERF-03)
- `ValidationResult` - Generic validation result
- `PdfValidationError` - PDF validation error details
- `ApiResponse<T>` - Generic API response wrapper
- `PaginatedResponse<T>` - Paginated response wrapper
- **Class**: `ApiError` - Enhanced error with correlation ID (PDF-06)
- **Helper**: `createApiError()` - Create ApiError from response

#### PDF Types (`pdf.ts`)
- `ProcessingStep` - PDF processing step enum
- `ProcessingProgress` - Processing progress response
- **Helpers**: `isProcessingComplete()`, `getStepLabel()`, `getStepOrder()`

## Benefits

### 1. Single Source of Truth
- All types defined in one location
- No duplicate type definitions
- Consistent naming across codebase

### 2. Better IDE Support
- Autocomplete shows all available types
- Jump to definition leads to centralized location
- Easier to discover existing types

### 3. Improved Maintainability
- Changes to types in one place
- Clear ownership and organization
- Easier to refactor and evolve

### 4. Type Safety
- Shared types ensure API contract consistency
- Frontend/backend type alignment
- Catch type mismatches at compile time

## Migration Strategy

### Phase 1: Foundation (Complete ✅)
- Created centralized type files
- Organized types by domain
- Created central export index
- Verified types compile

### Phase 2: Gradual Migration (Next Steps)
- Update most-used files first (lib/api.ts, pages/chat.tsx, pages/upload.tsx)
- Replace inline type definitions with imports from `@/types`
- Run tests after each migration to ensure no breakage
- Low-risk incremental approach

### Phase 3: Cleanup (Future)
- Remove duplicate type definitions
- Consolidate similar types
- Document type relationships
- Update test fixtures to use centralized types

## Examples

### Before Centralization
```typescript
// pages/chat.tsx
type AuthUser = {
  id: string;
  email: string;
  displayName?: string | null;
  role: string;
};

type Game = {
  id: string;
  name: string;
};

// pages/upload.tsx
interface AuthUser {  // ❌ Duplicate!
  id: string;
  email: string;
  displayName?: string | null;
  role: string;
}

interface GameSummary {  // ❌ Different name for same thing
  id: string;
  name: string;
  createdAt: string;
}
```

### After Centralization
```typescript
// pages/chat.tsx
import { AuthUser, Game, Message, QaResponse } from '@/types';

// pages/upload.tsx
import { AuthUser, Game, RuleSpec, RuleAtom } from '@/types';

// ✅ Single source of truth
// ✅ No duplication
// ✅ Consistent naming
```

## Current Status

### Completed ✅
- Created 4 type files (auth, domain, api, index)
- Integrated existing pdf.ts into centralized structure
- Added utility helpers for common operations
- Verified all types compile without errors
- Documented structure and usage

### Next Steps 📋
1. Update `lib/api.ts` to use centralized types
2. Update major pages (chat.tsx, upload.tsx, editor.tsx)
3. Update test fixtures to import from `@/types`
4. Remove duplicate type definitions incrementally
5. Run full test suite after migrations

### Metrics
- **Type Files Created**: 4 (auth, domain, api, index)
- **Total Exported Types**: 40+
- **Utility Helpers**: 6 (hasRole, canEdit, isProcessingComplete, etc.)
- **Compilation Status**: ✅ All types compile successfully

## Guidelines

### When to Add New Types

**✅ Add to centralized types:**
- Shared across multiple files
- Part of API contract (request/response)
- Core domain entities
- Common utility types

**❌ Keep as local types:**
- Component-specific props
- Internal component state
- Single-file utilities
- Test-only types

### Naming Conventions

- **Interfaces**: PascalCase (e.g., `AuthUser`, `RuleSpec`)
- **Type Aliases**: PascalCase (e.g., `UserRole`, `ExportFormat`)
- **Helpers**: camelCase (e.g., `hasRole()`, `canEdit()`)
- **Response Types**: Suffix with `Response` (e.g., `AuthResponse`, `SetupGuideResponse`)
- **Request Types**: Suffix with `Request` (e.g., `CreateRuleSpecCommentRequest`)

### File Organization

- **auth.ts**: Authentication, authorization, session management
- **domain.ts**: Core business entities (Game, Chat, RuleSpec, etc.)
- **api.ts**: API contracts (request/response types, errors)
- **pdf.ts**: PDF processing (specialized domain)
- **index.ts**: Re-exports (never add types here directly)

## Related Documents
- Frontend Improvements Action Plan: `claudedocs/FRONTEND-IMPROVEMENTS-ACTION-PLAN.md`
- Frontend Architecture Review: `claudedocs/frontend-architecture-review-2025-10-24.md`
