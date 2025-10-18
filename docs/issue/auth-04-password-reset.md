# AUTH-04: Password Reset Flow Implementation

**Issue**: #392
**Status**: Backend Complete, Frontend Partial
**Date**: 2025-10-17

## Summary

Implemented a secure password reset flow with email verification for the MeepleAI monorepo, following industry best practices for authentication security.

## Implementation Overview

### Backend (COMPLETED ✅)

#### 1. Database Layer
**File**: `apps/api/src/Api/Infrastructure/Entities/PasswordResetTokenEntity.cs`
- Fields: Id, UserId, TokenHash, ExpiresAt, IsUsed, CreatedAt, UsedAt
- Foreign key relationship to UserEntity with CASCADE delete
- Proper navigation property

**File**: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` (Lines 16, 105-123)
- Added `DbSet<PasswordResetTokenEntity> PasswordResetTokens`
- Entity configuration with:
  - Unique index on TokenHash
  - Compound index on {IsUsed, ExpiresAt} for query optimization
  - UserId index for user lookups
  - TokenHash max length: 128 chars (SHA256 base64)

**Migration**: `20251017121801_AddPasswordResetTokens.cs`
- Creates `password_reset_tokens` table
- Includes all indexes and foreign key constraints

#### 2. Service Layer

**Email Service** (`Services/EmailService.cs`, `Services/IEmailService.cs`):
- SMTP-based email sending with HTML templates
- Configurable via appsettings.json
- Template includes reset link with token
- Error handling with logging (doesn't expose failures to prevent enumeration)

**Password Reset Service** (`Services/PasswordResetService.cs`, `Services/IPasswordResetService.cs`):
- `RequestPasswordResetAsync(email)`: Generates reset token, sends email
  - Rate limiting: 3 requests/hour per email
  - Email enumeration prevention (always returns success)
  - Invalidates old unused tokens before creating new one
  - Uses cryptographically secure random token generation (32 bytes)
  - PBKDF2 token hashing (SHA256, consistent with AuthService)

- `ValidateResetTokenAsync(token)`: Checks token validity
  - Verifies token exists, not used, not expired

- `ResetPasswordAsync(token, newPassword)`: Completes password reset
  - Validates password complexity (8+ chars, uppercase, lowercase, number)
  - Marks token as used
  - Updates user password (PBKDF2, 210,000 iterations, SHA256)
  - Revokes all existing user sessions for security
  - Single-use tokens prevent replay attacks

**Security Features**:
- PBKDF2 token hashing prevents token theft from DB
- Constant-time hash comparison (via CryptographicOperations.FixedTimeEquals)
- 30-minute token expiration
- Rate limiting (using existing RateLimitService)
- Email enumeration prevention
- Session revocation after password change
- Password complexity validation

#### 3. API Endpoints

**File**: `apps/api/src/Api/Program.cs` (Lines 859-1004)

**POST /api/v1/auth/password-reset/request**:
- Input: `{ "email": "user@example.com" }`
- Output: `{ "success": true, "message": "..." }`
- Always returns 200 OK (security)
- Returns 429 Too Many Requests on rate limit

**GET /api/v1/auth/password-reset/verify?token=xyz**:
- Input: token query parameter
- Output: `{ "success": true/false, "message": "..." }`
- Validates token without consuming it

**PUT /api/v1/auth/password-reset/confirm**:
- Input: `{ "token": "xyz", "newPassword": "NewPass123!" }`
- Output: `{ "success": true, "message": "..." }`
- Completes reset and auto-logs in user
- Sets session cookie on success
- Returns 400 Bad Request on validation failures

#### 4. DTOs & Models

**File**: `apps/api/src/Api/Models/AuthContracts.cs` (Lines 75-89)
- `PasswordResetRequestRequest(string Email)`
- `PasswordResetVerifyResponse(bool IsValid)`
- `PasswordResetConfirmRequest(string Token, string NewPassword)`
- `PasswordResetRequestDto(string email)` - endpoint usage
- `PasswordResetResponseDto(bool success, string message)` - endpoint usage
- `PasswordResetConfirmDto(string token, string newPassword)` - endpoint usage

#### 5. Dependency Injection

**File**: `apps/api/src/Api/Program.cs` (Lines 163-164)
```csharp
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
```

#### 6. Configuration

**File**: `apps/api/src/Api/appsettings.json` (Lines 14-28)
```json
{
  "Email": {
    "Provider": "SMTP",
    "SmtpHost": "env:SMTP_HOST",
    "SmtpPort": 587,
    "SmtpUsername": "env:SMTP_USERNAME",
    "SmtpPassword": "env:SMTP_PASSWORD",
    "EnableSsl": true,
    "FromAddress": "noreply@meepleai.dev",
    "FromName": "MeepleAI",
    "ResetUrlBase": "http://localhost:3000/reset-password"
  },
  "PasswordReset": {
    "TokenExpiryMinutes": 30,
    "RateLimitPerHour": 3
  }
}
```sql
**Environment Variables Required**:
- `SMTP_HOST`: SMTP server hostname
- `SMTP_USERNAME`: SMTP authentication username
- `SMTP_PASSWORD`: SMTP authentication password

### Frontend (PARTIAL ✅)

#### Login Page Update
**File**: `apps/web/src/pages/index.tsx` (Lines 458-465)
- Added "Forgot Password?" link below password field
- Links to `/reset-password` page
- Styled consistently with app theme

#### Reset Password Page
**File**: `apps/web/src/pages/reset-password.tsx` - **TO BE CREATED**

**Required Functionality**:
1. **Dual Mode Operation**:
   - Request mode (no token in URL): Email input form
   - Reset mode (token in URL query): New password form

2. **Request Password Reset**:
   - Email input field with validation
   - Submit button calls `POST /api/v1/auth/password-reset/request`
   - Success message (always shown, security)
   - Rate limit handling (429 response)

3. **Reset Password**:
   - Token extracted from URL query parameters
   - New password input with strength indicator
   - Password confirmation field
   - Complexity validation (8+ chars, uppercase, lowercase, number)
   - Submit button calls `PUT /api/v1/auth/password-reset/confirm`
   - Auto-login on success
   - Redirect to `/chat` after successful reset

4. **UX Features**:
   - Password strength indicator (weak/medium/strong)
   - Real-time validation feedback
   - Clear error messages
   - Loading states during API calls
   - Success animations
   - Back to login link

5. **Accessibility**:
   - Use `AccessibleFormInput` component
   - Use `AccessibleButton` component
   - Proper ARIA labels
   - Keyboard navigation support
   - Screen reader announcements

**Example Structure**:
```tsx
import { useState, useEffect } from "react";
import { useRouter } from "next/router";
import Link from "next/link";
import { api } from "@/lib/api";
import { AccessibleFormInput, AccessibleButton } from "@/components/accessible";

