# 🎯 SPRINT-1 Implementation Guide - Full DDD Approach

**Status**: Ready to Implement
**DDD Context**: Authentication (Phase 2, 70% complete)
**Total Effort**: 51h (2 weeks)
**Issues**: #846-850

---

## 📁 File Structure

```
src/Api/BoundedContexts/Authentication/
├── Domain/
│   ├── Entities/
│   │   ├── User.cs (ENHANCE - add OAuth + 2FA methods)
│   │   └── OAuthAccount.cs (NEW - OAuth entity)
│   ├── ValueObjects/
│   │   ├── Email.cs (EXISTS - from Phase 2)
│   │   ├── TotpSecret.cs (NEW - #847)
│   │   ├── BackupCode.cs (NEW - #847)
│   │   ├── OAuthProvider.cs (NEW - #846)
│   │   └── OAuthToken.cs (NEW - #846)
│   ├── Services/
│   │   └── (none needed for SPRINT-1)
│   └── Repositories/
│       └── IUserRepository.cs (ENHANCE - add OAuth queries)
│
├── Application/
│   ├── Commands/
│   │   ├── LinkOAuthAccountCommand.cs (NEW - #846)
│   │   ├── UnlinkOAuthAccountCommand.cs (NEW - #846)
│   │   ├── Enable2FACommand.cs (NEW - #847)
│   │   ├── Disable2FACommand.cs (NEW - #847)
│   │   ├── UpdateUserProfileCommand.cs (NEW - #849)
│   │   └── ChangePasswordCommand.cs (NEW - #849)
│   ├── Queries/
│   │   ├── GetLinkedOAuthAccountsQuery.cs (NEW - #846)
│   │   ├── Get2FAStatusQuery.cs (NEW - #847)
│   │   └── GetUserProfileQuery.cs (NEW - #849)
│   ├── Handlers/
│   │   ├── LinkOAuthAccountCommandHandler.cs (NEW)
│   │   ├── Enable2FACommandHandler.cs (NEW)
│   │   ├── UpdateUserProfileCommandHandler.cs (NEW)
│   │   └── GetUserProfileQueryHandler.cs (NEW)
│   └── DTOs/
│       ├── OAuthAccountDto.cs (NEW - #846)
│       ├── TwoFactorStatusDto.cs (NEW - #847)
│       └── UserProfileDto.cs (NEW - #849)
│
└── Infrastructure/
    └── Repositories/
        └── UserRepository.cs (ENHANCE - OAuth + 2FA persistence)

tests/Api.Tests/BoundedContexts/Authentication/
├── Domain/
│   ├── Entities/
│   │   └── UserTests.cs (ENHANCE - OAuth + 2FA tests)
│   └── ValueObjects/
│       ├── TotpSecretTests.cs (NEW - #850)
│       ├── BackupCodeTests.cs (NEW - #850)
│       └── OAuthProviderTests.cs (NEW - #850)
├── Application/
│   ├── Commands/
│   │   ├── LinkOAuthAccountCommandHandlerTests.cs (NEW)
│   │   └── Enable2FACommandHandlerTests.cs (NEW)
│   └── Queries/
│       └── GetUserProfileQueryHandlerTests.cs (NEW)
└── Infrastructure/
    └── Repositories/
        └── UserRepositoryTests.cs (ENHANCE)
```

---

## 🔨 Issue #846: OAuth Integration Complete

### DDD Implementation Checklist

#### Domain Layer (3 files, ~200 lines)

**File: `Authentication/Domain/Entities/OAuthAccount.cs`** (NEW)
```csharp
namespace Api.BoundedContexts.Authentication.Domain.Entities;

public class OAuthAccount : Entity<Guid>
{
    public OAuthProvider Provider { get; private set; }
    public string ProviderUserId { get; private set; }
    public OAuthToken AccessToken { get; private set; }
    public OAuthToken? RefreshToken { get; private set; }
    public DateTime TokenExpiresAt { get; private set; }
    public DateTime LinkedAt { get; private set; }

    private OAuthAccount() { } // EF

    public OAuthAccount(
        OAuthProvider provider,
        string providerUserId,
        OAuthToken accessToken,
        OAuthToken? refreshToken = null)
    {
        Id = Guid.NewGuid();
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        ProviderUserId = providerUserId ?? throw new ArgumentNullException(nameof(providerUserId));
        AccessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken));
        RefreshToken = refreshToken;
        TokenExpiresAt = DateTime.UtcNow.AddHours(1); // Provider-specific
        LinkedAt = DateTime.UtcNow;
    }

    public void RefreshAccessToken(OAuthToken newAccessToken, OAuthToken? newRefreshToken = null)
    {
        AccessToken = newAccessToken ?? throw new ArgumentNullException(nameof(newAccessToken));
        if (newRefreshToken != null)
            RefreshToken = newRefreshToken;
        TokenExpiresAt = DateTime.UtcNow.AddHours(1);
    }

    public bool IsTokenExpired() => DateTime.UtcNow >= TokenExpiresAt;
}
```

**File: `Authentication/Domain/ValueObjects/OAuthProvider.cs`** (NEW)
```csharp
namespace Api.BoundedContexts.Authentication.Domain.ValueObjects;

public class OAuthProvider : ValueObject
{
    public string Value { get; private set; }

    private static readonly string[] ValidProviders = { "Google", "Discord", "GitHub" };

    private OAuthProvider() { } // EF

    public OAuthProvider(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("OAuth provider cannot be empty");

        if (!ValidProviders.Contains(value, StringComparer.OrdinalIgnoreCase))
            throw new DomainException($"Invalid OAuth provider: {value}. Valid: {string.Join(", ", ValidProviders)}");

        Value = value;
    }

    public static implicit operator string(OAuthProvider provider) => provider.Value;
    public static explicit operator OAuthProvider(string value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

**File: `Authentication/Domain/ValueObjects/OAuthToken.cs`** (NEW)
```csharp
namespace Api.BoundedContexts.Authentication.Domain.ValueObjects;

public class OAuthToken : ValueObject
{
    public string EncryptedValue { get; private set; }

    private OAuthToken() { } // EF

