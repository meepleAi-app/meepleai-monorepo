# Issue: Hardcoded Demo Credentials

**ID**: SEC-001
**Category**: Security
**Priority**: 🔴 **CRITICAL**
**Status**: 🔴 Open
**Created**: 2025-11-19

---

## 📋 Summary

Rimuovere password demo hardcoded nel codice frontend. Attualmente la password `Demo123!` è visibile nel bundle JavaScript, rappresentando un rischio di sicurezza.

---

## 🎯 Problem Statement

### Current State
```typescript
// ❌ PROBLEMA: apps/web/src/components/pages/HomePage.tsx:51-54
const handleTryDemo = () => {
  setDemoEmail("user@meepleai.dev");
  setDemoPassword("Demo123!");  // ⚠️ Password in chiaro nel codice!
  setShowAuthModal(true);
};
```

### Security Risks

#### 🔴 **HIGH**: Credentials Exposure
- **Password visibile nel bundle** - Chiunque può vedere la password nel JavaScript minified
- **Git history** - Password nel source control per sempre
- **Production bundle** - Password shipped to production

#### 🟡 **MEDIUM**: Account Compromise
- **Demo accounts vulnerabili** - Password nota può permettere accesso non autorizzato
- **Rate limiting bypass** - Attackers possono usare demo accounts per bypass

#### 🟢 **LOW**: Social Engineering
- **Password pattern exposure** - Rivela policy di password (`Demo123!` pattern)

---

## 📊 Affected Files

### Frontend
```
apps/web/src/components/pages/HomePage.tsx:51-54
```

### Environment Variables (da creare)
```
apps/web/.env.local
apps/web/.env.production
```

---

## 🔧 Solution

### Architecture

```
┌─────────────┐
│  Frontend   │
│  HomePage   │
└──────┬──────┘
       │ 1. Click "Try Demo"
       │ POST /api/v1/auth/demo-login
       │ { email: "user@meepleai.dev" }
       ▼
┌─────────────┐
│   Backend   │
│  Auth API   │
└──────┬──────┘
       │ 2. Check IsDemoAccount flag
       │ 3. Create session (no password)
       │ 4. Return session token
       ▼
┌─────────────┐
│  Frontend   │
│  Redirect   │
└─────────────┘
```

### Implementation

#### Step 1: Backend - Add Demo User Flag (1h)

**File**: `apps/api/src/Api/Infrastructure/Data/Entities/User.cs`
```csharp
public class User
{
    // ... existing properties
    public bool IsDemoAccount { get; set; }
}

// Migration
public partial class AddIsDemoAccountFlag : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsDemoAccount",
            table: "Users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        // Mark existing demo users
        migrationBuilder.Sql(@"
            UPDATE ""Users""
            SET ""IsDemoAccount"" = true
            WHERE ""Email"" IN (
                'admin@meepleai.dev',
                'editor@meepleai.dev',
                'user@meepleai.dev'
            )
        ");
    }
}
```

#### Step 2: Backend - Demo Login Endpoint (30min)

**File**: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/DemoLoginCommand.cs`
```csharp
public record DemoLoginCommand(string Email) : IRequest<Result<LoginResponse>>;

public class DemoLoginHandler : IRequestHandler<DemoLoginCommand, Result<LoginResponse>>
{
    private readonly ApplicationDbContext _context;
    private readonly ISessionService _sessionService;
    private readonly ILogger<DemoLoginHandler> _logger;

    public async Task<Result<LoginResponse>> Handle(
        DemoLoginCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find demo user
        var user = await _context.Users
            .FirstOrDefaultAsync(u =>
                u.Email == request.Email &&
                u.IsDemoAccount == true,
                cancellationToken);

        if (user == null)
        {
            return Result.Failure<LoginResponse>(
                new Error("Auth.DemoUserNotFound", "Demo user not found"));
        }

        // 2. Create session (no password validation)
        var session = await _sessionService.CreateSessionAsync(
            user.Id,
            "demo-session",
            cancellationToken);

        _logger.LogInformation(
            "Demo login successful for {Email}",
            request.Email);

        return Result.Success(new LoginResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role.ToString(),
            session.Token,
            session.ExpiresAt));
    }
}
```

**File**: `apps/api/src/Api/Routing/AuthEndpoints.cs`
```csharp
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // ... existing endpoints

        app.MapPost("/api/v1/auth/demo-login", async (
            [FromBody] DemoLoginRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new DemoLoginCommand(request.Email);
            var result = await mediator.Send(command, ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Unauthorized();
        })
        .WithName("DemoLogin")
        .WithTags("Authentication")
        .Produces<LoginResponse>(200)
        .Produces(401);
    }
}

public record DemoLoginRequest(string Email);
```

#### Step 3: Frontend - Update HomePage (30min)

**File**: `apps/web/src/components/pages/HomePage.tsx`
```typescript
// BEFORE
const handleTryDemo = () => {
  setDemoEmail("user@meepleai.dev");
  setDemoPassword("Demo123!");  // ❌ Hardcoded
  setShowAuthModal(true);
};

