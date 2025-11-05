# TEST-ISSUE-001: Increase Auth Component Test Coverage to 80%+

**Priority**: 🔴 CRITICAL
**Labels**: `critical`, `testing`, `security`, `auth`
**Estimated Effort**: 8-12 hours
**Current Coverage**: 30% statements, 0% branches, 20% functions
**Target Coverage**: 80%+ across all metrics

---

## Problem Statement

The authentication components (`components/auth/`) have critically low test coverage at 30%, with **0% branch coverage**. Authentication is a security-critical area that requires comprehensive testing to prevent security vulnerabilities and ensure proper OAuth flow handling.

### Current State

```
Coverage: components/auth
- Statements: 30% (3/10)
- Branches: 0% (0/4)  ⚠️ CRITICAL
- Functions: 20% (1/5)
- Lines: 30% (3/10)
```

### Impact

- **Security Risk**: Untested authentication paths
- **OAuth Vulnerabilities**: Missing error handling tests
- **User Experience**: Potential login failures
- **CI/CD**: Failing tests blocking deployments

---

## Affected Files

- `src/components/auth/OAuthButtons.tsx` (Primary concern)
- `src/components/auth/__tests__/OAuthButtons.test.tsx` (Currently failing)
- OAuth integration utilities
- OAuth error handling components

---

## Current Test Failures

```
FAIL src/components/auth/__tests__/OAuthButtons.test.tsx
```

- OAuth button rendering tests failing
- OAuth provider mocks not properly configured
- Integration tests missing
- Error scenario tests missing

---

## Acceptance Criteria

### Coverage Metrics
- [ ] 80%+ statement coverage
- [ ] 80%+ branch coverage (up from 0%)
- [ ] 80%+ function coverage
- [ ] All OAuth providers tested (Google, Discord, GitHub)

### Functional Testing
- [ ] OAuth button rendering for each provider
- [ ] OAuth button click handlers
- [ ] OAuth loading states
- [ ] OAuth error states
- [ ] OAuth success flow
- [ ] OAuth redirect handling
- [ ] OAuth state parameter validation

### Test Quality
- [ ] All tests passing
- [ ] No flaky tests
- [ ] Clear test descriptions
- [ ] Proper mocks and fixtures
- [ ] Edge cases covered

---

## Implementation Tasks

### Task 1: Fix OAuthButtons.test.tsx (4 hours)

**Current Issues**:
- Test suite completely failing
- OAuth provider mocks not set up correctly
- Missing test coverage for all providers

**Steps**:
1. Set up proper OAuth provider mocks
   ```typescript
   const mockOAuthProviders = {
     google: { clientId: 'mock-google', name: 'Google' },
     discord: { clientId: 'mock-discord', name: 'Discord' },
     github: { clientId: 'mock-github', name: 'GitHub' }
   };
   ```

2. Test button rendering for each provider
   ```typescript
   it('should render Google OAuth button', () => {
     render(<OAuthButtons />);
     expect(screen.getByRole('button', { name: /google/i })).toBeInTheDocument();
   });
   ```

3. Test click handlers
   ```typescript
   it('should call OAuth redirect on Google button click', async () => {
     const mockRedirect = jest.fn();
     // Setup mock
     render(<OAuthButtons onRedirect={mockRedirect} />);

     const googleButton = screen.getByRole('button', { name: /google/i });
     await userEvent.click(googleButton);

     expect(mockRedirect).toHaveBeenCalledWith('/api/v1/auth/oauth/google/login');
   });
   ```

4. Test error states
5. Test loading states

**Files to Modify**:
- `src/components/auth/__tests__/OAuthButtons.test.tsx`

---

### Task 2: Add OAuth Flow Tests (3 hours)

**Create New Test File**: `src/components/auth/__tests__/OAuthFlow.test.tsx`

**Test Scenarios**:

1. **Successful OAuth Redirect**
   ```typescript
   it('should initiate OAuth flow with correct redirect URL', async () => {
     // Test Google OAuth flow
     // Test Discord OAuth flow
     // Test GitHub OAuth flow
   });
   ```

2. **OAuth Callback Handling**
   ```typescript
   it('should handle OAuth callback with valid code', async () => {
     // Mock successful callback
     // Verify token exchange
     // Verify user session creation
   });
   ```

3. **OAuth Error Handling**
   ```typescript
   it('should handle OAuth error callback', async () => {
     // Mock error callback
     // Verify error message display
     // Verify no session created
   });
   ```

4. **State Parameter Validation**
   ```typescript
   it('should validate OAuth state parameter', async () => {
     // Test valid state
     // Test invalid state (CSRF protection)
     // Test missing state
   });
   ```

---

### Task 3: Add Integration Tests (3 hours)

**Create New Test File**: `src/components/auth/__tests__/OAuthIntegration.test.tsx`

**Test Scenarios**:

1. **Complete Login Flow**
   ```typescript
   it('should complete full OAuth login flow', async () => {
     // 1. Click OAuth button
     // 2. Mock redirect to provider
     // 3. Mock callback with code
     // 4. Verify session created
     // 5. Verify redirect to dashboard
   });
   ```