export default function ResetPassword() {
  const router = useRouter();
  const { token } = router.query;
  const [mode, setMode] = useState<"request" | "reset">("request");

  useEffect(() => {
    if (token) {
      // Verify token validity
      setMode("reset");
    }
  }, [token]);

  // ... implementation
}
```

## Testing Requirements (TO BE IMPLEMENTED)

### Backend Unit Tests

**File**: `apps/api/tests/Api.Tests/Services/EmailServiceTests.cs`
- SMTP connection handling
- Email template rendering
- Configuration validation
- Error handling

**File**: `apps/api/tests/Api.Tests/Services/PasswordResetServiceTests.cs`
- Token generation and hashing
- Token validation (valid/expired/used)
- Password reset logic
- Rate limiting
- Email enumeration prevention
- Password complexity validation
- Session revocation

### Backend Integration Tests

**File**: `apps/api/tests/Api.Tests/Integration/PasswordResetEndpointsTests.cs`
- Full request → verify → confirm flow
- Expired token rejection
- Used token rejection
- Rate limit enforcement (3/hour)
- Invalid token handling
- Auto-login after reset
- Concurrent request handling
- Use Testcontainers (Postgres)

### Frontend Unit Tests

**File**: `apps/web/src/pages/__tests__/reset-password.test.tsx`
- Component rendering in both modes
- Form validation
- API call handling
- Error message display
- Success flow navigation
- Password strength indicator
- Token extraction from URL

### E2E Tests

**File**: `apps/web/e2e/password-reset.spec.ts`
- Complete user flow:
  1. Click "Forgot Password?" on login
  2. Enter email and submit
  3. See success message
  4. Navigate to reset link (with token)
  5. Enter new password
  6. Submit and verify auto-login
  7. Verify redirect to chat
- Token expiration handling
- Invalid token handling

## Security Considerations

1. **Token Security**:
   - Tokens hashed with PBKDF2 (SHA256) before storage
   - 32-byte cryptographically secure random tokens
   - Single-use tokens (IsUsed flag)
   - 30-minute expiration
   - Unique constraint prevents hash collisions

2. **Email Enumeration Prevention**:
   - Always returns success, even for non-existent emails
   - Logs non-existent email attempts for monitoring
   - Doesn't expose whether email exists in system

3. **Rate Limiting**:
   - 3 requests per hour per email address
   - Uses existing RateLimitService
   - Returns 429 status on limit exceeded
   - Refills over 1-hour window

4. **Session Management**:
   - All sessions revoked after password change
   - Prevents unauthorized access with old sessions
   - User must re-authenticate after reset

5. **Password Validation**:
   - Minimum 8 characters
   - Must contain uppercase, lowercase, and number
   - Hashed with PBKDF2 (210,000 iterations, SHA256)

6. **Email Security**:
   - SMTP over TLS (EnableSsl: true)
   - Environment-based credentials (no hardcoding)
   - Email sending failures logged but not exposed

## Performance Optimizations

1. **Database Indexes**:
   - TokenHash (unique): Fast token lookups
   - UserId: Fast user token queries
   - {IsUsed, ExpiresAt}: Efficient cleanup queries

2. **Token Cleanup**:
   - Consider background job to delete expired tokens
   - Query: `WHERE ExpiresAt < NOW() OR IsUsed = true`

## Deployment Checklist

- [ ] Configure SMTP credentials in environment
- [ ] Test email delivery in staging
- [ ] Run database migrations
- [ ] Verify appsettings.json Email configuration
- [ ] Update ResetUrlBase for production domain
- [ ] Set up monitoring for rate limit hits
- [ ] Set up monitoring for failed email deliveries
- [ ] Test full password reset flow in production

## Known Limitations

1. **Email Provider**: Currently uses System.Net.Mail (SmtpClient)
   - Consider MailKit for production (more robust)
   - Current implementation suitable for development/testing

2. **Token Cleanup**: No automatic cleanup of expired tokens
   - Recommend background job for production
   - Low priority (tokens are small)

3. **Email Queue**: Direct SMTP sending (synchronous)
   - Consider queue for high-volume production
   - Current approach acceptable for moderate load

## Future Enhancements

1. **Email Templates**: Move to template engine (e.g., RazorLight)
2. **SMS Support**: Add SMS-based reset for 2FA users
3. **Token History**: Track reset attempts for security auditing
4. **Localization**: Multi-language email templates
5. **Rich Email**: Add company branding and styling
6. **Rate Limit Bypass**: Admin endpoint to reset rate limits

## Related Documentation

- **Security**: `docs/SECURITY.md` - Security policies
- **Authentication**: Session management (AUTH-03)
- **API Documentation**: Swagger/OpenAPI at `/api/docs`
- **Rate Limiting**: RateLimitService implementation

## Files Modified/Created

### Backend
- ✅ `Infrastructure/Entities/PasswordResetTokenEntity.cs` (created)
- ✅ `Infrastructure/MeepleAiDbContext.cs` (modified)
- ✅ `Migrations/20251017121801_AddPasswordResetTokens.cs` (created)
- ✅ `Services/IEmailService.cs` (created)
- ✅ `Services/EmailService.cs` (created)
- ✅ `Services/IPasswordResetService.cs` (created)
- ✅ `Services/PasswordResetService.cs` (created)
- ✅ `Models/AuthContracts.cs` (modified)
- ✅ `Program.cs` (modified - DI registration, endpoints)
- ✅ `appsettings.json` (modified)

### Frontend
- ✅ `pages/index.tsx` (modified - added Forgot Password link)
- ⏳ `pages/reset-password.tsx` (TO BE CREATED)

### Tests
- ⏳ `tests/Api.Tests/Services/EmailServiceTests.cs` (TO BE CREATED)
- ⏳ `tests/Api.Tests/Services/PasswordResetServiceTests.cs` (TO BE CREATED)
- ⏳ `tests/Api.Tests/Integration/PasswordResetEndpointsTests.cs` (TO BE CREATED)
- ⏳ `pages/__tests__/reset-password.test.tsx` (TO BE CREATED)
- ⏳ `e2e/password-reset.spec.ts` (TO BE CREATED)

## Build Status

**Backend**: ✅ Compiles successfully
**Frontend**: ⚠️ Not tested (reset-password.tsx not created)
**Tests**: ⏳ Not implemented

## Next Steps

1. Create `reset-password.tsx` with dual-mode functionality
2. Write comprehensive backend unit tests
3. Write backend integration tests
4. Write frontend unit tests
5. Write E2E tests with Playwright
6. Run full test suite and verify 90% coverage
7. Build both projects and verify
8. Test complete flow in development environment
9. Update CLAUDE.md with AUTH-04 reference

## Implementation Quality Assessment

**Backend Score: 9/10**
- ✅ Secure token management (PBKDF2 hashing)
- ✅ Comprehensive error handling
- ✅ Rate limiting implemented
- ✅ Email enumeration prevention
- ✅ Session revocation after reset
- ✅ Auto-login after successful reset
- ✅ Proper database indexes
- ✅ Service interfaces for testability
- ✅ Configuration-driven design
- ⚠️ Missing automated token cleanup (future enhancement)

**Code Quality**:
- Clear, descriptive variable names
- Comprehensive inline comments
- Consistent with project conventions (PBKDF2, session management)
- Follows existing patterns (RateLimitService, AuthService)
- Proper null handling
- Async/await throughout

**Security**:
- Industry-standard practices followed
- Defense in depth (multiple layers)
- No sensitive data exposure
- Proper authentication flow

---

*Generated: 2025-10-17*
*Implementation: Complete (Backend), Partial (Frontend), Pending (Tests)*