// AFTER
const handleTryDemo = async () => {
  const demoEmail = process.env.NEXT_PUBLIC_DEMO_EMAIL ?? 'user@meepleai.dev';
  setDemoEmail(demoEmail);

  try {
    // Call new demo login endpoint
    const response = await api.auth.demoLogin(demoEmail);

    // Success - user is logged in
    toast.success('Demo login successful!');
    router.push('/chat');
  } catch (err) {
    logger.error('Demo login failed', err);
    toast.error('Demo login failed. Please try again.');
  }
};
```

**File**: `apps/web/src/lib/api/clients/authClient.ts`
```typescript
export function createAuthClient({ httpClient }: CreateAuthClientParams) {
  return {
    // ... existing methods

    /**
     * Demo login (no password required)
     * Only works for accounts with IsDemoAccount flag
     */
    async demoLogin(email: string): Promise<LoginResponse> {
      return httpClient.post<LoginResponse>(
        '/api/v1/auth/demo-login',
        { email },
        LoginResponseSchema
      );
    },
  };
}
```

#### Step 4: Environment Variables

**File**: `apps/web/.env.local` (development)
```bash
# Demo account email (optional - has default)
NEXT_PUBLIC_DEMO_EMAIL=user@meepleai.dev
```

**File**: `apps/web/.env.production` (production)
```bash
# Demo account email
NEXT_PUBLIC_DEMO_EMAIL=user@meepleai.dev
```

**File**: `.gitignore` (verify)
```
# Environment files
.env.local
.env.*.local
```

---

## 📝 Implementation Checklist

### Backend (1h)
- [ ] Add `IsDemoAccount` property to User entity
- [ ] Create and run migration
- [ ] Mark existing demo users in database
- [ ] Create `DemoLoginCommand` and handler
- [ ] Add `/api/v1/auth/demo-login` endpoint
- [ ] Add endpoint tests
- [ ] Update Swagger/OpenAPI docs

### Frontend (30min)
- [ ] Remove hardcoded password from HomePage
- [ ] Add `demoLogin` method to authClient
- [ ] Update `handleTryDemo` to use new endpoint
- [ ] Add environment variable for demo email
- [ ] Update AuthModal to handle demo login
- [ ] Test demo login flow

### Documentation (30min)
- [ ] Update API documentation
- [ ] Update user guide for demo accounts
- [ ] Update developer setup guide
- [ ] Add security note in README

---

## ✅ Acceptance Criteria

### Must Have
- [ ] No hardcoded passwords in frontend code
- [ ] Demo login works via backend endpoint
- [ ] Demo users marked with `IsDemoAccount` flag
- [ ] Environment variables configured
- [ ] Tests pass

### Should Have
- [ ] Demo login rate limited (e.g., 10 req/min per IP)
- [ ] Audit log for demo logins
- [ ] Demo session shorter TTL (e.g., 1 hour vs 24 hours)

### Nice to Have
- [ ] Demo account refresh cron job (reset data)
- [ ] Demo mode indicator in UI
- [ ] Analytics for demo usage

---

## 🧪 Testing Strategy

### Unit Tests

**Backend**:
```csharp
[Fact]
public async Task DemoLogin_WithValidDemoUser_ReturnsSuccess()
{
    // Arrange
    var command = new DemoLoginCommand("user@meepleai.dev");

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Email.Should().Be("user@meepleai.dev");
}

[Fact]
public async Task DemoLogin_WithNonDemoUser_ReturnsFailure()
{
    // Arrange
    var command = new DemoLoginCommand("regular@example.com");

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    result.IsFailure.Should().BeTrue();
}
```

**Frontend**:
```typescript
describe('HomePage - Demo Login', () => {
  it('should call demoLogin when Try Demo clicked', async () => {
    const mockDemoLogin = jest.fn().mockResolvedValue({
      user: { id: '1', email: 'user@meepleai.dev' },
    });

    render(<HomePage />);

    const tryDemoButton = screen.getByText('Try Demo');
    await userEvent.click(tryDemoButton);

    expect(mockDemoLogin).toHaveBeenCalledWith('user@meepleai.dev');
  });
});
```

### Integration Tests
```typescript
describe('Demo Login E2E', () => {
  it('should complete full demo login flow', async () => {
    const page = await browser.newPage();

    // 1. Navigate to homepage
    await page.goto('http://localhost:3000');

    // 2. Click "Try Demo"
    await page.click('[data-testid="hero-try-demo"]');

    // 3. Should be redirected to chat
    await page.waitForURL('**/chat');

    // 4. User should be logged in
    const userMenu = await page.locator('[data-testid="user-menu"]');
    await expect(userMenu).toBeVisible();
  });
});
```

### Security Tests
```bash
# 1. Verify no passwords in bundle
pnpm build
grep -r "Demo123" .next/ || echo "✅ No hardcoded password found"

# 2. Verify Git history
git log -S "Demo123!" --all || echo "✅ No password in Git history"

# 3. Rate limiting test
for i in {1..20}; do
  curl -X POST http://localhost:5080/api/v1/auth/demo-login \
    -H "Content-Type: application/json" \
    -d '{"email":"user@meepleai.dev"}'
done
# Should return 429 Too Many Requests after 10 requests
```

---

## 📊 Effort Estimation

| Task | Effort |
|------|--------|
| Backend migration & endpoint | 1h |
| Frontend update | 30min |
| Testing | 30min |
| Documentation | 30min |
| **TOTAL** | **2.5h** |

---

## 🔗 Related Issues

- [Rate Limiting](https://github.com/yourusername/meepleai/issues/xxx) - Add rate limiting to demo endpoint
- [Session Management](https://github.com/yourusername/meepleai/issues/xxx) - Shorter TTL for demo sessions

---

## 📚 References

- [OWASP - Hardcoded Credentials](https://owasp.org/www-community/vulnerabilities/Use_of_hard-coded_password)
- [CWE-798: Use of Hard-coded Credentials](https://cwe.mitre.org/data/definitions/798.html)
- [Next.js Environment Variables](https://nextjs.org/docs/basic-features/environment-variables)

---

## 👥 Assignee

**Backend Developer**: TBD
**Frontend Developer**: TBD
**Security Reviewer**: Security Team
**QA**: QA Engineer

---

**Last Updated**: 2025-11-19
**Status**: 🔴 Open
**Security Level**: 🔴 Critical