2. **Account Linking**
   ```typescript
   it('should link OAuth account to existing user', async () => {
     // Setup logged-in user
     // Initiate OAuth link
     // Verify account linked
     // Verify no duplicate user created
   });
   ```

3. **Account Unlinking**
   ```typescript
   it('should unlink OAuth account', async () => {
     // Setup user with linked OAuth account
     // Unlink account
     // Verify account removed
     // Verify user can still log in with password
   });
   ```

4. **Multiple Provider Support**
   ```typescript
   it('should support multiple OAuth providers on same account', async () => {
     // Link Google account
     // Link Discord account
     // Link GitHub account
     // Verify all providers linked
     // Verify can log in with any provider
   });
   ```

---

### Task 4: Edge Cases & Error Scenarios (2 hours)

**Create New Test File**: `src/components/auth/__tests__/OAuthErrors.test.tsx`

**Test Scenarios**:

1. **Network Failures**
   ```typescript
   it('should handle network error during OAuth redirect', async () => {
     // Mock network failure
     // Verify error message
     // Verify retry option
   });
   ```

2. **Invalid State Tokens**
   ```typescript
   it('should reject callback with invalid state token', async () => {
     // Mock callback with wrong state
     // Verify error message about CSRF
     // Verify no session created
   });
   ```

3. **Expired Tokens**
   ```typescript
   it('should handle expired OAuth tokens', async () => {
     // Mock expired token
     // Verify refresh attempt
     // Verify re-authentication prompt if refresh fails
   });
   ```

4. **Provider Unavailability**
   ```typescript
   it('should handle OAuth provider being unavailable', async () => {
     // Mock provider 500 error
     // Verify user-friendly error message
     // Verify alternative login options shown
   });
   ```

5. **Account Conflict**
   ```typescript
   it('should handle email already registered with different provider', async () => {
     // User exists with email from Google OAuth
     // Try to link Discord OAuth with same email
     // Verify appropriate handling (auto-link or error)
   });
   ```

---

## Testing Strategy

### Test Structure

```typescript
describe('OAuthButtons', () => {
  describe('Rendering', () => {
    // Button rendering tests
  });

  describe('User Interactions', () => {
    // Click handler tests
  });

  describe('State Management', () => {
    // Loading and error state tests
  });

  describe('Accessibility', () => {
    // A11y tests (aria-labels, keyboard navigation)
  });
});
```

### Mocking Strategy

1. **OAuth API Endpoints**
   ```typescript
   jest.mock('@/lib/api', () => ({
     auth: {
       oauthLogin: jest.fn(),
       oauthCallback: jest.fn(),
       oauthUnlink: jest.fn(),
     }
   }));
   ```

2. **Router**
   ```typescript
   const mockPush = jest.fn();
   jest.mock('next/router', () => ({
     useRouter: () => ({
       push: mockPush,
       query: {},
     }),
   }));
   ```

3. **Window Location**
   ```typescript
   delete window.location;
   window.location = { href: '', assign: jest.fn() } as any;
   ```

---

## Success Metrics

### Coverage Metrics
- Auth component coverage: 30% → 80%+
- Branch coverage: 0% → 80%+
- Function coverage: 20% → 80%+

### Test Metrics
- All OAuth tests passing: 0 → ~50 tests
- Test execution time: < 5 seconds per suite
- No flaky tests
- CI/CD pipeline green

### Quality Metrics
- Security: All OAuth flows validated
- Error Handling: All error scenarios covered
- User Experience: All user paths tested
- Documentation: All tests self-documenting

---

## Dependencies

- None (can start immediately)
- No blocking issues
- No infrastructure changes needed

---

## Risk Assessment

### High Risk Areas
1. **CSRF Protection**: Must thoroughly test state validation
2. **Token Handling**: Must test token expiration and refresh
3. **Account Linking**: Must test email conflict scenarios

### Mitigation
- Comprehensive test coverage (target: 80%+)
- Security-focused test scenarios
- Edge case coverage
- Code review by security team

---

## Definition of Done

- [ ] All 4 tasks completed
- [ ] OAuthButtons.test.tsx passing
- [ ] OAuthFlow.test.tsx created and passing
- [ ] OAuthIntegration.test.tsx created and passing
- [ ] OAuthErrors.test.tsx created and passing
- [ ] Coverage metrics met (80%+ all metrics)
- [ ] All tests passing in CI
- [ ] No test warnings or errors
- [ ] Code reviewed and approved
- [ ] Merged to main branch

---

## Related Issues

- TEST-ISSUE-002: Fix 125 Failing Frontend Tests (includes OAuthButtons.test.tsx)
- AUTH-06: OAuth 2.0 Authentication (implementation)
- AUTH-07: Two-Factor Authentication (related auth feature)

---

## References

- [OAuth Setup Guide](../docs/guide/oauth-setup-guide.md)
- [OAuth Security Docs](../docs/security/oauth-security.md)
- [Testing Guide](../docs/testing/test-writing-guide.md) (to be created)
- [CLAUDE.md](../CLAUDE.md) - Auth section

---

**Created**: 2025-11-05
**Status**: Ready for Assignment
**Assignee**: TBD
**Due Date**: Within 48 hours (CRITICAL)
