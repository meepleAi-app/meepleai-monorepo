# Demo Users Removed - Testing Guide Update

**Date**: 2025-11-30
**Status**: ✅ Complete

## Summary

All demo user seeding and demo-related UI components have been removed from the MeepleAI codebase. Tests now use **mock authentication** instead of relying on seeded demo users in the database.

## What Was Removed

### Frontend Components
- ❌ `DemoCredentialsHint` component (credential display UI)
- ❌ Demo credentials section in `AuthModal`
- ❌ All references to `showDemoCredentials` prop

### Backend Seeding
- ❌ `EnsureTestUserExistsAsync()` function (77 lines removed from `Program.cs`)
- ❌ Demo user seeding for `user@meepleai.dev`, `editor@meepleai.dev`
- ❌ `/admin/seed` endpoint
- ❌ `CreateDemoRuleSpecCommand` and handler

### Database
- ❌ No more automatic seeding of demo users
- ✅ Only `admin@meepleai.dev` is created from `INITIAL_ADMIN_EMAIL` env var

## How Tests Work Now

### E2E Tests (Playwright)
Tests use **mock authentication fixtures** defined in `apps/web/e2e/fixtures/auth.ts`:

```typescript
// Mock authentication - NO database dependency
await setupMockAuth(page, 'Admin', 'admin-test@example.com');
await setupMockAuth(page, 'Editor', 'editor-test@example.com');
await setupMockAuth(page, 'User', 'user-test@example.com');
```

### Backend Integration Tests
Use `TestConstants.cs` with test fixture emails:

```csharp
// Test fixture emails (mock only - not seeded)
public const string AdminEmail = "admin-test@example.com";
public const string EditorEmail = "editor-test@example.com";
public const string UserEmail = "user-test@example.com";
```

### API Tests (Postman/Newman)
Create users programmatically in test setup:

```bash
# Create test user via API
curl -X POST http://localhost:8080/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email": "test@example.com", "password": "TestPassword123!", "displayName": "Test User"}'
```

## Migration Guide for Existing Tests

### Before (Relied on Demo Users)
```typescript
// ❌ OLD - Relied on seeded demo users
await page.fill('input[name="email"]', 'user@meepleai.dev');
await page.fill('input[name="password"]', 'Demo123!');
await page.click('button[type="submit"]');
```

### After (Use Mock Auth)
```typescript
// ✅ NEW - Use mock authentication
import { setupMockAuth } from '@/e2e/fixtures/auth';

await setupMockAuth(page, 'User', 'test-user@example.com');
await page.goto('/dashboard'); // Already authenticated
```

## Updated Documentation

The following documentation files may still reference demo credentials for **illustrative purposes only**:

- `docs/02-development/testing/manual-testing-guide.md`
- `docs/02-development/testing/core/testing-checkpoint-guide.md`
- `docs/02-development/guides/local-debugging-guide.md`

**Important**: Any credentials shown in these docs are **examples only**. Actual tests use mock authentication.

## Environment Variables

### Production/Staging
Only one user is created automatically:

```bash
INITIAL_ADMIN_EMAIL=your-admin@company.com
INITIAL_ADMIN_PASSWORD=YourSecurePassword123!
```

### Development/Testing
**No automatic user seeding**. Create users via:
1. Registration API endpoint
2. Admin user management UI
3. Test fixtures (for automated tests)

## Benefits

✅ **Security**: No hardcoded demo credentials
✅ **Performance**: Faster test execution (no DB dependency)
✅ **Isolation**: Tests don't interfere with each other
✅ **Flexibility**: Easy to test different user states
✅ **Maintainability**: No demo user cleanup needed

## Questions?

For test-related questions, see:
- **E2E Testing**: `apps/web/e2e/README.md`
- **Backend Testing**: `apps/api/tests/Api.Tests/README.md`
- **Test Fixtures**: `apps/web/e2e/fixtures/auth.ts`