    public OAuthToken(string plainTextToken, IEncryptionService encryptionService)
    {
        if (string.IsNullOrWhiteSpace(plainTextToken))
            throw new DomainException("OAuth token cannot be empty");

        EncryptedValue = encryptionService.Encrypt(plainTextToken, "OAuthTokens");
    }

    public string Decrypt(IEncryptionService encryptionService)
    {
        return encryptionService.Decrypt(EncryptedValue, "OAuthTokens");
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return EncryptedValue;
    }
}
```

**File: `Authentication/Domain/Entities/User.cs`** (ENHANCE - add methods)
```csharp
// Add to existing User aggregate
public class User : AggregateRoot<Guid>
{
    private readonly List<OAuthAccount> _oauthAccounts = new();
    public IReadOnlyList<OAuthAccount> OAuthAccounts => _oauthAccounts.AsReadOnly();

    // OAuth Methods
    public void LinkOAuthAccount(OAuthProvider provider, string providerUserId, OAuthToken accessToken, OAuthToken? refreshToken = null)
    {
        if (_oauthAccounts.Any(a => a.Provider.Value == provider.Value))
            throw new DomainException($"OAuth account for {provider.Value} already linked");

        var account = new OAuthAccount(provider, providerUserId, accessToken, refreshToken);
        _oauthAccounts.Add(account);

        // Domain event (optional)
        AddDomainEvent(new OAuthAccountLinkedEvent(Id, provider));
    }

    public void UnlinkOAuthAccount(OAuthProvider provider)
    {
        var account = _oauthAccounts.FirstOrDefault(a => a.Provider.Value == provider.Value);
        if (account == null)
            throw new DomainException($"No OAuth account found for {provider.Value}");

        _oauthAccounts.Remove(account);

        // Domain event (optional)
        AddDomainEvent(new OAuthAccountUnlinkedEvent(Id, provider));
    }

    public OAuthAccount? GetOAuthAccount(OAuthProvider provider)
    {
        return _oauthAccounts.FirstOrDefault(a => a.Provider.Value == provider.Value);
    }

    public bool HasOAuthAccount(OAuthProvider provider)
    {
        return _oauthAccounts.Any(a => a.Provider.Value == provider.Value);
    }
}
```

#### Application Layer (6 files, ~450 lines)

**File: `Authentication/Application/Commands/LinkOAuthAccountCommand.cs`**
```csharp
namespace Api.BoundedContexts.Authentication.Application.Commands;

public record LinkOAuthAccountCommand(
    Guid UserId,
    string Provider,
    string ProviderUserId,
    string AccessToken,
    string? RefreshToken = null
) : ICommand;

public class LinkOAuthAccountCommandHandler : ICommandHandler<LinkOAuthAccountCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<LinkOAuthAccountCommandHandler> _logger;

    public LinkOAuthAccountCommandHandler(
        IUserRepository userRepository,
        IEncryptionService encryptionService,
        ILogger<LinkOAuthAccountCommandHandler> logger)
    {
        _userRepository = userRepository;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task Handle(LinkOAuthAccountCommand command, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user == null)
            throw new NotFoundException($"User {command.UserId} not found");

        var provider = new OAuthProvider(command.Provider);
        var accessToken = new OAuthToken(command.AccessToken, _encryptionService);
        OAuthToken? refreshToken = command.RefreshToken != null
            ? new OAuthToken(command.RefreshToken, _encryptionService)
            : null;

        user.LinkOAuthAccount(provider, command.ProviderUserId, accessToken, refreshToken);

        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("OAuth account linked: User {UserId}, Provider {Provider}", command.UserId, command.Provider);
    }
}
```

**File: `Authentication/Application/Commands/UnlinkOAuthAccountCommand.cs`**
```csharp
public record UnlinkOAuthAccountCommand(
    Guid UserId,
    string Provider
) : ICommand;

public class UnlinkOAuthAccountCommandHandler : ICommandHandler<UnlinkOAuthAccountCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UnlinkOAuthAccountCommandHandler> _logger;

    public async Task Handle(UnlinkOAuthAccountCommand command, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user == null)
            throw new NotFoundException($"User {command.UserId} not found");

        var provider = new OAuthProvider(command.Provider);
        user.UnlinkOAuthAccount(provider);

        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("OAuth account unlinked: User {UserId}, Provider {Provider}", command.UserId, command.Provider);
    }
}
```

**File: `Authentication/Application/Queries/GetLinkedOAuthAccountsQuery.cs`**
```csharp
public record GetLinkedOAuthAccountsQuery(Guid UserId) : IQuery<List<OAuthAccountDto>>;

public class GetLinkedOAuthAccountsQueryHandler : IQueryHandler<GetLinkedOAuthAccountsQuery, List<OAuthAccountDto>>
{
    private readonly IUserRepository _userRepository;

    public async Task<List<OAuthAccountDto>> Handle(GetLinkedOAuthAccountsQuery query, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(query.UserId, cancellationToken);
        if (user == null)
            return new List<OAuthAccountDto>();

        return user.OAuthAccounts.Select(a => new OAuthAccountDto
        {
            Provider = a.Provider.Value,
            ProviderUserId = a.ProviderUserId,
            LinkedAt = a.LinkedAt,
            IsTokenExpired = a.IsTokenExpired()
        }).ToList();
    }
}
```

**File: `Authentication/Application/DTOs/OAuthAccountDto.cs`**
```csharp
public record OAuthAccountDto
{
    public required string Provider { get; init; }
    public required string ProviderUserId { get; init; }
    public required DateTime LinkedAt { get; init; }
    public required bool IsTokenExpired { get; init; }
}
```

#### Infrastructure Layer (1 file enhanced, ~100 lines)

**File: `Authentication/Infrastructure/Repositories/UserRepository.cs`** (ENHANCE)
```csharp
public class UserRepository : IUserRepository
{
    private readonly MeepleAiDbContext _context;

    // Add OAuth-specific query methods
    public async Task<User?> GetByOAuthProviderAsync(string provider, string providerUserId, CancellationToken cancellationToken = default)
    {
        var userEntity = await _context.Users
            .Include(u => u.OAuthAccounts)
            .FirstOrDefaultAsync(u => u.OAuthAccounts.Any(
                a => a.Provider == provider && a.ProviderUserId == providerUserId),
                cancellationToken);

        return userEntity != null ? MapToDomain(userEntity) : null;
    }

