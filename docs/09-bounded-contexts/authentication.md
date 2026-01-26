# Authentication Bounded Context

**Gestione autenticazione, sessioni, OAuth, 2FA, e API keys**

---

## 📋 Responsabilità

- Registrazione e login utenti (email/password)
- Gestione sessioni (cookie-based)
- OAuth 2.0 (Google, GitHub, Discord)
- Two-Factor Authentication (TOTP)
- API Key generation e revocation
- Password reset e email verification

---

## 🏗️ Domain Model

### Aggregates

**User** (Aggregate Root):
```csharp
public class User
{
    public Guid Id { get; private set; }
    public Email Email { get; private set; }          // Value Object
    public PasswordHash Password { get; private set; } // Value Object
    public bool EmailConfirmed { get; private set; }
    public bool TwoFactorEnabled { get; private set; }
    public string? TwoFactorSecret { get; private set; }
    public List<RefreshToken> RefreshTokens { get; private set; }
    public List<ApiKey> ApiKeys { get; private set; }

    public void EnableTwoFactor(string secret) { }
    public void ConfirmEmail() { }
    public ApiKey GenerateApiKey(string name) { }
}
```

**ApiKey** (Entity):
```csharp
public class ApiKey
{
    public Guid Id { get; private set; }
    public string Key { get; private set; }        // "mpl_{env}_{base64}"
    public string KeyHash { get; private set; }    // PBKDF2 hash
    public string Name { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }

    public void Revoke() { }
}
```

### Value Objects

**Email**:
```csharp
public record Email
{
    public string Value { get; init; }

    public static Email Create(string value)
    {
        // Validation: format, lowercase normalization
    }
}
```

**PasswordHash**:
```csharp
public record PasswordHash
{
    public string Value { get; init; }

    public static PasswordHash Create(string plaintext)
    {
        // BCrypt hashing with cost factor 12
    }

    public bool Verify(string plaintext) { }
}
```

---

## 📡 Application Layer (CQRS)

### Commands (Write Operations)

| Command | Handler | Endpoint | Auth |
|---------|---------|----------|------|
| `RegisterCommand` | `RegisterCommandHandler` | `POST /api/v1/auth/register` | None |
| `LoginCommand` | `LoginCommandHandler` | `POST /api/v1/auth/login` | None |
| `LogoutCommand` | `LogoutCommandHandler` | `POST /api/v1/auth/logout` | Cookie |
| `EnableTwoFactorCommand` | `EnableTwoFactorCommandHandler` | `POST /api/v1/auth/2fa/enable` | Cookie |
| `VerifyTwoFactorCommand` | `VerifyTwoFactorCommandHandler` | `POST /api/v1/auth/2fa/verify` | Cookie |
| `GenerateApiKeyCommand` | `GenerateApiKeyCommandHandler` | `POST /api/v1/auth/api-keys` | Cookie |
| `RevokeApiKeyCommand` | `RevokeApiKeyCommandHandler` | `DELETE /api/v1/auth/api-keys/{id}` | Cookie |
| `HandleOAuthCallbackCommand` | `HandleOAuthCallbackCommandHandler` | `GET /api/v1/auth/oauth/callback/{provider}` | None |

### Queries (Read Operations)

| Query | Handler | Endpoint | Auth |
|-------|---------|----------|------|
| `GetCurrentUserQuery` | `GetCurrentUserQueryHandler` | `GET /api/v1/auth/me` | Cookie/API Key |
| `GetApiKeysQuery` | `GetApiKeysQueryHandler` | `GET /api/v1/auth/api-keys` | Cookie |

---

## 🔐 Security Features

### Password Security
- **Hashing**: BCrypt with cost factor 12 (2^12 = 4096 iterations)
- **Validation**: Min 8 chars, uppercase, lowercase, digit, special char
- **Storage**: Never store plaintext, only PasswordHash value object

### API Key Security
- **Format**: `mpl_{env}_{base64}` (32 bytes random, Base64-encoded)
- **Storage**: PBKDF2 hash (10,000 iterations) with per-key salt
- **Rotation**: Recommended every 90 days
- **Revocation**: Immediate soft-delete (IsRevoked flag)

### Session Security
- **Cookie**: HttpOnly, Secure, SameSite=Lax
- **Expiration**: 30 days sliding expiration (extends on activity)
- **Storage**: Redis for distributed session store

### Two-Factor Authentication (TOTP)
- **Library**: OtpNet (.NET)
- **Algorithm**: TOTP (Time-based OTP, RFC 6238)
- **Secret**: 160-bit random (Base32 encoded)
- **Window**: ±1 step (30s each) = 90s tolerance

---

## 📊 Database Schema

**Tables**:
- `Users` - User accounts
- `RefreshTokens` - JWT refresh tokens
- `ApiKeys` - API key registry
- `OAuthProviders` - OAuth configurations
- `TwoFactorRecoveryCodes` - Backup codes for 2FA

**Key Indexes**:
```sql
CREATE INDEX idx_users_email ON Users(Email);
CREATE INDEX idx_apikeys_keyhash ON ApiKeys(KeyHash);
CREATE INDEX idx_apikeys_userid ON ApiKeys(UserId);
```

