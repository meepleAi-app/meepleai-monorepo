# Authentication Bounded Context - API Reference

**Gestione autenticazione, sessioni, OAuth, 2FA, e API keys**

---

## 📋 Responsabilità

- Registrazione e login utenti (email/password)
- Gestione sessioni (cookie-based con sliding expiration)
- OAuth 2.0 (Google, GitHub, Discord)
- Two-Factor Authentication (TOTP con backup codes)
- API Key generation, rotation, e revocation
- Password reset e email verification
- Account lockout e admin management
- Session management multi-device
- User profile e preferences

---

## 🏗️ Domain Model

### Aggregates

| Aggregate/Entity | Key Properties | Factory Methods |
|------------------|----------------|-----------------|
| **User** (Root) | Id, Email, PasswordHash, DisplayName, Role, EmailConfirmed, TwoFactorEnabled, BackupCodes | `Create()`, `EnableTwoFactor()`, `DisableTwoFactor()`, `GenerateApiKey()`, `LockAccount()` |
| **ApiKey** | Id, UserId, KeyHash, Name, Scopes, ExpiresAt, IsRevoked | `Create()`, `Revoke()`, `RecordUsage()` |
| **Session** | Id, UserId, SessionToken, DeviceInfo, ExpiresAt, IsRevoked | `Create()`, `Extend()`, `Revoke()` |
| **OAuthAccount** | Id, UserId, Provider, ProviderUserId, Email | `Link()`, `Unlink()` |

### Value Objects

| Value Object | Purpose | Validation |
|--------------|---------|------------|
| **Email** | Email address with validation | RFC 5322 format, lowercase normalization |
| **PasswordHash** | PBKDF2 password hash | 210,000 iterations, per-password salt |

**Implementation Examples**: See `tests/Api.Tests/BoundedContexts/Authentication/Domain/`

---

## 📡 Application Layer (CQRS)

> **Note**: This context implements **57 commands and queries** (36 commands + 21 queries).
> All endpoints use `IMediator.Send()` pattern per CQRS architecture.

---

### CORE AUTHENTICATION

#### Registration & Login

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `RegisterCommand` | POST | `/api/v1/auth/register` | None | `RegisterPayload` | `{ user: UserDto, expiresAt }` |
| `LoginCommand` | POST | `/api/v1/auth/login` | None | `LoginPayload` | `{ user: UserDto, expiresAt }` OR `{ requiresTwoFactor, sessionToken }` |
| `LogoutCommand` | POST | `/api/v1/auth/logout` | Cookie | None | `{ ok: bool }` |
| `LoginWithApiKeyCommand` | POST | `/api/v1/auth/apikey/login` | None | `ApiKeyLoginPayload` | `{ user: UserDto, message }` |
| `LogoutApiKeyCommand` | POST | `/api/v1/auth/apikey/logout` | API Key Cookie | None | `{ ok: bool, message }` |

**RegisterCommand**:
- Request: email, password, displayName, role
- Response: UserDto + expiresAt
- Validation: Email format/unique, password strength (min 8 chars, uppercase, lowercase, digit, special)
- Side Effects: Session cookie created, verification email sent, UserCreatedEvent raised
- Errors: 400 (validation), 409 (duplicate email)

**LoginCommand**:
- Request: email, password
- Response: UserDto + expiresAt OR requiresTwoFactor + sessionToken (if 2FA enabled)
- Validation: Account not locked, password matches hash
- Side Effects: Failed login count incremented/reset, LastLoginAt updated, session cookie created
- Errors: 400 (missing fields), 401 (invalid credentials), 403 (account locked)
- Events: UserLoggedInEvent, LoginFailedEvent

**Implementation Examples**: See `tests/Api.Tests/BoundedContexts/Authentication/Application/`

---

### SESSION MANAGEMENT