    public async Task<List<User>> GetUsersWithOAuthProviderAsync(string provider, CancellationToken cancellationToken = default)
    {
        var userEntities = await _context.Users
            .Include(u => u.OAuthAccounts)
            .Where(u => u.OAuthAccounts.Any(a => a.Provider == provider))
            .ToListAsync(cancellationToken);

        return userEntities.Select(MapToDomain).ToList();
    }

    // Enhanced UpdateAsync to handle OAuthAccounts
    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Users
            .Include(u => u.OAuthAccounts)
            .FirstOrDefaultAsync(u => u.Id == user.Id.ToString(), cancellationToken);

        if (entity == null)
            throw new NotFoundException($"User {user.Id} not found");

        // Map domain User to persistence UserEntity (including OAuthAccounts)
        MapToPersistence(user, entity);

        await _context.SaveChangesAsync(cancellationToken);
    }

    // Mapping methods
    private void MapToPersistence(User domainUser, UserEntity entity)
    {
        // ... existing mappings ...

        // Map OAuth accounts
        entity.OAuthAccounts.Clear();
        foreach (var domainAccount in domainUser.OAuthAccounts)
        {
            entity.OAuthAccounts.Add(new OAuthAccountEntity
            {
                Id = domainAccount.Id.ToString(),
                UserId = entity.Id,
                Provider = domainAccount.Provider.Value,
                ProviderUserId = domainAccount.ProviderUserId,
                AccessTokenEncrypted = domainAccount.AccessToken.EncryptedValue,
                RefreshTokenEncrypted = domainAccount.RefreshToken?.EncryptedValue,
                TokenExpiresAt = domainAccount.TokenExpiresAt,
                LinkedAt = domainAccount.LinkedAt
            });
        }
    }
}
```

#### Endpoint Layer (Program.cs updates)

**File: `src/Api/Program.cs`** (ENHANCE - OAuth endpoints)
```csharp
// Replace existing OAuth endpoints with CQRS-based ones
var authGroup = v1Api.MapGroup("/auth");

authGroup.MapPost("/oauth/link", async (
    LinkOAuthAccountCommand command,
    IMediator mediator) =>
{
    await mediator.Send(command);
    return Results.Ok(new { message = "OAuth account linked successfully" });
})
.RequireAuthorization()
.WithName("LinkOAuthAccount")
.WithOpenApi();

authGroup.MapDelete("/oauth/unlink/{provider}", async (
    string provider,
    ClaimsPrincipal user,
    IMediator mediator) =>
{
    var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    await mediator.Send(new UnlinkOAuthAccountCommand(userId, provider));
    return Results.Ok(new { message = "OAuth account unlinked" });
})
.RequireAuthorization()
.WithName("UnlinkOAuthAccount")
.WithOpenApi();

authGroup.MapGet("/oauth/accounts", async (
    ClaimsPrincipal user,
    IMediator mediator) =>
{
    var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var accounts = await mediator.Send(new GetLinkedOAuthAccountsQuery(userId));
    return Results.Ok(accounts);
})
.RequireAuthorization()
.WithName("GetLinkedOAuthAccounts")
.WithOpenApi();
```

#### Database Migration

**Migration Name**: `20251111_AddOAuthAccountsToDDD`

```csharp
// No schema changes needed - oauth_accounts table already exists
// But add mapping in DbContext for OAuthAccountEntity if not present

public class MeepleAiDbContext : DbContext
{
    public DbSet<OAuthAccountEntity> OAuthAccounts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ... existing ...

