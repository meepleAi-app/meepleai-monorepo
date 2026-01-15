---
title: "[Testing] Add E2E Tests for OAuth Authentication Flows"
labels: testing, frontend, backend, oauth
assignees: ''
---

## Description

Add comprehensive end-to-end tests for all OAuth authentication flows (Google, Discord, GitHub) using Playwright to ensure social login functionality works correctly.

## Context

After implementing OAuth configuration validation (#issue-ref), we need to ensure the complete OAuth flows work end-to-end in the browser. Currently, OAuth providers are configured and validated via health checks, but we lack automated E2E tests.

## Acceptance Criteria

### Google OAuth Flow
- [ ] Test authorization URL generation (`/api/v1/auth/oauth/google/authorize`)
- [ ] Test redirect to Google login page
- [ ] Test successful authentication and callback
- [ ] Test user profile creation from Google data
- [ ] Test session creation after OAuth success

### Discord OAuth Flow
- [ ] Test authorization URL generation (`/api/v1/auth/oauth/discord/authorize`)
- [ ] Test redirect to Discord authorization page
- [ ] Test successful authentication and callback
- [ ] Test user profile creation from Discord data
- [ ] Test session creation after OAuth success

### GitHub OAuth Flow
- [ ] Test authorization URL generation (`/api/v1/auth/oauth/github/authorize`)
- [ ] Test redirect to GitHub authorization page
- [ ] Test successful authentication and callback
- [ ] Test user profile creation from GitHub data
- [ ] Test session creation after OAuth success

### Error Scenarios
- [ ] Test invalid client ID error handling
- [ ] Test invalid client secret error handling
- [ ] Test callback with invalid state parameter
- [ ] Test callback with invalid code
- [ ] Test user cancels OAuth flow

## Technical Details

### Test Location
- Create `apps/web/e2e/oauth-flows.spec.ts`

### Required Setup
- Mock OAuth provider responses (or use test OAuth apps)
- Configure test OAuth credentials in CI/CD
- Set up browser automation with Playwright

### Example Test Structure
```typescript
test.describe('OAuth Authentication', () => {
  test('Google OAuth flow completes successfully', async ({ page }) => {
    await page.goto('/login');
    await page.click('[data-testid="google-oauth-button"]');

    // Wait for redirect to Google
    await page.waitForURL(/accounts\.google\.com/);

    // Mock Google login (or use test account)
    // ...

    // Verify redirect back to app
    await page.waitForURL(/localhost:3000/);

    // Verify user is logged in
    await expect(page.locator('[data-testid="user-menu"]')).toBeVisible();
  });
});
```

## Dependencies

- Playwright installed and configured
- Test OAuth applications created for each provider
- CI/CD secrets configured for test credentials

## Related Issues

- #issue-ref - OAuth Configuration Validation Implementation
- #issue-ref - Health Check Enhancement

## Definition of Done

- [ ] E2E tests written for all 3 OAuth providers
- [ ] Error scenarios covered
- [ ] Tests pass in CI/CD
- [ ] Documentation updated in `docs/05-testing/`
- [ ] Test credentials configured in GitHub Secrets

## Estimated Effort

**Complexity**: Medium
**Time**: 4-6 hours