#### Session Lifecycle

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `CreateSessionCommand` | POST | `/api/v1/auth/sessions` | None | `CreateSessionPayload` | `SessionDto` |
| `GetSessionStatusQuery` | GET | `/api/v1/auth/session/status` | Cookie | None | `SessionStatusResponse` |
| `ExtendSessionCommand` | POST | `/api/v1/auth/session/extend` | Cookie | None | `{ expiresAt }` |
| `GetUserSessionsQuery` | GET | `/api/v1/users/me/sessions` | Cookie | None | `List<SessionDto>` |
| `RevokeSessionCommand` | POST | `/api/v1/auth/sessions/{sessionId}/revoke` | Cookie | None | `{ ok, message }` |
| `LogoutAllDevicesCommand` | POST | `/api/v1/auth/sessions/revoke-all` | Cookie | `LogoutAllDevicesPayload` | `{ ok, revokedCount, currentSessionRevoked, message }` |
| `RevokeAllUserSessionsCommand` | POST | `/api/v1/admin/users/{userId}/sessions/revoke` | Cookie + Admin | Path: userId | `{ ok, revokedCount }` |
| `RevokeInactiveSessionsCommand` | POST | `/api/v1/admin/sessions/cleanup` | Cookie + Admin | Query: inactiveDays? | `{ ok, revokedCount }` |
| `GetAllSessionsQuery` | GET | `/api/v1/admin/sessions` | Cookie + Admin | Query: userId?, isActive?, page, pageSize | `PaginatedList<SessionDto>` |

**Key Session Operations**:
- **GetSessionStatus**: Returns expiresAt, lastSeenAt, remainingMinutes (Redis cached 1 min)
- **ExtendSession**: Adds 30 days from now (sliding window, configurable)
- **LogoutAllDevices**: Revokes all sessions, requires password confirmation, returns revokedCount

---

### TWO-FACTOR AUTHENTICATION (TOTP)

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `GenerateTotpSetupCommand` | POST | `/api/v1/auth/2fa/setup` | Cookie | None | `TotpSetupDto` |
| `Enable2FACommand` | POST | `/api/v1/auth/2fa/enable` | Cookie | `TwoFactorEnableRequest` | `{ Success, BackupCodes, ErrorMessage }` |
| `Verify2FACommand` | POST | `/api/v1/auth/2fa/verify` | None | `TwoFactorVerifyRequest` | `{ message, user }` |
| `Disable2FACommand` | POST | `/api/v1/auth/2fa/disable` | Cookie | `TwoFactorDisableRequest` | `{ message }` |
| `Get2FAStatusQuery` | GET | `/api/v1/users/me/2fa/status` | Cookie | None | `TwoFactorStatusDto` |
| `AdminDisable2FACommand` | POST | `/api/v1/auth/admin/2fa/disable` | Cookie + Admin | `AdminDisable2FARequest` | `{ message }` |

**2FA Workflow**:
1. **GenerateTotpSetup**: Returns QR code + secret + 8 backup codes (8 digits each, single-use)
2. **Enable2FA**: Validates 6-digit TOTP code, stores encrypted secret, raises TwoFactorEnabledEvent (rate limited: 3 attempts/min)
3. **Verify2FA**: Exchanges temp login token for permanent session using TOTP code or backup code (8 digits)
4. **AdminDisable2FA**: Admin-only emergency recovery (requires Admin role, creates audit log)

---

### OAUTH 2.0

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `InitiateOAuthLoginCommand` | GET | `/api/v1/auth/oauth/{provider}/login` | None | Path: provider | 302 Redirect |
| `HandleOAuthCallbackCommand` | GET | `/api/v1/auth/oauth/{provider}/callback` | None | Query: code, state | 302 Redirect |
| `LinkOAuthAccountCommand` | POST | `/api/v1/auth/oauth/{provider}/link` | Cookie | Path: provider, Query: code, state | `{ ok, message }` |
| `UnlinkOAuthAccountCommand` | DELETE | `/api/v1/auth/oauth/{provider}/unlink` | Cookie | Path: provider | 204 No Content |
| `GetLinkedOAuthAccountsQuery` | GET | `/api/v1/users/me/oauth-accounts` | Cookie | None | `List<OAuthAccountDto>` |

**OAuth Providers**: Google, GitHub, Discord