        modelBuilder.Entity<OAuthAccountEntity>(entity =>
        {
            entity.ToTable("oauth_accounts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Provider).IsRequired();
            entity.Property(e => e.ProviderUserId).IsRequired();
            entity.HasIndex(e => new { e.Provider, e.ProviderUserId }).IsUnique();

            // Relationship
            entity.HasOne(e => e.User)
                  .WithMany(u => u.OAuthAccounts)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
```

### Implementation Checklist

- [ ] **Domain Layer**:
  - [ ] Create OAuthAccount.cs entity
  - [ ] Create OAuthProvider.cs value object
  - [ ] Create OAuthToken.cs value object
  - [ ] Enhance User.cs with LinkOAuthAccount(), UnlinkOAuthAccount()
  - [ ] Add domain events (optional): OAuthAccountLinkedEvent, OAuthAccountUnlinkedEvent

- [ ] **Application Layer**:
  - [ ] Create LinkOAuthAccountCommand + Handler
  - [ ] Create UnlinkOAuthAccountCommand + Handler
  - [ ] Create GetLinkedOAuthAccountsQuery + Handler
  - [ ] Create OAuthAccountDto

- [ ] **Infrastructure Layer**:
  - [ ] Enhance UserRepository with OAuth queries
  - [ ] Add OAuth mapping in MapToPersistence()
  - [ ] Add OAuth mapping in MapToDomain()

- [ ] **API Endpoints**:
  - [ ] Refactor existing OAuth endpoints to use MediatR + CQRS
  - [ ] Update Swagger documentation
  - [ ] Add authorization policies

- [ ] **Testing**:
  - [ ] Domain tests: User.LinkOAuthAccount validation
  - [ ] Domain tests: OAuthProvider validation (invalid provider)
  - [ ] Application tests: LinkOAuthAccountCommandHandler
  - [ ] Application tests: GetLinkedOAuthAccountsQueryHandler
  - [ ] Integration tests: Full OAuth flow with CQRS

### Acceptance Criteria

- [ ] User can link OAuth account (Google, Discord, GitHub)
- [ ] Domain validation prevents duplicate provider links
- [ ] OAuthToken encrypted at rest
- [ ] CQRS commands/queries work correctly
- [ ] Existing OAuth functionality preserved (backward compatible)
- [ ] 90%+ test coverage (domain + application)
- [ ] Integration tests pass

**Effort**: 8h
**Priority**: High (SPRINT-1 foundation)

---

## 🔨 Issue #847: 2FA/TOTP Management UI

### DDD Implementation Checklist

#### Domain Layer (3 files, ~300 lines)

**File: `Authentication/Domain/ValueObjects/TotpSecret.cs`** (NEW)
```csharp
namespace Api.BoundedContexts.Authentication.Domain.ValueObjects;

public class TotpSecret : ValueObject
{
    public string EncryptedSecret { get; private set; }
    private const int SecretLength = 32;

    private TotpSecret() { } // EF

    public TotpSecret(string plainTextSecret, IEncryptionService encryptionService)
    {
        if (string.IsNullOrWhiteSpace(plainTextSecret))
            throw new DomainException("TOTP secret cannot be empty");

        if (plainTextSecret.Length != SecretLength)
            throw new DomainException($"TOTP secret must be {SecretLength} characters");

        EncryptedSecret = encryptionService.Encrypt(plainTextSecret, "TotpSecrets");
    }

    public string Decrypt(IEncryptionService encryptionService)
    {
        return encryptionService.Decrypt(EncryptedSecret, "TotpSecrets");
    }

    public bool Verify(string code, IEncryptionService encryptionService, ITotpService totpService)
    {
        var secret = Decrypt(encryptionService);
        return totpService.ValidateCode(secret, code);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return EncryptedSecret;
    }
}
```

**File: `Authentication/Domain/ValueObjects/BackupCode.cs`** (NEW)
```csharp
namespace Api.BoundedContexts.Authentication.Domain.ValueObjects;

public class BackupCode : ValueObject
{
    public string CodeHash { get; private set; }
    public bool IsUsed { get; private set; }
    public DateTime? UsedAt { get; private set; }

    private BackupCode() { } // EF

    public BackupCode(string plainTextCode)
    {
        if (string.IsNullOrWhiteSpace(plainTextCode))
            throw new DomainException("Backup code cannot be empty");

        if (plainTextCode.Length != 9) // Format: XXXX-XXXX
            throw new DomainException("Backup code must be 9 characters (XXXX-XXXX format)");

        CodeHash = HashCode(plainTextCode);
        IsUsed = false;
        UsedAt = null;
    }

    public bool Verify(string plainTextCode)
    {
        if (IsUsed)
            return false;

        return VerifyHash(plainTextCode, CodeHash);
    }

    public void MarkAsUsed()
    {
        if (IsUsed)
            throw new DomainException("Backup code already used");

        IsUsed = true;
        UsedAt = DateTime.UtcNow;
    }

    private static string HashCode(string code)
    {
        // Use PBKDF2 hashing (same as passwords)
        using var deriveBytes = new Rfc2898DeriveBytes(code,
            new byte[16], // Salt (simplified for demo)
            210000,
            HashAlgorithmName.SHA256);
        return Convert.ToBase64String(deriveBytes.GetBytes(32));
    }

    private static bool VerifyHash(string code, string hash)
    {
        return HashCode(code) == hash;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return CodeHash;
        yield return IsUsed;
    }
}
```

**File: `Authentication/Domain/Entities/User.cs`** (ENHANCE - add 2FA methods)
```csharp
public class User : AggregateRoot<Guid>
{
    private TotpSecret? _totpSecret;
    private readonly List<BackupCode> _backupCodes = new();

    public bool IsTwoFactorEnabled => _totpSecret != null;
    public DateTime? TwoFactorEnabledAt { get; private set; }
    public IReadOnlyList<BackupCode> BackupCodes => _backupCodes.AsReadOnly();

    // 2FA Methods
    public (TotpSecret secret, List<BackupCode> backupCodes) Setup2FA(
        IEncryptionService encryptionService,
        ITotpService totpService)
    {
        if (_totpSecret != null)
            throw new DomainException("2FA already enabled");

        // Generate TOTP secret
        var plainSecret = totpService.GenerateSecret();
        var secret = new TotpSecret(plainSecret, encryptionService);

        // Generate 10 backup codes
        var backupCodes = Enumerable.Range(0, 10)
            .Select(_ => new BackupCode(GenerateBackupCodeFormat()))
            .ToList();

        // Don't save yet - user must verify first
        return (secret, backupCodes);
    }

    public void Enable2FA(TotpSecret secret, List<BackupCode> backupCodes, string verificationCode, IEncryptionService encryptionService, ITotpService totpService)
    {
        if (_totpSecret != null)
            throw new DomainException("2FA already enabled");

        // Verify code before enabling
        if (!secret.Verify(verificationCode, encryptionService, totpService))
            throw new DomainException("Invalid TOTP code");

        _totpSecret = secret;
        _backupCodes.Clear();
        _backupCodes.AddRange(backupCodes);
        TwoFactorEnabledAt = DateTime.UtcNow;

        AddDomainEvent(new TwoFactorEnabledEvent(Id));
    }

    public bool Verify2FA(string code, IEncryptionService encryptionService, ITotpService totpService)
    {
        if (_totpSecret == null)
            throw new DomainException("2FA not enabled");

        // Try TOTP code first
        if (_totpSecret.Verify(code, encryptionService, totpService))
            return true;

        // Try backup codes
        var backupCode = _backupCodes.FirstOrDefault(c => !c.IsUsed && c.Verify(code));
        if (backupCode != null)
        {
            backupCode.MarkAsUsed();
            return true;
        }

        return false;
    }

    public void Disable2FA(string password, string verificationCode, IEncryptionService encryptionService, ITotpService totpService)
    {
        if (_totpSecret == null)
            throw new DomainException("2FA not enabled");

        // Verify password AND 2FA code
        if (!PasswordHash.Verify(password))
            throw new DomainException("Invalid password");

        if (!Verify2FA(verificationCode, encryptionService, totpService))
            throw new DomainException("Invalid 2FA code");

        _totpSecret = null;
        _backupCodes.Clear();
        TwoFactorEnabledAt = null;

        AddDomainEvent(new TwoFactorDisabledEvent(Id));
    }

    private static string GenerateBackupCodeFormat()
    {
        // Generate XXXX-XXXX format
        var random = new Random();
        var part1 = random.Next(1000, 9999);
        var part2 = random.Next(1000, 9999);
        return $"{part1}-{part2}";
    }
}
```

#### Application Layer (4 files, ~350 lines)

**Commands**: Setup2FACommand, Enable2FACommand, Verify2FACommand, Disable2FACommand
**Queries**: Get2FAStatusQuery, GetBackupCodesCountQuery
**Handlers**: 6 handlers total
**DTOs**: TwoFactorSetupDto, TwoFactorStatusDto

(See full implementations in issue #847 updated body)

### Implementation Checklist

- [ ] **Domain Layer**:
  - [ ] Create TotpSecret.cs value object (encryption, verification)
  - [ ] Create BackupCode.cs value object (hashing, single-use)
  - [ ] Enhance User.cs with Setup2FA(), Enable2FA(), Verify2FA(), Disable2FA()
  - [ ] Add domain events: TwoFactorEnabledEvent, TwoFactorDisabledEvent

- [ ] **Application Layer**:
  - [ ] Setup2FACommand + Handler (generate secret + codes)
  - [ ] Enable2FACommand + Handler (verify + persist)
  - [ ] Verify2FACommand + Handler (TOTP or backup code)
  - [ ] Disable2FACommand + Handler (password + code verification)
  - [ ] Get2FAStatusQuery + Handler
  - [ ] DTOs: TwoFactorSetupDto, TwoFactorStatusDto

- [ ] **Infrastructure Layer**:
  - [ ] Enhance UserRepository with 2FA persistence
  - [ ] Mapping: Domain BackupCode ↔ Persistence BackupCodeEntity
  - [ ] Handle backup code single-use tracking

- [ ] **API Endpoints**:
  - [ ] POST /api/v1/auth/2fa/setup (Setup2FACommand)
  - [ ] POST /api/v1/auth/2fa/enable (Enable2FACommand)
  - [ ] POST /api/v1/auth/2fa/verify (Verify2FACommand)
  - [ ] POST /api/v1/auth/2fa/disable (Disable2FACommand)
  - [ ] GET /api/v1/users/me/2fa/status (Get2FAStatusQuery)

- [ ] **Testing**:
  - [ ] Domain tests: TotpSecret validation + encryption
  - [ ] Domain tests: BackupCode hashing + single-use
  - [ ] Domain tests: User.Enable2FA validation rules
  - [ ] Application tests: All command/query handlers
  - [ ] Integration tests: Complete 2FA flow (setup → enable → verify → disable)

**Effort**: 10h
**Priority**: High

---

## 🔨 Issue #848: Settings Pages - 4 Tabs Implementation

### DDD Integration (UI Layer)

**No Domain Layer Changes** - UI consumes Application layer via CQRS

**CQRS Consumption Pattern**:
```typescript
// pages/settings/profile.tsx
const { data: profile, isLoading } = useQuery(
  ['user', 'profile'],
  () => api.auth.getUserProfile() // → GetUserProfileQuery
);

const updateProfile = useMutation(
  (data: UpdateProfileData) => api.auth.updateProfile(data), // → UpdateUserProfileCommand
  {
    onSuccess: () => {
      toast.success('Profile updated');
      queryClient.invalidateQueries(['user', 'profile']);
    }
  }
);
```

### Implementation Checklist

- [ ] **Profile Tab** (`/settings/profile`):
  - [ ] Uses GetUserProfileQuery for display
  - [ ] Uses UpdateUserProfileCommand for changes
  - [ ] Form validation (client-side + server-side via command)

- [ ] **Privacy Tab** (`/settings/privacy`):
  - [ ] 2FA Section:
    - [ ] Get2FAStatusQuery → display status
    - [ ] Setup2FACommand → QR code modal
    - [ ] Enable2FACommand → verify code modal
    - [ ] Disable2FACommand → password + code confirmation
  - [ ] Session Management:
    - [ ] GetActiveSessionsQuery → list sessions
    - [ ] RevokeSessionCommand → revoke button

- [ ] **Preferences Tab** (`/settings/preferences`):
  - [ ] (No DDD - user preferences, can use legacy)

- [ ] **Advanced Tab** (`/settings/advanced`):
  - [ ] OAuth Accounts:
    - [ ] GetLinkedOAuthAccountsQuery → list linked accounts
    - [ ] LinkOAuthAccountCommand → link button (redirect to OAuth flow)
    - [ ] UnlinkOAuthAccountCommand → unlink button

**Effort**: 10h (no change - UI layer)
**DDD Impact**: Minimal (just API client changes to use CQRS endpoints)

---

## 🔨 Issue #849: User Profile Management Service

### DDD Implementation (Application Service with CQRS)

This service becomes a **thin orchestration layer** - business logic moves to domain, CQRS handles requests.

#### Application Layer (6 files, ~400 lines)

**File: `Authentication/Application/Commands/UpdateUserProfileCommand.cs`**
```csharp
public record UpdateUserProfileCommand(
    Guid UserId,
    string DisplayName,
    string Email
) : ICommand;

public class UpdateUserProfileCommandHandler : ICommandHandler<UpdateUserProfileCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UpdateUserProfileCommandHandler> _logger;

    public async Task Handle(UpdateUserProfileCommand command, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user == null)
            throw new NotFoundException($"User {command.UserId} not found");

        // Domain validation happens in User.UpdateProfile()
        var email = new Email(command.Email); // Email VO validates format
        user.UpdateProfile(command.DisplayName, email);

        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("User profile updated: {UserId}", command.UserId);
    }
}
```

**File: `Authentication/Application/Commands/ChangePasswordCommand.cs`**
```csharp
public record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword
) : ICommand;

public class ChangePasswordCommandHandler : ICommandHandler<ChangePasswordCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public async Task Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user == null)
            throw new NotFoundException($"User {command.UserId} not found");

        // Domain method validates current password
        user.ChangePassword(command.CurrentPassword, command.NewPassword, _passwordHasher);

        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Password changed: User {UserId}", command.UserId);

        // Optional: Invalidate all sessions except current (security best practice)
        // await _sessionRepository.RevokeAllSessionsExceptAsync(user.Id, currentSessionId);
    }
}
```

**File: `Authentication/Application/Queries/GetUserProfileQuery.cs`**
```csharp
public record GetUserProfileQuery(Guid UserId) : IQuery<UserProfileDto>;

