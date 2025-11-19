# Issue: Eliminate `any` Types

**ID**: TS-001
**Category**: Type Safety
**Priority**: 🔴 **CRITICAL**
**Status**: 🔴 Open
**Created**: 2025-11-19

---

## 📋 Summary

Eliminare tutte le occorrenze di `any` type nel codebase frontend per garantire type safety completa. Attualmente ci sono **21 files** con uso di `any`, principalmente in:
- Admin pages (error handling)
- API client methods (response types)
- Event handlers (React events)

---

## 🎯 Problem Statement

### Current State
```typescript
// ❌ PROBLEMA: any type bypassa type checking
catch (err: any) {
  toast.error(err.message); // No compile-time safety
}

async loginWithApiKey(apiKey: string): Promise<{ user: any; message: string }> {
  // ^^^ user type è any - nessuna type safety
}

const handleSubmit = (e: any) => {
  // Generic event handler - no autocomplete
}
```

### Impact
- ⚠️ **Runtime errors** - Possibili crash per proprietà undefined
- ⚠️ **No IntelliSense** - IDE autocomplete non funziona
- ⚠️ **Refactoring rischioso** - No compile-time checks
- ⚠️ **Type safety compromessa** - Bypassa TypeScript strict mode

### Risks
- **High**: Production bugs da type errors
- **Medium**: Developer productivity ridotta
- **Low**: Onboarding più difficile per nuovi dev

---

## 📊 Affected Files

### Admin Pages (10 files) - 20 occurrences
```
apps/web/src/app/admin/
├── prompts/page.tsx (3 any)
├── prompts/[id]/compare/page.tsx (1 any)
├── prompts/[id]/versions/new/page.tsx (2 any)
├── prompts/[id]/versions/[versionId]/page.tsx (3 any)
├── prompts/[id]/page.tsx (3 any)
├── prompts/[id]/audit/page.tsx (2 any)
├── n8n-templates/page.tsx (1 any)
├── analytics/page.tsx (2 any)
├── bulk-export/page.tsx (2 any)
└── versions/page.tsx (1 any)
```

### API Clients (3 files) - 5 occurrences
```
apps/web/src/lib/api/clients/
├── authClient.ts (2 any)
├── chatClient.ts (2 any)
└── gamesClient.ts (1 any)
```

### Components (8 files) - ~10 occurrences
Various component files with generic event handlers

---

## 🔧 Solution

### 1. Error Handling Pattern

#### Before
```typescript
// ❌ Admin page error handling
try {
  const data = await api.prompts.getAll();
  setPrompts(data);
} catch (err: any) {
  toast.error(err.message);
  console.error(err);
}
```

#### After
```typescript
// ✅ Type-safe error handling
interface ApiError {
  message: string;
  statusCode?: number;
  details?: Record<string, unknown>;
}

function isApiError(error: unknown): error is ApiError {
  return (
    typeof error === 'object' &&
    error !== null &&
    'message' in error &&
    typeof (error as any).message === 'string'
  );
}

try {
  const data = await api.prompts.getAll();
  setPrompts(data);
} catch (err: unknown) {
  const errorMessage = err instanceof Error
    ? err.message
    : 'An unexpected error occurred';

  const apiError: ApiError = {
    message: errorMessage,
    statusCode: isApiError(err) ? err.statusCode : undefined,
    details: isApiError(err) ? err.details : undefined,
  };

  toast.error(apiError.message);
  logger.error('Failed to fetch prompts', err, { apiError });
}
```

### 2. API Client Types

#### Before
```typescript
// ❌ authClient.ts
async loginWithApiKey(apiKey: string): Promise<{ user: any; message: string }> {
  const response = await httpClient.post<{ user: any; message: string }>(...);
  return response;
}
```

#### After
```typescript
// ✅ Type-safe with Zod
import { UserProfile, UserProfileSchema } from '../schemas';

// Define response schema
const LoginWithApiKeyResponseSchema = z.object({
  user: UserProfileSchema,
  message: z.string(),
});

type LoginWithApiKeyResponse = z.infer<typeof LoginWithApiKeyResponseSchema>;

async loginWithApiKey(apiKey: string): Promise<LoginWithApiKeyResponse> {
  const response = await httpClient.post<LoginWithApiKeyResponse>(
    '/api/v1/auth/apikey/login',
    { apiKey },
    LoginWithApiKeyResponseSchema // Runtime validation
  );
  setStoredApiKey(apiKey.trim());
  return response;
}
```

### 3. React Event Handlers

#### Before
```typescript
// ❌ Generic event handler
const handleSubmit = (e: any) => {
  e.preventDefault();
  // No autocomplete for event properties
};

const handleChange = (e: any) => {
  setValue(e.target.value);
};
```

#### After
```typescript
// ✅ Proper React types
import { FormEvent, ChangeEvent, MouseEvent } from 'react';

const handleSubmit = (e: FormEvent<HTMLFormElement>) => {
  e.preventDefault();
  // Full IntelliSense support
};

const handleChange = (e: ChangeEvent<HTMLInputElement>) => {
  setValue(e.target.value);
  // TypeScript knows e.target is HTMLInputElement
};

const handleClick = (e: MouseEvent<HTMLButtonElement>) => {
  // Button-specific event handling
};
```