**OAuth Flow**:
1. **InitiateOAuth**: Generates state token (10-min expiry, IP-bound), stores in Redis, redirects to provider
2. **HandleCallback**: Validates state, exchanges code for token, fetches profile, creates/links user, creates session, redirects to frontend
3. **LinkOAuthAccount**: Links OAuth to existing authenticated user (requires active session)
4. **UnlinkOAuthAccount**: Removes OAuth link (validation: cannot unlink last auth method)

**Security**: State expires in 10 min, tied to IP (replay protection), defensive transactions (Issue #2600)
**Edge Cases**: Email exists → link account; OAuth exists → return session; email mismatch → reject

---

### PASSWORD MANAGEMENT

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `ChangePasswordCommand` | PUT | `/api/v1/users/profile/password` | Cookie | `ChangePasswordPayload` | `{ ok, message }` |
| `RequestPasswordResetCommand` | POST | `/api/v1/auth/password-reset/request` | None | `PasswordResetRequestPayload` | `{ ok, message }` |
| `ValidatePasswordResetTokenQuery` | GET | `/api/v1/auth/password-reset/verify` | None | Query: token | `{ ok, message }` |
| `ResetPasswordCommand` | PUT | `/api/v1/auth/password-reset/confirm` | None | `PasswordResetConfirmPayload` | `{ ok, message }` |

**Password Management**:
- **ChangePassword**: Requires current password, validates strength, invalidates all other sessions, creates audit log
- **RequestPasswordReset**: Generates 256-bit token (1-hour expiry), sends email, always returns success (anti-enumeration), rate limited (1/min per email)
- **ResetPassword**: Validates token (single-use), enforces strength requirements, invalidates ALL sessions, creates audit log

---

### EMAIL VERIFICATION

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `VerifyEmailCommand` | POST | `/api/v1/auth/email/verify` | None | `VerifyEmailPayload` | `{ ok, message }` |
| `ResendVerificationCommand` | POST | `/api/v1/auth/email/resend` | None | `ResendVerificationPayload` | `{ ok, message }` |

**Email Verification**:
- **VerifyEmail**: Validates token, sets EmailConfirmed = true, raises EmailVerifiedEvent
- **ResendVerification**: Rate limited (1/min per email), always returns success (anti-enumeration)

---

### API KEY MANAGEMENT

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `CreateApiKeyManagementCommand` | POST | `/api/v1/api-keys` | Cookie | `CreateApiKeyRequest` | `{ ApiKey: ApiKeyDto, RawKey: string }` |
| `ListApiKeysQuery` | GET | `/api/v1/api-keys` | Cookie | Query: includeRevoked, page, pageSize | `PaginatedList<ApiKeyDto>` |
| `GetApiKeyQuery` | GET | `/api/v1/api-keys/{keyId}` | Cookie | None | `ApiKeyDto` |
| `UpdateApiKeyManagementCommand` | PUT | `/api/v1/api-keys/{keyId}` | Cookie | `UpdateApiKeyRequest` | `ApiKeyDto` |
| `RevokeApiKeyManagementCommand` | DELETE | `/api/v1/api-keys/{keyId}` | Cookie | None | 204 No Content |
| `RotateApiKeyCommand` | POST | `/api/v1/api-keys/{keyId}/rotate` | Cookie | `RotateApiKeyRequest` | `{ OldApiKey, NewApiKey }` |
| `GetApiKeyUsageQuery` | GET | `/api/v1/api-keys/{keyId}/usage` | Cookie | None | `ApiKeyUsageDto` |
| `GetApiKeyUsageStatsQuery` | GET | `/api/v1/api-keys/{keyId}/stats` | Cookie | None | `ApiKeyUsageStatsDto` |
| `GetApiKeyUsageLogsQuery` | GET | `/api/v1/api-keys/{keyId}/logs` | Cookie | Query: skip, take | `{ logs: List<ApiKeyUsageLogDto>, pagination }` |

**API Key Management**:
- **CreateApiKey**: Generates `mpl_{env}_{base64}` (32 bytes), PBKDF2 hash (10K iterations), returns raw key ONCE, validates name (3-100 chars), expiry (max 2 years)
- **RotateApiKey**: Atomic rotation, immediate revocation (0s grace period), recommended every 90 days
- **GetApiKeyUsageStats**: Returns totalRequests, dailyUsage, topEndpoints, errorRate, avgResponseTime

---

#### Admin API Key Management

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `DeleteApiKeyCommand` | DELETE | `/api/v1/admin/api-keys/{keyId}` | Cookie + Admin | None | 204 No Content |
| `GetAllApiKeysWithStatsQuery` | GET | `/api/v1/admin/api-keys/stats` | Cookie + Admin | Query: userId?, includeRevoked | `{ keys: List<ApiKeyWithStatsDto>, count, filters }` |
| `BulkExportApiKeysQuery` | GET | `/api/v1/admin/api-keys/bulk/export` | Cookie + Admin | Query: userId?, isActive?, searchTerm? | CSV File |
| `BulkImportApiKeysCommand` | POST | `/api/v1/admin/api-keys/bulk/import` | Cookie + Admin | CSV content (raw text) | `{ SuccessCount, FailedCount, Errors }` |

**Bulk Operations** (Admin only):
- **BulkExportApiKeys**: CSV export with UserId, KeyName, KeyPreview, CreatedAt, ExpiresAt, IsRevoked, TotalRequests
- **BulkImportApiKeys**: CSV import with row validation, requires Admin + password confirmation, returns successCount + error details

---

### USER PROFILE & PREFERENCES

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `GetUserProfileQuery` | GET | `/api/v1/users/profile` | Cookie | None | `UserProfileDto` |
| `UpdateUserProfileCommand` | PUT | `/api/v1/users/profile` | Cookie | `UpdateProfilePayload` | `{ ok, message }` |
| `UpdatePreferencesCommand` | PUT | `/api/v1/users/preferences` | Cookie | `UpdatePreferencesPayload` | `UserProfileDto` |
| `GetUserDevicesQuery` | GET | `/api/v1/users/me/devices` | Cookie | None | `List<DeviceDto>` |
| `GetUserByIdQuery` | GET | `/api/v1/users/{userId}` | Cookie | None | `UserDto` |
| `GetUserUploadQuotaQuery` | GET | `/api/v1/users/me/upload-quota` | Cookie | None | `PdfUploadQuotaInfo` |
| `GetUserActivityQuery` | GET | `/api/v1/users/me/activity` | Cookie | Query: filters | `GetUserActivityResult` |
| `GetUserDetailedAiUsageQuery` | GET | `/api/v1/users/me/ai-usage` | Cookie | Query: days? | `UserAiUsageDto` |
| `GetUserAvailableFeaturesQuery` | GET | `/api/v1/users/me/features` | Cookie | None | `List<UserFeatureDto>` |

**User Preferences & Usage**:
- **UpdatePreferences**: Language (it/en), Theme (light/dark/auto), DataRetentionDays (30-365, GDPR)
- **GetUserDetailedAiUsage**: Returns totalTokens, totalCostUsd, breakdown by model/operationType, dailyTimeSeries (days param: 30-365)

---

### ACCOUNT LOCKOUT & ADMIN

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `GetAccountLockoutStatusQuery` | GET | `/api/v1/users/me/lockout-status` | Cookie | None | `AccountLockoutDto` |
| `UnlockAccountCommand` | POST | `/api/v1/auth/admin/unlock-account` | Cookie + Admin | `UnlockAccountRequest` | `{ ok, message }` |

**Account Lockout**:
- **Trigger**: 5 failed logins in 15 min
- **Duration**: 15 min (auto-reset OR admin unlock)
- **Notification**: Email sent on lockout
- **UnlockAccount**: Admin-only, creates audit log with reason

---

### SHARE LINKS (Document Sharing)

| Command/Query | HTTP Method | Endpoint | Auth | Request | Response |
|---------------|-------------|----------|------|---------|----------|
| `CreateShareLinkCommand` | POST | `/api/v1/share-links` | Cookie | `CreateShareLinkPayload` | `ShareLinkDto` |
| `ValidateShareLinkQuery` | GET | `/api/v1/share-links/{token}/validate` | None | None | `{ ok, document }` |
| `RevokeShareLinkCommand` | DELETE | `/api/v1/share-links/{linkId}` | Cookie | None | 204 No Content |

**Share Links**:
- **CreateShareLink**: Generates token for PDF document, supports expiresAt + maxUses, read-only access, cannot cross users
- **ValidateShareLink**: Validates token, returns document if valid
- **RevokeShareLink**: Immediately invalidates token

---

## 🔄 Domain Events

| Event | When Raised | Payload | Subscribers |
|-------|-------------|---------|-------------|
| `UserCreatedEvent` | After successful registration | `{ UserId, Email, Role }` | Administration (audit), UserLibrary (init) |
| `UserLoggedInEvent` | After successful login | `{ UserId, IpAddress, SessionId }` | Administration (audit), SessionTracking |
| `TwoFactorEnabledEvent` | After 2FA setup complete | `{ UserId }` | Administration (security audit) |
| `TwoFactorDisabledEvent` | After 2FA disabled | `{ UserId, DisabledBy }` | Administration (security audit) |
| `PasswordChangedEvent` | After password change | `{ UserId }` | Administration (audit), UserNotifications (email) |
| `PasswordResetEvent` | After password reset | `{ UserId }` | Administration (audit), UserNotifications (email) |
| `ApiKeyCreatedEvent` | After API key generation | `{ UserId, KeyId, KeyName }` | Administration (audit) |
| `ApiKeyRevokedEvent` | After API key revocation | `{ UserId, KeyId }` | Administration (audit) |
| `SessionCreatedEvent` | After session creation | `{ UserId, SessionId, IpAddress }` | SessionTracking |
| `SessionRevokedEvent` | After session revocation | `{ UserId, SessionId, RevokedBy }` | SessionTracking |
| `OAuthAccountLinkedEvent` | After OAuth account linked | `{ UserId, Provider }` | Administration (audit) |
| `OAuthAccountUnlinkedEvent` | After OAuth unlink | `{ UserId, Provider }` | Administration (audit) |
| `EmailVerifiedEvent` | After email verification | `{ UserId, Email }` | Administration (audit) |
| `AccountLockedEvent` | After account lockout | `{ UserId, LockedUntil, Reason }` | Administration (security), UserNotifications (email) |
| `AccountUnlockedEvent` | After admin unlock | `{ UserId, UnlockedBy }` | Administration (audit) |

---

## 🔗 Integration Points

### Dependencies

**Inbound** (Events Published To):
- Administration: All auth events for audit logging
- SessionTracking: Session lifecycle events for analytics
- UserLibrary: UserId from session for collections
- UserNotifications: Security events for email alerts

**Outbound**: None (foundational context)

---

## 🔐 Security & Authorization

### Authentication Methods

| Method | Header/Cookie | Format | Use Case |
|--------|---------------|--------|----------|
| **Session Cookie** | Cookie: `meepleai_session_{env}` | JWT-like token, httpOnly | Web application |
| **API Key Cookie** | Cookie: `meepleai_apikey_{env}` | API key value, httpOnly | CLI/scripts via web login |
| **API Key Header** | Header: `Authorization: ApiKey {key}` | `mpl_{env}_{base64}` | Programmatic access |

### Authorization Levels

| Level | Requirements | Endpoints |
|-------|--------------|-----------|
| **Public** | None | Registration, login, OAuth, password reset, email verify |
| **Authenticated** | Valid session OR API key | Profile, preferences, API key CRUD, 2FA, sessions |
| **Admin** | Cookie + Admin role | User management, bulk operations, unlock accounts |

### Security Configuration

| Component | Specification |
|-----------|---------------|
| **Password Hash** | PBKDF2, 210K iterations, 128-bit salt, 256-bit key |
| **Password Strength** | Min 8 chars, 1 uppercase, 1 lowercase, 1 digit, 1 special |
| **API Key** | 256-bit random, Base64 URL-safe, `mpl_{env}_{token}`, PBKDF2 hash (10K iter) |
| **Session Cookie** | HttpOnly, Secure, SameSite=Lax, 30-day sliding window, SHA256 hash |
| **Session Storage** | Redis-backed, configurable expiry via config |

**Rate Limits**:
- Login: 5 attempts/5min (per IP+Email)
- 2FA Verify: 3 attempts/1min (per session)
- OAuth: 10 requests/1min (per IP)
- Email Resend: 1 request/1min (per email)
- Password Reset: 1 request/1min (per email)

## 🎯 Usage Examples

**Implementation Examples**: See comprehensive test suite for detailed usage patterns:
- **E2E Flows**: `apps/web/__tests__/e2e/authentication/`
- **Integration Tests**: `tests/Api.Tests/BoundedContexts/Authentication/Integration/`
- **Unit Tests**: `tests/Api.Tests/BoundedContexts/Authentication/Application/`

**Key Flows**:
1. **Registration**: Email/password → Session cookie → Verification email
2. **Login with 2FA**: Credentials → Temp token → TOTP code → Session
3. **API Key**: Generate (save raw key immediately) → Use via Header OR Cookie
4. **OAuth**: Initiate → Provider redirect → Callback → Session creation
5. **Password Reset**: Request → Email link → Validate token → Set new password → Invalidate all sessions

---

## 📊 Performance Characteristics

### Caching Strategy

| Operation | Cache Layer | TTL | Invalidation Trigger |
|-----------|-------------|-----|---------------------|
| `GetUserByIdQuery` | Redis | 5 minutes | UserUpdatedEvent, PasswordChangedEvent |
| `GetSessionStatusQuery` | Redis | 1 minute | SessionExtendedEvent, SessionRevokedEvent |
| `ValidateApiKeyQuery` | Redis | 10 minutes | ApiKeyRevokedEvent, ApiKeyRotatedEvent |
| `Get2FAStatusQuery` | Redis | 5 minutes | TwoFactorEnabledEvent, TwoFactorDisabledEvent |
| `GetLinkedOAuthAccountsQuery` | Redis | 30 minutes | OAuthAccountLinkedEvent, OAuthAccountUnlinkedEvent |

### Database Indexes

| Index | Columns | Filter | Purpose |
|-------|---------|--------|---------|
| `idx_users_email` | Email | NOT IsDeleted | User lookup by email |
| `idx_users_role` | Role | NOT IsDeleted | Role-based queries |
| `idx_sessions_userid_active` | UserId, ExpiresAt | NOT IsRevoked | Active session queries |
| `idx_sessions_token_hash` | SessionToken | - | Session validation |
| `idx_apikeys_userid_active` | UserId, ExpiresAt | NOT IsRevoked | Active API key queries |
| `idx_apikeys_hash` | KeyHash | - | API key validation |
| `idx_oauth_userid_provider` | UserId, Provider | - | OAuth account lookup |
| `idx_oauth_provider_userid` | Provider, ProviderUserId | - | Unique constraint |

### Query Performance Targets

| Query Type | Target Latency | Cache Hit Rate |
|------------|----------------|----------------|
| User lookup (by ID) | <10ms | >90% |
| Session validation | <5ms | >95% |
| API key validation | <8ms | >85% |
| Login (full flow) | <200ms | N/A |
| OAuth callback | <500ms | N/A |

---

## 🧪 Testing Strategy

**Coverage Target**: 90%+ (unit), 85%+ (integration), 50+ (E2E flows)

### Test Locations

| Test Type | Location | Tools |
|-----------|----------|-------|
| **Unit Tests** | `tests/Api.Tests/BoundedContexts/Authentication/` | xUnit, FluentAssertions |
| **Integration Tests** | `tests/Api.Tests/BoundedContexts/Authentication/Integration/` | Testcontainers (PostgreSQL, Redis) |
| **E2E Tests** | `apps/web/__tests__/e2e/authentication/` | Playwright |

### Test Categories

**Unit Tests**:
- Domain: Password verification, 2FA secret validation, account lockout logic, API key generation
- Validators: Email format, password strength, required fields, code format validation
- Handlers: Success/failure scenarios, duplicate emails, 2FA flows, lockout behavior

**Integration Tests**:
- Session persistence (Redis caching, expiration cleanup)
- OAuth flow (state validation, account linking, mocked provider)
- Account lockout (5 failed logins, auto-unlock, admin unlock)
- API key rotation (create, use, rotate, verify revocation)

**E2E Tests**:
- Registration flow (form → submit → session cookie → redirect)
- Login with 2FA (credentials → TOTP prompt → code entry → session)
- OAuth login (Google redirect → callback → session creation)
- Password reset (request → email link → new password → login)

**Test Examples**: See test suite for comprehensive implementation examples

---

## 📂 Code Location

**Source**: `apps/api/src/Api/BoundedContexts/Authentication/`
- **Domain**: Entities (User, ApiKey, Session, OAuthAccount), ValueObjects (Email, PasswordHash), Repositories, Events (15+)
- **Application**: Commands (36), Queries (21), Handlers (57), DTOs (25+), Validators (20+)
- **Infrastructure**: Persistence (Repositories), Services (OAuth providers), DependencyInjection

**Routing**: `apps/api/src/Api/Routing/AuthenticationEndpoints.cs`
**Tests**: `tests/Api.Tests/BoundedContexts/Authentication/`

---

## 🔗 Related Documentation

**ADRs**: [ADR-027 Infrastructure Services](../architecture/adr/adr-027-infrastructure-services-policy.md), [ADR-009 Error Handling](../architecture/adr/adr-009-centralized-error-handling.md), [ADR-008 CQRS](../architecture/adr/adr-008-streaming-cqrs-migration.md)

**Bounded Contexts**: [Administration](./administration.md), [SessionTracking](../api/session-tracking/sse-integration.md), [UserNotifications](./user-notifications.md)

**Security**: [OAuth Testing](../testing/backend/oauth-testing.md), [TOTP Analysis](../security/totp-vulnerability-analysis.md), [Secrets Management](../deployment/secrets-management.md)

**API**: [Scalar Docs](http://localhost:8080/scalar/v1), [Endpoints](../api/endpoints/)

---

## 📈 Metrics & Monitoring

### Key Performance Indicators

| Metric | Target | Current | Trend |
|--------|--------|---------|-------|
| Login Success Rate | >95% | TBD | - |
| Session Creation Time | <200ms (P95) | TBD | - |
| 2FA Verification Time | <100ms (P95) | TBD | - |
| API Key Validation | <10ms (P95) | TBD | - |
| OAuth Flow Completion | >90% | TBD | - |

### Monitoring Queries

| Metric | Query |
|--------|-------|
| **Active Sessions** | `SELECT COUNT(*) FROM Sessions WHERE NOT IsRevoked AND ExpiresAt > NOW()` |
| **Failed Login Rate** | `SELECT COUNT(*) FROM AuditLogs WHERE Action = 'LoginFailed' AND Timestamp > NOW() - INTERVAL '1 hour'` |
| **Locked Accounts** | `SELECT COUNT(*) FROM Users WHERE IsLocked AND LockedUntil > NOW()` |

---

## 🚨 Known Issues & Limitations

### Limitations

| Issue | Impact | Workaround/Plan |
|-------|--------|-----------------|
| OAuth Email Mismatch | Cannot link OAuth if provider email differs from user | Planned enhancement |
| API Key Scopes | Scopes field exists but not enforced | Future roadmap |
| Session Revocation Cache | Revoked sessions valid up to 1 min (cache TTL) | Acceptable trade-off |
| Backup Codes | Single-use, cannot regenerate without re-enabling 2FA | Design limitation |

### Future Enhancements

**Planned**: WebAuthn/Passkeys, API key scope enforcement, session device fingerprinting, passwordless auth (magic links)
**Under Consideration**: Multi-tenancy, SSO (SAML/LDAP), audit retention policies, geolocation-based security

---

**Status**: ✅ Production (⚠️ Issue #3782 blocking login/register)
**Last Updated**: 2026-02-07
**Total Commands**: 36
**Total Queries**: 21
**Total Endpoints**: 50+
**Test Coverage**: 90%+ (unit), 85%+ (integration), 50+ (E2E flows)
**Domain Events**: 15+