public class GetUserProfileQueryHandler : IQueryHandler<GetUserProfileQuery, UserProfileDto>
{
    private readonly IUserRepository _userRepository;

    public async Task<UserProfileDto> Handle(GetUserProfileQuery query, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(query.UserId, cancellationToken);
        if (user == null)
            throw new NotFoundException($"User {query.UserId} not found");

        return new UserProfileDto
        {
            UserId = user.Id,
            Email = user.Email.Value,
            DisplayName = user.DisplayName,
            Role = user.Role.ToString(),
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            IsTwoFactorEnabled = user.IsTwoFactorEnabled,
            OAuthAccountsCount = user.OAuthAccounts.Count
        };
    }
}
```

**File: `Authentication/Application/DTOs/UserProfileDto.cs`**
```csharp
public record UserProfileDto
{
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public required string Role { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public required bool IsTwoFactorEnabled { get; init; }
    public required int OAuthAccountsCount { get; init; }
}
```

#### Domain Enhancements

**File: `Authentication/Domain/Entities/User.cs`** (ENHANCE)
```csharp
// Add profile management methods
public void UpdateProfile(string displayName, Email email)
{
    if (string.IsNullOrWhiteSpace(displayName))
        throw new DomainException("Display name is required");

    if (displayName.Length > 100)
        throw new DomainException("Display name too long (max 100 characters)");

    DisplayName = displayName;
    Email = email; // Email VO already validates format

    AddDomainEvent(new UserProfileUpdatedEvent(Id, displayName, email.Value));
}

public void ChangePassword(string currentPassword, string newPassword, IPasswordHasher hasher)
{
    if (!PasswordHash.Verify(currentPassword, hasher))
        throw new DomainException("Current password is incorrect");

    if (newPassword.Length < 8)
        throw new DomainException("New password must be at least 8 characters");

    if (!HasUpperCase(newPassword) || !HasDigit(newPassword))
        throw new DomainException("Password must contain uppercase letter and digit");

    PasswordHash = PasswordHash.Create(newPassword, hasher);

    AddDomainEvent(new PasswordChangedEvent(Id));
}

private static bool HasUpperCase(string str) => str.Any(char.IsUpper);
private static bool HasDigit(string str) => str.Any(char.IsDigit);
```

### Implementation Checklist

- [ ] **Domain Methods**:
  - [ ] User.UpdateProfile(displayName, email) - validation logic
  - [ ] User.ChangePassword(current, new, hasher) - password rules
  - [ ] Domain events: UserProfileUpdatedEvent, PasswordChangedEvent

- [ ] **Application Commands**:
  - [ ] UpdateUserProfileCommand + Handler
  - [ ] ChangePasswordCommand + Handler
  - [ ] GetUserProfileQuery + Handler

- [ ] **API Endpoints**:
  - [ ] PUT /api/v1/users/me/profile (UpdateUserProfileCommand)
  - [ ] POST /api/v1/users/me/change-password (ChangePasswordCommand)
  - [ ] GET /api/v1/users/me/profile (GetUserProfileQuery)

- [ ] **Testing**:
  - [ ] Domain tests: UpdateProfile validation (empty name, too long)
  - [ ] Domain tests: ChangePassword validation (weak password, wrong current)
  - [ ] Application tests: Command/Query handlers
  - [ ] Integration tests: Full profile update flow

**Effort**: 8h
**Priority**: Medium

---

## 🔨 Issue #850: Unit Test Suite - Authentication Module

### DDD Test Strategy (Comprehensive)

#### Test Organization

```
tests/Api.Tests/BoundedContexts/Authentication/
├── Domain/
│   ├── Entities/
│   │   └── UserTests.cs (COMPREHENSIVE)
│   └── ValueObjects/
│       ├── EmailTests.cs (exists from Phase 2)
│       ├── TotpSecretTests.cs (NEW - #847)
│       ├── BackupCodeTests.cs (NEW - #847)
│       ├── OAuthProviderTests.cs (NEW - #846)
│       └── OAuthTokenTests.cs (NEW - #846)
├── Application/
│   ├── Commands/
│   │   ├── LinkOAuthAccountCommandHandlerTests.cs (NEW - #846)
│   │   ├── Enable2FACommandHandlerTests.cs (NEW - #847)
│   │   ├── UpdateUserProfileCommandHandlerTests.cs (NEW - #849)
│   │   └── ChangePasswordCommandHandlerTests.cs (NEW - #849)
│   └── Queries/
│       ├── GetLinkedOAuthAccountsQueryHandlerTests.cs (NEW - #846)
│       ├── Get2FAStatusQueryHandlerTests.cs (NEW - #847)
│       └── GetUserProfileQueryHandlerTests.cs (NEW - #849)
└── Infrastructure/
    └── Repositories/
        └── UserRepositoryTests.cs (ENHANCE - OAuth + 2FA)
```

#### Domain Tests (UserTests.cs) - Comprehensive

**File: `tests/Api.Tests/BoundedContexts/Authentication/Domain/Entities/UserTests.cs`**

```csharp
public class UserTests
{
    // OAuth Tests
    [Fact]
    public void LinkOAuthAccount_ValidProvider_Success()
    {
        // Arrange
        var user = CreateTestUser();
        var provider = new OAuthProvider("Google");
        var token = new OAuthToken("access_token", _encryptionService);

        // Act
        user.LinkOAuthAccount(provider, "google-user-123", token);

        // Assert
        user.OAuthAccounts.Should().HaveCount(1);
        user.OAuthAccounts.First().Provider.Value.Should().Be("Google");
    }

    [Fact]
    public void LinkOAuthAccount_DuplicateProvider_ThrowsDomainException()
    {
        // Arrange
        var user = CreateTestUser();
        var provider = new OAuthProvider("Google");
        var token = new OAuthToken("token1", _encryptionService);
        user.LinkOAuthAccount(provider, "user-1", token);

        // Act & Assert
        var act = () => user.LinkOAuthAccount(provider, "user-2", new OAuthToken("token2", _encryptionService));
        act.Should().Throw<DomainException>()
           .WithMessage("*already linked*");
    }

    [Fact]
    public void UnlinkOAuthAccount_ExistingProvider_Success()
    {
        // Arrange
        var user = CreateTestUser();
        var provider = new OAuthProvider("Google");
        user.LinkOAuthAccount(provider, "user-1", new OAuthToken("token", _encryptionService));

        // Act
        user.UnlinkOAuthAccount(provider);

        // Assert
        user.OAuthAccounts.Should().BeEmpty();
    }

    [Fact]
    public void UnlinkOAuthAccount_NonExistingProvider_ThrowsDomainException()
    {
        // Arrange
        var user = CreateTestUser();
        var provider = new OAuthProvider("Google");

        // Act & Assert
        var act = () => user.UnlinkOAuthAccount(provider);
        act.Should().Throw<DomainException>()
           .WithMessage("*not found*");
    }

    // 2FA Tests
    [Fact]
    public void Enable2FA_ValidCode_Success()
    {
        // Arrange
        var user = CreateTestUser();
        var (secret, codes) = user.Setup2FA(_encryptionService, _totpService);
        var validCode = _totpService.GenerateCode(secret.Decrypt(_encryptionService));

        // Act
        user.Enable2FA(secret, codes, validCode, _encryptionService, _totpService);

        // Assert
        user.IsTwoFactorEnabled.Should().BeTrue();
        user.BackupCodes.Should().HaveCount(10);
    }

    [Fact]
    public void Enable2FA_InvalidCode_ThrowsDomainException()
    {
        // Arrange
        var user = CreateTestUser();
        var (secret, codes) = user.Setup2FA(_encryptionService, _totpService);

        // Act & Assert
        var act = () => user.Enable2FA(secret, codes, "000000", _encryptionService, _totpService);
        act.Should().Throw<DomainException>()
           .WithMessage("*Invalid TOTP code*");
    }

    [Fact]
    public void Verify2FA_ValidTotpCode_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWith2FAEnabled();
        var validCode = GenerateValidTotpCode(user);

        // Act
        var result = user.Verify2FA(validCode, _encryptionService, _totpService);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Verify2FA_ValidBackupCode_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWith2FAEnabled();
        var backupCode = user.BackupCodes.First();

        // Act
        var result = user.Verify2FA("XXXX-XXXX", _encryptionService, _totpService); // Use actual backup code

        // Assert
        result.Should().BeTrue();
        backupCode.IsUsed.Should().BeTrue();
    }

    [Fact]
    public void Verify2FA_UsedBackupCode_ReturnsFalse()
    {
        // Arrange
        var user = CreateUserWith2FAEnabled();
        user.Verify2FA("backup-code", _encryptionService, _totpService); // Use once

        // Act
        var result = user.Verify2FA("backup-code", _encryptionService, _totpService); // Try again

        // Assert
        result.Should().BeFalse(); // Single-use enforcement
    }

    [Fact]
    public void Disable2FA_ValidPasswordAndCode_Success()
    {
        // Arrange
        var user = CreateUserWith2FAEnabled();
        var password = "CurrentPassword123!";
        var validCode = GenerateValidTotpCode(user);

        // Act
        user.Disable2FA(password, validCode, _encryptionService, _totpService);

        // Assert
        user.IsTwoFactorEnabled.Should().BeFalse();
        user.BackupCodes.Should().BeEmpty();
    }

    // Profile Tests
    [Fact]
    public void UpdateProfile_ValidData_Success()
    {
        // Arrange
        var user = CreateTestUser();
        var newEmail = new Email("newemail@example.com");

        // Act
        user.UpdateProfile("New Name", newEmail);

        // Assert
        user.DisplayName.Should().Be("New Name");
        user.Email.Value.Should().Be("newemail@example.com");
    }

    [Fact]
    public void UpdateProfile_EmptyDisplayName_ThrowsDomainException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        var act = () => user.UpdateProfile("", new Email("email@test.com"));
        act.Should().Throw<DomainException>()
           .WithMessage("*Display name is required*");
    }

    [Fact]
    public void ChangePassword_ValidPasswords_Success()
    {
        // Arrange
        var user = CreateTestUser();
        var currentPassword = "OldPassword123!";
        var newPassword = "NewPassword456!";

        // Act
        user.ChangePassword(currentPassword, newPassword, _passwordHasher);

        // Assert
        user.PasswordHash.Verify(newPassword, _passwordHasher).Should().BeTrue();
    }

    [Fact]
    public void ChangePassword_WeakNewPassword_ThrowsDomainException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        var act = () => user.ChangePassword("CurrentPassword123!", "weak", _passwordHasher);
        act.Should().Throw<DomainException>()
           .WithMessage("*at least 8 characters*");
    }

    [Theory]
    [InlineData("nouppercaseornumber")]
    [InlineData("NOLOWERCASEORNUMBER")]
    [InlineData("NoNumbersHere")]
    public void ChangePassword_InvalidFormat_ThrowsDomainException(string invalidPassword)
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        var act = () => user.ChangePassword("Current123!", invalidPassword, _passwordHasher);
        act.Should().Throw<DomainException>();
    }
}
```

#### Value Object Tests

**File: `TotpSecretTests.cs`** (NEW)
```csharp
public class TotpSecretTests
{
    [Fact]
    public void Constructor_ValidSecret_CreatesEncrypted() { }

    [Fact]
    public void Constructor_InvalidLength_ThrowsDomainException() { }

    [Fact]
    public void Verify_ValidCode_ReturnsTrue() { }

    [Fact]
    public void Verify_InvalidCode_ReturnsFalse() { }

    [Fact]
    public void Decrypt_ReturnsOriginalSecret() { }
}
```

**File: `BackupCodeTests.cs`** (NEW)
```csharp
public class BackupCodeTests
{
    [Fact]
    public void Constructor_ValidFormat_Success() { }

    [Fact]
    public void Constructor_InvalidFormat_ThrowsDomainException() { }

    [Fact]
    public void Verify_ValidCode_ReturnsTrue() { }

    [Fact]
    public void Verify_InvalidCode_ReturnsFalse() { }

    [Fact]
    public void MarkAsUsed_FirstTime_Success() { }

    [Fact]
    public void MarkAsUsed_AlreadyUsed_ThrowsDomainException() { }

    [Fact]
    public void Verify_UsedCode_ReturnsFalse() { } // Single-use enforcement
}
```

#### Application Tests

**File: `LinkOAuthAccountCommandHandlerTests.cs`** (NEW)
```csharp
public class LinkOAuthAccountCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_LinksAccount() { }

    [Fact]
    public async Task Handle_UserNotFound_ThrowsNotFoundException() { }

    [Fact]
    public async Task Handle_DuplicateProvider_ThrowsDomainException() { }

    [Fact]
    public async Task Handle_Success_CallsRepositoryUpdate() { }
}
```

### Test Coverage Targets

| Layer | Coverage Target | Rationale |
|-------|----------------|-----------|
| **Domain** | 95%+ | Critical business rules, high value |
| **Application** | 90%+ | Command/Query handlers, orchestration |
| **Infrastructure** | 85%+ | Repository implementations, mapping |
| **Overall** | 90%+ | Enforced in CI |

### Implementation Checklist

- [ ] **Domain Tests** (95%+ coverage):
  - [ ] UserTests.cs - OAuth methods (6 tests)
  - [ ] UserTests.cs - 2FA methods (8 tests)
  - [ ] UserTests.cs - Profile methods (6 tests)
  - [ ] TotpSecretTests.cs (5 tests)
  - [ ] BackupCodeTests.cs (7 tests)
  - [ ] OAuthProviderTests.cs (3 tests)
  - [ ] OAuthTokenTests.cs (3 tests)

- [ ] **Application Tests** (90%+ coverage):
  - [ ] LinkOAuthAccountCommandHandlerTests.cs (4 tests)
  - [ ] UnlinkOAuthAccountCommandHandlerTests.cs (3 tests)
  - [ ] Enable2FACommandHandlerTests.cs (5 tests)
  - [ ] Disable2FACommandHandlerTests.cs (4 tests)
  - [ ] GetUserProfileQueryHandlerTests.cs (3 tests)
  - [ ] UpdateUserProfileCommandHandlerTests.cs (4 tests)
  - [ ] ChangePasswordCommandHandlerTests.cs (5 tests)

- [ ] **Infrastructure Tests** (85%+ coverage):
  - [ ] UserRepositoryTests.cs - OAuth persistence (4 tests)
  - [ ] UserRepositoryTests.cs - 2FA persistence (3 tests)
  - [ ] UserRepositoryTests.cs - Mapping validation (3 tests)

- [ ] **Integration Tests** (critical paths):
  - [ ] OAuth flow: Link → Use → Unlink
  - [ ] 2FA flow: Setup → Enable → Verify → Disable
  - [ ] Profile flow: Get → Update → Verify

**Total Test Count**: ~70 tests
**Effort**: 15h
**Priority**: Critical (blocks PR merge)

---

## 📋 Implementation Order (Recommended)

### Week 1: Domain + Application Foundation

**Day 1-2: Issue #849 (User Profile)** - 8h
- Foundation for other features
- UpdateProfile + ChangePassword domain methods
- Commands/Queries/Handlers
- Tests (domain + application)

**Day 3: Issue #846 (OAuth) - Part 1** - 4h
- OAuthAccount entity + value objects
- User.LinkOAuthAccount() + UnlinkOAuthAccount()
- Domain tests

**Day 4: Issue #846 (OAuth) - Part 2** - 4h
- CQRS commands/queries
- Handlers
- Application tests

**Day 5: Issue #847 (2FA) - Part 1** - 5h
- TotpSecret + BackupCode value objects
- User.Enable2FA() + Verify2FA() + Disable2FA()
- Domain tests

### Week 2: UI + Integration + Comprehensive Testing

**Day 6: Issue #847 (2FA) - Part 2** - 5h
- CQRS commands/queries
- Handlers
- Application tests

**Day 7-8: Issue #848 (Settings Pages)** - 10h
- UI implementation (4 tabs)
- CQRS integration
- React component tests

**Day 9-10: Issue #850 (Comprehensive Testing)** - 15h
- Complete domain test suite (95%+ coverage)
- Complete application test suite (90%+)
- Infrastructure tests
- Integration tests (E2E flows)
- Code coverage validation

**Total**: 51h over 10 working days (2 weeks)

---

## ✅ Definition of Done (SPRINT-1 with Full DDD)

### Domain Layer Complete
- [ ] All entities have rich behavior (not anemic)
- [ ] All value objects enforce invariants
- [ ] Domain methods encapsulate business rules
- [ ] Domain events raised for significant changes
- [ ] 95%+ test coverage on domain logic

### Application Layer Complete
- [ ] All commands/queries implement ICommand/IQuery
- [ ] All handlers registered with MediatR
- [ ] DTOs properly mapped from domain
- [ ] 90%+ test coverage on handlers

### Infrastructure Layer Complete
- [ ] Repositories implement domain interfaces
- [ ] Mapping (domain ↔ persistence) works correctly
- [ ] No domain logic in repositories (only persistence)
- [ ] 85%+ test coverage

### API Layer Complete
- [ ] Endpoints use MediatR (no direct service calls)
- [ ] Authorization policies enforced
- [ ] Swagger documentation updated
- [ ] Input validation via commands

### Testing Complete
- [ ] Domain tests: 95%+ coverage
- [ ] Application tests: 90%+ coverage
- [ ] Infrastructure tests: 85%+ coverage
- [ ] Integration tests: Critical paths covered
- [ ] All tests green in CI

### Documentation Complete
- [ ] Architecture Decision Record for OAuth + 2FA DDD approach
- [ ] Domain model documentation (entities, VOs, methods)
- [ ] API endpoint documentation (Swagger)
- [ ] Testing strategy documented

---

## 🎯 Success Metrics

### Code Quality
- Domain layer: 95%+ test coverage
- Overall: 90%+ test coverage
- 0 code smells (SonarQube)
- Cyclomatic complexity < 10

### DDD Compliance
- All business logic in domain layer
- Application layer is thin orchestration
- Infrastructure has no business logic
- Proper use of aggregates, entities, value objects

### Performance
- Command execution < 200ms (P95)
- Query execution < 100ms (P95)
- No N+1 queries (repository efficiency)

---

## 📚 Reference Implementation

See existing DDD Phase 2 + Phase 3 for patterns:
- `src/Api/BoundedContexts/KnowledgeBase/` - Complete DDD example
- `src/Api/SharedKernel/` - Base classes and interfaces
- `tests/Api.Tests/BoundedContexts/KnowledgeBase/` - Test patterns

---

**SPRINT-1 Implementation Guide Complete! Ready to code with full DDD approach! 🚀**