---

## 📝 Implementation Checklist

### Phase 1: Admin Pages (8 hours)
- [ ] Create `ApiError` type definition
- [ ] Create `isApiError` type guard
- [ ] Update `apps/web/src/app/admin/prompts/page.tsx`
- [ ] Update `apps/web/src/app/admin/prompts/[id]/compare/page.tsx`
- [ ] Update `apps/web/src/app/admin/prompts/[id]/versions/new/page.tsx`
- [ ] Update `apps/web/src/app/admin/prompts/[id]/versions/[versionId]/page.tsx`
- [ ] Update `apps/web/src/app/admin/prompts/[id]/page.tsx`
- [ ] Update `apps/web/src/app/admin/prompts/[id]/audit/page.tsx`
- [ ] Update `apps/web/src/app/admin/n8n-templates/page.tsx`
- [ ] Update `apps/web/src/app/admin/analytics/page.tsx`
- [ ] Update `apps/web/src/app/admin/bulk-export/page.tsx`
- [ ] Update `apps/web/src/app/versions/page.tsx`
- [ ] Run tests: `pnpm test --testPathPattern=admin`

### Phase 2: API Clients (4 hours)
- [ ] Update `authClient.ts` - loginWithApiKey response type
- [ ] Update `authClient.ts` - other methods with any
- [ ] Update `chatClient.ts` - message types
- [ ] Update `gamesClient.ts` - game types
- [ ] Add Zod schemas for all responses
- [ ] Run tests: `pnpm test --testPathPattern=api`

### Phase 3: Components (6 hours)
- [ ] Identify all generic event handlers
- [ ] Replace with proper React event types
- [ ] Update component props interfaces
- [ ] Run tests: `pnpm test`

### Phase 4: Validation (2 hours)
- [ ] Enable ESLint rule: `"@typescript-eslint/no-explicit-any": "error"`
- [ ] Run typecheck: `pnpm typecheck`
- [ ] Fix any remaining type errors
- [ ] Run full test suite: `pnpm test`
- [ ] Code review

---

## ✅ Acceptance Criteria

### Must Have
- [ ] Zero `any` types in production code
- [ ] ESLint rule `@typescript-eslint/no-explicit-any` set to `"error"`
- [ ] TypeScript strict mode passes (`pnpm typecheck`)
- [ ] All tests pass (`pnpm test`)
- [ ] No new TypeScript errors introduced

### Should Have
- [ ] All API responses have Zod schemas
- [ ] Type guards for complex error handling
- [ ] JSDoc comments for complex types

### Nice to Have
- [ ] Shared types in `@/types` directory
- [ ] Type utility functions for common patterns

---

## 🧪 Testing Strategy

### Unit Tests
```typescript
// Test type guards
describe('isApiError', () => {
  it('should return true for API error objects', () => {
    const error = { message: 'Error', statusCode: 500 };
    expect(isApiError(error)).toBe(true);
  });

  it('should return false for Error instances', () => {
    const error = new Error('Error');
    expect(isApiError(error)).toBe(false);
  });
});
```

### Integration Tests
```typescript
// Test API client type safety
describe('authClient.loginWithApiKey', () => {
  it('should return typed UserProfile', async () => {
    const response = await api.auth.loginWithApiKey('test-key');

    // TypeScript should know these properties exist
    expect(response.user.id).toBeDefined();
    expect(response.user.email).toBeDefined();
    expect(response.message).toBeDefined();
  });
});
```

### Type Tests
```typescript
// apps/web/src/types/__tests__/type-tests.ts
import { expectType } from 'tsd';
import { UserProfile } from '@/types';

// Compile-time type checking
const user: UserProfile = {
  id: 'test',
  email: 'test@example.com',
  displayName: 'Test',
  role: 'User',
};

expectType<string>(user.id);
expectType<string>(user.email);
```

---

## 📊 Effort Estimation

| Phase | Files | Effort |
|-------|-------|--------|
| Admin Pages | 10 | 8h |
| API Clients | 3 | 4h |
| Components | 8 | 6h |
| Validation | - | 2h |
| **TOTAL** | **21** | **20h** |

---

## 🔗 Related Issues

- [Structured Logging](../logging/structured-logging.md) - Logger needs proper error types
- [API Client Types](https://github.com/yourusername/meepleai/issues/xxx) - Backend schema alignment

---

## 📚 References

- [TypeScript Handbook - Type Guards](https://www.typescriptlang.org/docs/handbook/2/narrowing.html)
- [React TypeScript Cheatsheet](https://react-typescript-cheatsheet.netlify.app/)
- [Zod Documentation](https://zod.dev/)
- [ESLint TypeScript Rules](https://typescript-eslint.io/rules/no-explicit-any/)

---

## 👥 Assignee

**Developer**: TBD
**Reviewer**: Frontend Lead
**QA**: QA Engineer

---

**Last Updated**: 2025-11-19
**Status**: 🔴 Open