---

## 🔄 Integration Points

### Outbound (Dependencies)

**Administration Context**:
- `UserCreatedEvent` → Audit log creation
- `UserDeletedEvent` → Cleanup audit trail

**UserLibrary Context**:
- User registration triggers library initialization

### Inbound (Consumed By)

**All Contexts**:
- Authentication required for most endpoints
- Current user info via `ICurrentUserService`

---

## 🧪 Testing

**Location**: `tests/Api.Tests/BoundedContexts/Authentication/`

**Coverage**: 95%+ (critical security context)

**Test Categories**:
- **Unit Tests**: Password hashing, email validation, TOTP generation
- **Integration Tests**: Login flow, OAuth callback, API key generation
- **E2E Tests**: Complete registration → login → 2FA → API key workflow

**Key Tests**:
```csharp
// Unit
PasswordHash_Create_ShouldUseBCrypt()
Email_Create_ShouldNormalizeLowercase()
TotpService_Generate_ShouldProduceValidCode()

// Integration
LoginCommand_ValidCredentials_ShouldReturnCookie()
RegisterCommand_DuplicateEmail_ShouldThrowConflict()
OAuthCallback_ValidCode_ShouldCreateUser()
```

---

## 📖 ADRs & Design Decisions

**Related ADRs**:
- [ADR-009: Centralized Error Handling](../01-architecture/adr/adr-009-centralized-error-handling.md)
- [ADR-010: Security Headers Middleware](../01-architecture/adr/adr-010-security-headers-middleware.md)
- [ADR-011: CORS Whitelist](../01-architecture/adr/adr-011-cors-whitelist-headers.md)

**OAuth Configuration** (Issue #2565):
- Self-hosted TOTP (no external service cost)
- OAuth providers configurable via `oauth.secret`
- Fallback to email/password if OAuth fails

---

## 🚀 Implementation Examples

### Register New User

```csharp
// Command
var command = new RegisterCommand(
    Email: "user@example.com",
    Password: "SecurePassword123!",
    ConfirmPassword: "SecurePassword123!"
);

// Send via MediatR
var result = await _mediator.Send(command);

// Result
// UserDto { Id = ..., Email = "user@example.com", EmailConfirmed = false }
```

### Enable Two-Factor Authentication

```csharp
// 1. Generate secret
var command = new EnableTwoFactorCommand();
var result = await _mediator.Send(command);
// Returns: { Secret = "JBSWY3DPEHPK3PXP", QrCodeUrl = "data:image/png;base64,..." }

// 2. User scans QR code with authenticator app

// 3. Verify TOTP code
var verifyCommand = new VerifyTwoFactorCommand(Code: "123456");
var verified = await _mediator.Send(verifyCommand);
// If verified = true, 2FA is enabled
```

### Generate API Key

```csharp
var command = new GenerateApiKeyCommand(
    Name: "Mobile App",
    ExpiresAt: DateTime.UtcNow.AddYears(1)
);

var result = await _mediator.Send(command);
// Returns: { Key = "mpl_prod_abc123...", ExpiresAt = "2027-01-18" }
// IMPORTANT: Key shown only once, then hashed
```

---

## 📂 File Structure

```
Authentication/
├── Domain/
│   ├── Entities/
│   │   ├── User.cs
│   │   ├── ApiKey.cs
│   │   └── RefreshToken.cs
│   ├── ValueObjects/
│   │   ├── Email.cs
│   │   └── PasswordHash.cs
│   ├── Repositories/
│   │   └── IUserRepository.cs
│   └── Events/
│       ├── UserRegisteredEvent.cs
│       └── UserDeletedEvent.cs
├── Application/
│   ├── Commands/
│   │   ├── RegisterCommand.cs
│   │   ├── LoginCommand.cs
│   │   ├── EnableTwoFactorCommand.cs
│   │   └── GenerateApiKeyCommand.cs
│   ├── Queries/
│   │   ├── GetCurrentUserQuery.cs
│   │   └── GetApiKeysQuery.cs
│   ├── Handlers/
│   │   └── (Command/Query handlers)
│   ├── DTOs/
│   │   ├── UserDto.cs
│   │   └── ApiKeyDto.cs
│   └── Validators/
│       ├── RegisterCommandValidator.cs
│       └── LoginCommandValidator.cs
└── Infrastructure/
    ├── Persistence/
    │   ├── UserRepository.cs
    │   └── Configurations/
    │       └── UserConfiguration.cs (EF Core)
    └── Services/
        ├── TotpService.cs
        └── OAuthService.cs
```

---

## 📖 Related Documentation

- [OAuth Testing Guide](../05-testing/backend/oauth-testing.md)
- [API Authentication Reference](../03-api/README.md#authentication)
- [Security Documentation](../06-security/README.md)
- [Secrets Management](../04-deployment/secrets-management.md)

---

**Last Updated**: 2026-01-18
**Code Location**: `apps/api/src/Api/BoundedContexts/Authentication/`
**Test Coverage**: 95%+
**Status**: ✅ Production
