# GDPR/DPA Compliance Remediation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Close all compliance gaps identified in the Hetzner DPA v1.2 audit, making MeepleAI fully GDPR-compliant as both Controller and Client.

**Architecture:** 10 issues organized in 3 phases — Critical (blocking go-live), Important (30-day window), Documentation (non-code). Each issue follows existing CQRS/MediatR patterns with domain entities, commands, handlers, validators, endpoints, and tests.

**Tech Stack:** .NET 9 (C#), Next.js 16 (React 19), PostgreSQL 16, Redis 7, EF Core, MediatR, FluentValidation, xUnit, Vitest

**Parent Branch:** `main-dev`
**Feature Branch:** `feature/gdpr-dpa-compliance`

---

## Phase 1: Critical (🔴 Blocks Go-Live)

### Issue #1: Complete Account Deletion Endpoint (GDPR Art. 17)

**Problem:** `DeleteUserCommandHandler` only deletes the User entity. No cascade to Sessions, ApiKeys, OAuthAccounts, LLM data, AI consent, notifications. No self-service endpoint.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Application/Handlers/DeleteUserCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/DeleteOwnAccountCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Handlers/DeleteOwnAccountCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Validators/DeleteOwnAccountCommandValidator.cs`
- Modify: `apps/api/src/Api/Routing/AuthEndpoints.cs` (add self-service endpoint)
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Handlers/DeleteOwnAccountCommandHandlerTests.cs`

**Step 1: Write failing test for DeleteOwnAccountCommandHandler**

```csharp
// DeleteOwnAccountCommandHandlerTests.cs
public class DeleteOwnAccountCommandHandlerTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_ValidUser_DeletesAllUserData()
    {
        // Arrange: user with sessions, apikeys, oauth, llm data
        // Act: send DeleteOwnAccountCommand
        // Assert: all related data deleted, user soft-deleted
    }

    [Fact]
    public async Task Handle_LastAdmin_ThrowsForbidden()
    {
        // Cannot delete if user is the only admin
    }

    [Fact]
    public async Task Handle_NonExistentUser_ThrowsNotFound() { }
}
```

**Step 2: Create DeleteOwnAccountCommand**

```csharp
// DeleteOwnAccountCommand.cs
[AuditableAction("UserSelfDelete", "User", Level = 3)]
internal record DeleteOwnAccountCommand(Guid UserId) : ICommand;
```

**Step 3: Create DeleteOwnAccountCommandValidator**

```csharp
// DeleteOwnAccountCommandValidator.cs
internal sealed class DeleteOwnAccountCommandValidator : AbstractValidator<DeleteOwnAccountCommand>
{
    public DeleteOwnAccountCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
```

**Step 4: Implement DeleteOwnAccountCommandHandler**

The handler must cascade delete in this order:
1. Revoke all sessions (`SessionRepository.RevokeAllUserSessionsAsync`)
2. Revoke all API keys (`ApiKey.Revoke()` for each)
3. Delete OAuth accounts
4. Delete LLM data (reuse `DeleteUserLlmDataCommand` via mediator)
5. Delete AI consent (`UserAiConsentRepository`)
6. Delete notifications
7. Delete the user entity
8. Log audit event

```csharp
// DeleteOwnAccountCommandHandler.cs
internal sealed class DeleteOwnAccountCommandHandler : ICommandHandler<DeleteOwnAccountCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteOwnAccountCommandHandler> _logger;

    public async Task Handle(DeleteOwnAccountCommand command, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, ct)
            ?? throw new NotFoundException("User", command.UserId);

        // Prevent last admin deletion
        if (user.Role == UserRole.Admin)
        {
            var adminCount = await _userRepository.CountAdminsAsync(ct);
            if (adminCount <= 1)
                throw new ConflictException("Cannot delete the last admin account.");
        }

        // 1. Revoke all sessions
        await _sessionRepository.RevokeAllUserSessionsAsync(command.UserId, ct);

        // 2. Delete LLM data (reuse existing command)
        await _mediator.Send(new DeleteUserLlmDataCommand(
            command.UserId, command.UserId, IsAdminRequest: false), ct);

        // 3. Delete user (cascades to ApiKeys, OAuthAccounts via EF)
        await _userRepository.DeleteAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogWarning("GDPR Art.17: User {UserId} account fully deleted (self-service)", command.UserId);
    }
}
```

**Step 5: Register endpoint**

```csharp
// In AuthEndpoints.cs or UserSettingsEndpoints.cs
app.MapDelete("/api/v1/users/me", async (HttpContext context, IMediator mediator) =>
{
    var session = context.RequireAuthenticatedSession();
    await mediator.Send(new DeleteOwnAccountCommand(session.User!.Id));
    // Clear session cookie
    context.Response.Cookies.Delete("session");
    return Results.NoContent();
})
.RequireAuthorization()
.WithTags("Users")
.WithSummary("Delete own account and all associated data (GDPR Art. 17)");
```

**Step 6: Update admin DeleteUserCommandHandler to also cascade**

Apply same cascade logic to the existing admin handler.

**Step 7: Verify EF cascade configuration**

Check `UserEntityConfiguration.cs` has:
```csharp
builder.HasMany(u => u.Sessions).WithOne().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
builder.HasMany(u => u.ApiKeys).WithOne().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
builder.HasMany(u => u.OAuthAccounts).WithOne().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Cascade);
```

**Step 8: Run tests, commit**

```bash
cd apps/api && dotnet test --filter "DeleteOwnAccount"
git add -A && git commit -m "feat(auth): add self-service account deletion endpoint (GDPR Art. 17)"
```

---

### Issue #2: Cookie Consent Banner (ePrivacy Directive)

**Problem:** Cookie/privacy policy pages exist but no runtime consent banner for non-essential cookies.

**Files:**
- Create: `apps/web/src/components/cookie-consent/CookieConsentBanner.tsx`
- Create: `apps/web/src/components/cookie-consent/cookie-consent-store.ts`
- Create: `apps/web/src/components/cookie-consent/index.ts`
- Modify: `apps/web/src/app/layout.tsx` (mount banner)
- Create: `apps/web/src/__tests__/components/cookie-consent/CookieConsentBanner.test.tsx`

**Step 1: Create Zustand store for consent state**

```typescript
// cookie-consent-store.ts
import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export type CookieCategory = 'essential' | 'analytics' | 'preferences';

interface CookieConsentState {
  consented: boolean;
  categories: Record<CookieCategory, boolean>;
  consentedAt: string | null;
  accept: (categories: Record<CookieCategory, boolean>) => void;
  acceptAll: () => void;
  rejectNonEssential: () => void;
}

export const useCookieConsentStore = create<CookieConsentState>()(
  persist(
    (set) => ({
      consented: false,
      categories: { essential: true, analytics: false, preferences: false },
      consentedAt: null,
      accept: (categories) => set({
        consented: true,
        categories: { ...categories, essential: true },
        consentedAt: new Date().toISOString(),
      }),
      acceptAll: () => set({
        consented: true,
        categories: { essential: true, analytics: true, preferences: true },
        consentedAt: new Date().toISOString(),
      }),
      rejectNonEssential: () => set({
        consented: true,
        categories: { essential: true, analytics: false, preferences: false },
        consentedAt: new Date().toISOString(),
      }),
    }),
    { name: 'meepleai-cookie-consent' }
  )
);
```

**Step 2: Create CookieConsentBanner component**

```tsx
// CookieConsentBanner.tsx
'use client';

import { useCookieConsentStore } from './cookie-consent-store';
import Link from 'next/link';

export function CookieConsentBanner() {
  const { consented, acceptAll, rejectNonEssential } = useCookieConsentStore();

  if (consented) return null;

  return (
    <div className="fixed bottom-0 inset-x-0 z-50 p-4 bg-white/90 backdrop-blur-md border-t border-stone-200 shadow-lg"
         role="dialog" aria-label="Cookie consent">
      <div className="max-w-4xl mx-auto flex flex-col sm:flex-row items-center gap-4">
        <p className="text-sm text-stone-600 flex-1">
          We use essential cookies for site functionality. Optional cookies help us improve your experience.{' '}
          <Link href="/cookies" className="underline text-amber-700 hover:text-amber-800">Learn more</Link>
        </p>
        <div className="flex gap-2 shrink-0">
          <button onClick={rejectNonEssential}
            className="px-4 py-2 text-sm border border-stone-300 rounded-lg hover:bg-stone-50">
            Essential Only
          </button>
          <button onClick={acceptAll}
            className="px-4 py-2 text-sm bg-amber-600 text-white rounded-lg hover:bg-amber-700">
            Accept All
          </button>
        </div>
      </div>
    </div>
  );
}
```

**Step 3: Mount in root layout**

```tsx
// In app/layout.tsx, add inside body:
import { CookieConsentBanner } from '@/components/cookie-consent';
// ...
<CookieConsentBanner />
```

**Step 4: Write tests**

```typescript
// CookieConsentBanner.test.tsx
describe('CookieConsentBanner', () => {
  it('renders when not consented', () => { /* ... */ });
  it('hides after accepting all', () => { /* ... */ });
  it('hides after rejecting non-essential', () => { /* ... */ });
  it('persists consent in localStorage', () => { /* ... */ });
  it('has accessible role=dialog', () => { /* ... */ });
});
```

**Step 5: Commit**

```bash
cd apps/web && pnpm test -- --run CookieConsentBanner
git add -A && git commit -m "feat(web): add cookie consent banner (ePrivacy Directive)"
```

---

### Issue #3: Hetzner DPA Appendice 1 — Data Processing Description

**Problem:** Appendix 1 of the DPA is generic. Must be filled with MeepleAI-specific data categories.

**Files:**
- Create: `docs/compliance/hetzner-dpa-appendix-1-meepleai.md`

**Content to create:**

```markdown
# Appendix 1 — MeepleAI Data Processing Description

## Scope, Type, and Purpose of Data Processing

MeepleAI is an AI-powered board game assistant hosted on Hetzner Cloud infrastructure.
Data processing includes:
- User account management (registration, authentication, sessions)
- AI chat processing (RAG, multi-agent, vector search)
- PDF document processing (board game rulebooks)
- Game library management
- Community game catalog

## Types of Personal Data Processed

| Category | Data Fields | Legal Basis | Retention |
|----------|-------------|-------------|-----------|
| Identity Data | Email, display name | Art. 6(1)(b) contract | Account lifetime |
| Authentication Data | Password hash (PBKDF2-SHA256), session tokens (SHA-256 hash), OAuth tokens (AES encrypted), TOTP secrets (encrypted), backup codes (SHA-256 hash) | Art. 6(1)(b) contract | Sessions: 30 days, Account: lifetime |
| Technical Data | IP address, user agent, device fingerprint (SHA-256) | Art. 6(1)(f) legitimate interest | 30 days (sessions) |
| AI Interaction Data | Chat prompts (no PII), AI responses, conversation memory | Art. 6(1)(a) consent + Art. 6(1)(f) | 30 days (logs), 90 days (memory) |
| Usage Data | API key usage, LLM token counts, cost tracking | Art. 6(1)(f) legitimate interest | 30 days |
| Preference Data | Language, theme, data retention days, notification preferences | Art. 6(1)(b) contract | Account lifetime |
| Gamification Data | Level, XP, achievements, badges | Art. 6(1)(b) contract | Account lifetime |
| Consent Records | AI consent, external provider consent, consent version, timestamp | Art. 6(1)(c) legal obligation | Permanent (legal requirement) |

## Categories of Data Subjects

- **Registered Users**: Individuals who create a MeepleAI account to use the board game AI assistant
- **Admin Users**: Platform administrators with elevated access rights

## Special Categories (Art. 9 GDPR)

No special categories of personal data are processed. MeepleAI does not collect health, biometric, racial, political, religious, sexual orientation, or trade union data.

## Data Transfers to Third Parties

| Processor | Location | Data Sent | Safeguard |
|-----------|----------|-----------|-----------|
| OpenRouter Inc. | USA | AI prompts (PII stripped), conversation context | SCCs Art. 46(2)(c) + user consent |
| BoardGameGeek | USA | Game search queries (no PII) | Public API, no personal data |

## Technical Architecture on Hetzner

- **Product Type**: Cloud Server (CX-series) located in Germany/Finland (EU)
- **Services**: PostgreSQL 16, Redis 7, Qdrant vector DB, .NET 9 API, Next.js frontend
- **Encryption in transit**: TLS 1.2+ (Traefik reverse proxy)
- **Encryption at rest**: See Issue #5 of compliance remediation plan
```

**Step 1: Create file, commit**

```bash
git add docs/compliance/hetzner-dpa-appendix-1-meepleai.md
git commit -m "docs(compliance): add Hetzner DPA Appendix 1 with MeepleAI data description"
```

---

### Issue #4: DPIA Formale (GDPR Art. 35)

**Problem:** `docs/compliance/dpia-llm.md` exists but covers only LLM processing. Need a comprehensive DPIA covering ALL data processing.

**Files:**
- Create: `docs/compliance/dpia-full-platform.md`

**Content:** Full DPIA covering:
1. Systematic description of all processing operations
2. Assessment of necessity and proportionality
3. Risk assessment for data subjects' rights
4. Measures to address risks (encryption, pseudonymization, retention, access controls)
5. Cross-reference to Hetzner TOMs (Appendix 2 of DPA)
6. Conclusion and review schedule

**Step 1: Create comprehensive DPIA document**

Structure:
- Section 1: Processing description (ref Appendix 1)
- Section 2: EDPB screening criteria (9 criteria)
- Section 3: Risk matrix (likelihood x severity for each processing activity)
- Section 4: Mitigation measures mapped to Hetzner DPA TOMs
- Section 5: Residual risk assessment
- Section 6: DPO/Controller review and sign-off
- Section 7: Annual review schedule (next: March 2027)

**Step 2: Commit**

```bash
git add docs/compliance/dpia-full-platform.md
git commit -m "docs(compliance): add comprehensive DPIA for full platform (GDPR Art. 35)"
```

---

### Issue #5: Encryption at Rest Documentation & Configuration

**Problem:** DPA Appendix 2 §7 states encryption at rest is Client's responsibility. PostgreSQL, Redis, and Qdrant lack encryption at rest config.

**Files:**
- Create: `docs/compliance/encryption-at-rest-guide.md`
- Create: `infra/scripts/setup-luks-encryption.sh`
- Modify: `infra/docker-compose.yml` (add TLS config for Redis, PostgreSQL)

**Step 1: Create encryption guide document**

Document the required infrastructure changes:
1. **Volume encryption**: Hetzner Cloud volumes support LUKS encryption at creation
2. **PostgreSQL**: Enable `ssl = on` in postgresql.conf + use `sslmode=verify-full` in connection string
3. **Redis**: Enable TLS via `redis-server --tls-port 6380 --tls-cert-file /tls/redis.crt`
4. **Qdrant**: Mount on LUKS-encrypted volume

**Step 2: Create LUKS setup script**

```bash
#!/bin/bash
# setup-luks-encryption.sh
# Run on Hetzner Cloud server to encrypt data volumes
# Requires: cryptsetup, Hetzner Cloud volume attached

VOLUME="/dev/disk/by-id/scsi-0HC_Volume_XXXXX"
MAPPER_NAME="encrypted-data"
MOUNT_POINT="/mnt/encrypted-data"

echo "Setting up LUKS encryption for data volume..."
cryptsetup luksFormat "$VOLUME"
cryptsetup luksOpen "$VOLUME" "$MAPPER_NAME"
mkfs.ext4 "/dev/mapper/$MAPPER_NAME"
mkdir -p "$MOUNT_POINT"
mount "/dev/mapper/$MAPPER_NAME" "$MOUNT_POINT"
echo "Volume encrypted and mounted at $MOUNT_POINT"
```

**Step 3: Add PostgreSQL SSL config to docker-compose**

```yaml
# In docker-compose.yml postgres service
command: >
  postgres
  -c ssl=on
  -c ssl_cert_file=/var/lib/postgresql/server.crt
  -c ssl_key_file=/var/lib/postgresql/server.key
```

**Step 4: Commit**

```bash
git add docs/compliance/encryption-at-rest-guide.md infra/scripts/setup-luks-encryption.sh
git commit -m "docs(compliance): add encryption at rest guide and LUKS setup script"
```

---

## Phase 2: Important (🟡 Within 30 Days)

### Issue #6: Full Data Export API (GDPR Art. 20 — Data Portability)

**Problem:** Only bulk admin export exists (`GET /admin/users/bulk/export`). No self-service user data export.

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/ExportUserDataQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Handlers/ExportUserDataQueryHandler.cs`
- Modify: `apps/api/src/Api/Routing/UserSettingsEndpoints.cs` (add export endpoint)
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Handlers/ExportUserDataQueryHandlerTests.cs`

**Step 1: Write failing test**

```csharp
[Fact]
public async Task Handle_ValidUser_ReturnsCompleteDataExport()
{
    // Assert: JSON contains user profile, sessions, apikeys, ai consent, llm stats, game library
}
```

**Step 2: Create ExportUserDataQuery**

```csharp
internal record ExportUserDataQuery(Guid UserId) : IQuery<UserDataExportDto>;

internal record UserDataExportDto(
    UserProfileExport Profile,
    IReadOnlyList<SessionExport> Sessions,
    IReadOnlyList<ApiKeyExport> ApiKeys,
    AiConsentExport? AiConsent,
    LlmUsageStatsExport LlmStats,
    DateTime ExportedAt);
```

**Step 3: Implement handler**

Aggregates data from all repositories into a single JSON export.

**Step 4: Register endpoint**

```csharp
app.MapGet("/api/v1/users/me/export", async (HttpContext context, IMediator mediator) =>
{
    var session = context.RequireAuthenticatedSession();
    var result = await mediator.Send(new ExportUserDataQuery(session.User!.Id));
    return Results.Ok(result);
})
.RequireAuthorization()
.WithTags("Users")
.WithSummary("Export all personal data (GDPR Art. 20)");
```

**Step 5: Run tests, commit**

```bash
dotnet test --filter "ExportUserData"
git commit -m "feat(auth): add self-service data export endpoint (GDPR Art. 20)"
```

---

### Issue #7: Audit-Proof Logging for Personal Data Changes

**Problem:** AuditLog entity exists but doesn't track all GDPR-relevant events. Missing types for consent, erasure, breach.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Domain/Entities/AuditLog.cs` (add GDPR action constants)
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationType.cs` (add GDPR types)
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Domain/Entities/AuditLogGdprTests.cs`

**Step 1: Add GDPR audit action constants**

```csharp
// In AuditLog.cs or a new AuditActions static class
public static class GdprAuditActions
{
    public const string DataSubjectAccessRequest = "GdprDataSubjectAccessRequest";
    public const string DataExported = "GdprDataExported";
    public const string AccountDeleted = "GdprAccountDeleted";
    public const string LlmDataDeleted = "GdprLlmDataDeleted";
    public const string ConsentGranted = "GdprConsentGranted";
    public const string ConsentWithdrawn = "GdprConsentWithdrawn";
    public const string BreachDetected = "GdprBreachDetected";
    public const string BreachNotified = "GdprBreachNotified";
}
```

**Step 2: Add notification types**

```csharp
// In NotificationType.cs
public static readonly NotificationType GdprErasureCompleted = new("gdpr_erasure_completed");
public static readonly NotificationType GdprBreachAlert = new("gdpr_breach_alert");
public static readonly NotificationType GdprConsentWithdrawn = new("gdpr_consent_withdrawn");
public static readonly NotificationType GdprDataExported = new("gdpr_data_exported");
```

**Step 3: Wire audit logging into DeleteOwnAccountCommandHandler and ExportUserDataQueryHandler**

Use the existing `[AuditableAction]` attribute pattern.

**Step 4: Tests, commit**

```bash
dotnet test --filter "AuditLogGdpr"
git commit -m "feat(admin): add GDPR-specific audit actions and notification types"
```

---

### Issue #8: Email Pseudonymization / Column-Level Encryption

**Problem:** User emails stored in plaintext in PostgreSQL. DPA requires encryption at rest as Client responsibility.

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Encryption/ColumnEncryptionConverter.cs`
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Persistence/UserEntityConfiguration.cs`
- Create: `apps/api/tests/Api.Tests/Infrastructure/Encryption/ColumnEncryptionConverterTests.cs`

**Step 1: Create EF Core value converter for column encryption**

```csharp
// ColumnEncryptionConverter.cs
internal sealed class ColumnEncryptionConverter : ValueConverter<string, string>
{
    public ColumnEncryptionConverter(IEncryptionService encryption)
        : base(
            v => encryption.Encrypt(v, "ColumnEncryption"),
            v => encryption.Decrypt(v, "ColumnEncryption"))
    { }
}
```

**Step 2: Apply to User email in configuration**

```csharp
// In UserEntityConfiguration.cs
builder.Property(u => u.Email)
    .HasConversion(new ColumnEncryptionConverter(encryptionService))
    .HasMaxLength(512); // encrypted values are longer
```

**Step 3: Migration**

```bash
dotnet ef migrations add EncryptUserEmails
# Note: requires data migration script to encrypt existing emails
```

**Step 4: Tests, commit**

```bash
dotnet test --filter "ColumnEncryption"
git commit -m "feat(infra): add column-level encryption for user emails (GDPR Art. 32)"
```

---

### Issue #9: Breach Notification Process Document

**Problem:** No documented process for Art. 33/34 breach notification within 72 hours.

**Files:**
- Create: `docs/compliance/breach-notification-process.md`

**Content:**
1. Detection → Classification (severity: low/medium/high/critical)
2. Internal escalation (< 4 hours)
3. Risk assessment (likelihood of harm to data subjects)
4. Notify Hetzner (per DPA §5.8) if breach at infrastructure level
5. Notify supervisory authority (Garante Privacy, Italy) within 72 hours if high risk
6. Notify affected data subjects without undue delay if very high risk
7. Document everything in AuditLog with `GdprBreachDetected` / `GdprBreachNotified`
8. Post-incident review within 7 days

**Step 1: Create document, commit**

```bash
git add docs/compliance/breach-notification-process.md
git commit -m "docs(compliance): add breach notification process (GDPR Art. 33/34)"
```

---

### Issue #10: Sign the DPA

**Problem:** Page 11 shows Hetzner signed (10/03/2026) but Client signature field is empty.

**Action:** Manual — print, sign, scan, upload to Hetzner account portal at https://accounts.hetzner.com/account/dpa

**No code changes required.** This is a reminder item.

---

## Execution Order

| Priority | Issue | Type | Est. Effort | Dependencies |
|----------|-------|------|-------------|--------------|
| 1 | #1 Account Deletion | Backend | 4h | None |
| 2 | #2 Cookie Consent | Frontend | 2h | None |
| 3 | #3 Appendix 1 | Docs | 1h | None |
| 4 | #4 DPIA | Docs | 2h | #3 |
| 5 | #5 Encryption Guide | Infra/Docs | 2h | None |
| 6 | #7 Audit Actions | Backend | 2h | #1 |
| 7 | #6 Data Export | Backend | 3h | #7 |
| 8 | #8 Email Encryption | Backend | 3h | #5 |
| 9 | #9 Breach Process | Docs | 1h | None |
| 10 | #10 Sign DPA | Manual | 15min | None |

**Total estimated effort: ~20 hours**

**Parallelization opportunities:**
- Issues #1, #2, #3, #5, #9 are fully independent → can run in parallel
- Issues #7 depends on #1 (uses same audit patterns)
- Issue #6 depends on #7 (uses GDPR audit actions)
- Issue #8 depends on #5 (encryption infrastructure)
- Issue #4 depends on #3 (references Appendix 1)

---

## Testing Strategy

### Backend Tests
- Unit tests for each command handler (mock repositories)
- Integration tests for cascade deletion (Testcontainers + PostgreSQL)
- Verify audit log entries are created for all GDPR actions

### Frontend Tests
- Vitest: Cookie consent store state management
- Vitest: Banner rendering and interaction
- E2E (Playwright): Banner appears on first visit, hides after consent

### Compliance Validation
- Verify all 4 data types in Appendix 1 are covered by retention policies
- Verify self-service deletion removes ALL user data (integration test)
- Verify data export contains ALL user data categories
- Verify audit trail captures all GDPR events
