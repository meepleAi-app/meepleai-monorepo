# Security Patterns - Production-Ready Best Practices

**Source**: Security fixes from integration tests (Issues #798, #814, Completed 2025-11-07)
**Framework**: ASP.NET Core 9.0
**Status**: Production-validated patterns

---

## Path Traversal Prevention (CWE-22, OWASP A01:2021)

### Pattern: Multi-Layer Defense

**Implementation** (`Infrastructure/Security/PathSecurity.cs`):

```csharp
public static class PathSecurity
{
    public static string SanitizeFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            throw new ArgumentException("Filename cannot be empty");

        // Layer 1: Path traversal pattern detection
        if (filename.Contains("..") ||   // Standard traversal
            filename.Contains("....") ||  // Doubled traversal
            filename.Contains("//") ||    // Double slashes
            filename.Contains("\\\\"))    // Double backslashes
        {
            throw new SecurityException(
                $"Path traversal pattern detected in filename: '{filename}'");
        }

        // Layer 2: Remove path characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(filename
            .Where(c => !invalidChars.Contains(c))
            .ToArray());

        // Layer 3: Remove dangerous patterns
        sanitized = sanitized
            .Replace(":", "")
            .Replace("<", "")
            .Replace(">", "")
            .Replace("|", "")
            .Replace("*", "")
            .Replace("?", "")
            .Replace("\"", "");

        // Layer 4: Limit length
        if (sanitized.Length > 255)
            sanitized = sanitized.Substring(0, 255);

        return sanitized;
    }

    public static string GenerateSafeFilename(string originalFilename)
    {
        var extension = Path.GetExtension(originalFilename);

        if (string.IsNullOrWhiteSpace(extension))
        {
            return $"{Guid.NewGuid():N}"; // N format = no hyphens
        }

        var sanitizedExtension = SanitizeFilename(extension.TrimStart('.'));
        return $"{Guid.NewGuid():N}.{sanitizedExtension}"; // ✅ Explicit dot
    }

    public static void ValidatePath(string path, string allowedBasePath)
    {
        var fullPath = Path.GetFullPath(path);
        var basePath = Path.GetFullPath(allowedBasePath);

        if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException(
                $"Path '{path}' is outside allowed directory '{allowedBasePath}'");
        }
    }
}
```

**Key Defenses**:
1. Pattern detection (`..`, `....`, `//`, `\\`)
2. Invalid char removal
3. Dangerous pattern removal
4. Length limiting
5. GUID-based naming (prevents guessing)
6. Full path validation (basePath boundary check)

---

### Testing Path Security

**Test Pattern** (xUnit):

```csharp
public class PathSecurityTests
{
    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32")]
    [InlineData("....//secret")]
    [InlineData("file//path")]
    public void SanitizeFilename_PathTraversal_ThrowsSecurityException(string maliciousFilename)
    {
        // Act & Assert
        var act = () => PathSecurity.SanitizeFilename(maliciousFilename);
        act.Should().Throw<SecurityException>()
           .WithMessage("*Path traversal pattern detected*");
    }

    [Theory]
    [InlineData("document.pdf", @"^[a-f0-9]{32}\.pdf$")]
    [InlineData("file.txt", @"^[a-f0-9]{32}\.txt$")]
    [InlineData("image.png", @"^[a-f0-9]{32}\.png$")]
    public void GenerateSafeFilename_ValidExtension_ReturnsGuidWithExtension(
        string original, string expectedPattern)
    {
        // Act
        var result = PathSecurity.GenerateSafeFilename(original);

        // Assert
        Assert.DoesNotContain("-", result); // N format = no hyphens
        Assert.Matches(expectedPattern, result);
    }

    [Fact]
    public void ValidatePath_OutsideAllowedDirectory_ThrowsSecurityException()
    {
        // Arrange
        var basePath = "/app/uploads";
        var maliciousPath = "/app/uploads/../../../etc/passwd";

        // Act & Assert
        var act = () => PathSecurity.ValidatePath(maliciousPath, basePath);
        act.Should().Throw<SecurityException>()
           .WithMessage("*outside allowed directory*");
    }
}
```

**Coverage Target**: 100% (security code must be fully tested)

---

## PII Masking in Logs (GDPR/Privacy Compliance)

### Pattern: Automatic PII Masking

**Implementation** (`Logging/PiiMaskingEnricher.cs`):

```csharp
public class PiiMaskingEnricher : ILogEventEnricher
{
    private static readonly Regex EmailRegex = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b");
    private static readonly Regex IpRegex = new(@"\b(?:\d{1,3}\.){3}\d{1,3}\b");
    private static readonly Regex CreditCardRegex = new(@"\b\d{4}[- ]?\d{4}[- ]?\d{4}[- ]?\d{4}\b");

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        if (logEvent.Properties.ContainsKey("Email"))
        {
            var email = logEvent.Properties["Email"].ToString();
            var masked = MaskEmail(email);
            logEvent.AddOrUpdateProperty(factory.CreateProperty("Email", masked));
        }

        if (logEvent.Properties.ContainsKey("IpAddress"))
        {
            var ip = logEvent.Properties["IpAddress"].ToString();
            var masked = MaskIpAddress(ip);
            logEvent.AddOrUpdateProperty(factory.CreateProperty("IpAddress", masked));
        }

        // Mask PII in message text
        var maskedMessage = MaskPiiInText(logEvent.MessageTemplate.Text);
        // ... (apply masking)
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return email;

        var parts = email.Split('@');
        if (parts.Length != 2)
            return email;

        var local = parts[0];
        var domain = parts[1];

        // Mask: "test@example.com" → "t***t@example.com"
        var masked = local.Length <= 2
            ? new string('*', local.Length)
            : $"{local[0]}***{local[^1]}";

        return $"{masked}@{domain}";
    }

    private static string MaskIpAddress(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip) || ip == "null")
            return ip;

        var parts = ip.Split('.');
        if (parts.Length != 4)
            return ip;

        // Mask: "192.168.1.1" → "192.168.1.***"
        return $"{parts[0]}.{parts[1]}.{parts[2]}.***";
    }

    private static string MaskCreditCard(string card)
    {
        // Mask: "1234567890123456" → "****-****-****-3456"
        if (card.Length < 4)
            return "****";

        var last4 = card.Substring(card.Length - 4);
        return $"****-****-****-{last4}";
    }
}
```

**Register in Program.cs**:
```csharp
Log.Logger = new LoggerConfiguration()
    .Enrich.With<PiiMaskingEnricher>()
    .WriteTo.Console()
    .WriteTo.Seq(Configuration["SEQ_URL"])
    .CreateLogger();
```

---

### Testing PII Masking

**Test Pattern**:

```csharp
public class PiiMaskingTests
{
    [Theory]
    [InlineData("test@example.com", "t***t@example.com")]
    [InlineData("a@b.com", "a***@b.com")] // Short email
    [InlineData("verylongemail@domain.com", "v***m@domain.com")]
    public void MaskEmail_ValidEmail_MasksCorrectly(string input, string expected)
    {
        // Act
        var result = PiiMaskingEnricher.MaskEmail(input);

        // Assert
        result.Should().Be(expected);
        result.Should().NotContain(input.Split('@')[0]); // Local part masked
    }

    [Theory]
    [InlineData("192.168.1.1", "192.168.1.***")]
    [InlineData("10.0.0.1", "10.0.0.***")]
    [InlineData("null", "null")] // Handle null gracefully
    public void MaskIpAddress_ValidIp_MasksCorrectly(string input, string expected)
    {
        // Act
        var result = PiiMaskingEnricher.MaskIpAddress(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void LogEvent_ContainsPii_MaskedInOutput()
    {
        // Arrange
        var logger = new LoggerConfiguration()
            .Enrich.With<PiiMaskingEnricher>()
            .WriteTo.Sink(new TestSink())
            .CreateLogger();

        // Act
        logger.Information("User {Email} from {IpAddress}", "test@example.com", "192.168.1.1");

        // Assert
        var logOutput = testSink.LogEvents.First().RenderMessage();
        logOutput.Should().Contain("t***t@example.com");
        logOutput.Should().Contain("192.168.1.***");
        logOutput.Should().NotContain("test@example.com"); // Original masked
        logOutput.Should().NotContain("192.168.1.1"); // Original masked
    }
}
```

**Coverage Target**: 100% (privacy-critical code)

---

## IDisposable Best Practices (CODE-01)

### Pattern: Always Dispose Resources

**Common Violations** (Fixed in Issue #798):

#### 1. HttpContent Must Be Disposed

```csharp
// ❌ BAD: Memory leak
public async Task<HttpResponseMessage> PostDataAsync(object data)
{
    var content = new StringContent(JsonSerializer.Serialize(data));
    return await _httpClient.PostAsync("/api/data", content); // ❌ content not disposed
}

// ✅ GOOD: Proper disposal
public async Task<HttpResponseMessage> PostDataAsync(object data)
{
    using var content = new StringContent(JsonSerializer.Serialize(data));
    return await _httpClient.PostAsync("/api/data", content);
}
```

#### 2. IServiceScope Must Be Disposed

```csharp
// ❌ BAD: Scope leak
public async Task ProcessInBackground()
{
    var scope = _scopeFactory.CreateScope(); // ❌ Never disposed
    var service = scope.ServiceProvider.GetRequiredService<IMyService>();
    await service.ProcessAsync();
}

// ✅ GOOD: Scope disposed
public async Task ProcessInBackground()
{
    using var scope = _scopeFactory.CreateScope();
    var service = scope.ServiceProvider.GetRequiredService<IMyService>();
    await service.ProcessAsync();
} // Scope automatically disposed here
```

#### 3. HttpClient Injection (Never `new HttpClient()`)

```csharp
// ❌ BAD: Socket exhaustion
public class MyService
{
    public async Task CallApiAsync()
    {
        using var client = new HttpClient(); // ❌ Creates new connection each time
        return await client.GetStringAsync("https://api.example.com");
    }
}

// ✅ GOOD: Inject via IHttpClientFactory
public class MyService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public MyService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task CallApiAsync()
    {
        var client = _httpClientFactory.CreateClient();
        return await client.GetStringAsync("https://api.example.com");
    } // Client managed by factory (connection pooling)
}
```

**Register in Program.cs**:
```csharp
builder.Services.AddHttpClient();
```

---

### Roslyn Analyzers Enforcement

**.editorconfig** (Enforce IDisposable rules):

```ini
# CA2000: Dispose objects before losing scope
dotnet_diagnostic.CA2000.severity = error

# CA1001: Types that own disposable fields should be disposable
dotnet_diagnostic.CA1001.severity = warning

# IDE0067: Dispose objects before losing scope
dotnet_diagnostic.IDE0067.severity = error

# IDE0068: Use recommended dispose pattern
dotnet_diagnostic.IDE0068.severity = warning

# IDE0069: Disposable fields should be disposed
dotnet_diagnostic.IDE0069.severity = error
```

**Build Enforcement**: Violations block compilation (error severity)

---

## Input Validation Patterns

### Pattern 1: Email Validation

```csharp
public class Email : ValueObject
{
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled);

    public string Value { get; private set; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Email cannot be empty");

        if (!EmailRegex.IsMatch(value))
            throw new DomainException($"Invalid email format: {value}");

        if (value.Length > 255)
            throw new DomainException("Email too long (max 255 characters)");

        Value = value.ToLowerInvariant(); // Normalize
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

**Use**: Email value object in domain entities (User, etc.)

---

### Pattern 2: Password Strength Validation

```csharp
public static class PasswordValidator
{
    private const int MinLength = 8;
    private const int MaxLength = 128;

    public static void Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new DomainException("Password cannot be empty");

        if (password.Length < MinLength)
            throw new DomainException($"Password must be at least {MinLength} characters");

        if (password.Length > MaxLength)
            throw new DomainException($"Password too long (max {MaxLength} characters)");

        if (!password.Any(char.IsUpper))
            throw new DomainException("Password must contain at least one uppercase letter");

        if (!password.Any(char.IsDigit))
            throw new DomainException("Password must contain at least one digit");

        // Optional: Special character requirement
        if (!password.Any(c => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(c)))
            throw new DomainException("Password must contain at least one special character");
    }
}
```

**Use**: User.ChangePassword(), User.SetPassword()

---

### Pattern 3: SQL Injection Prevention

**Always Use Parameterized Queries**:

```csharp
// ❌ BAD: SQL injection vulnerability
public async Task<User?> GetUserByEmailAsync(string email)
{
    var sql = $"SELECT * FROM users WHERE email = '{email}'"; // ❌ Vulnerable!
    return await _context.Users.FromSqlRaw(sql).FirstOrDefaultAsync();
}

// ✅ GOOD: Parameterized query
public async Task<User?> GetUserByEmailAsync(string email)
{
    return await _context.Users
        .Where(u => u.Email == email) // ✅ EF Core parameterizes
        .FirstOrDefaultAsync();
}

// ✅ GOOD: Explicit parameters (if raw SQL needed)
public async Task<User?> GetUserByEmailAsync(string email)
{
    var sql = "SELECT * FROM users WHERE email = @email";
    return await _context.Users
        .FromSqlRaw(sql, new SqlParameter("@email", email))
        .FirstOrDefaultAsync();
}
```

**Rule**: NEVER concatenate user input into SQL strings

---

## Rate Limiting Patterns

### Pattern: Per-Role Rate Limits

**Implementation** (`Infrastructure/RateLimitService.cs`):

```csharp
public class RateLimitService
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public async Task<bool> IsAllowedAsync(string userId, string role, string operation)
    {
        var key = $"ratelimit:{userId}:{operation}";
        var limit = GetLimitForRole(role, operation);

        if (_cache.TryGetValue(key, out int currentCount))
        {
            if (currentCount >= limit)
                return false; // Rate limit exceeded

            _cache.Set(key, currentCount + 1, TimeSpan.FromMinutes(1));
        }
        else
        {
            _cache.Set(key, 1, TimeSpan.FromMinutes(1));
        }

        return true;
    }

    private int GetLimitForRole(string role, string operation)
    {
        return role switch
        {
            "Admin" => 1000, // High limit for admins
            "Editor" => 500,
            "User" => 100,
            _ => 10 // Anonymous users
        };
    }
}
```

**Usage in Endpoint**:
```csharp
app.MapPost("/api/upload", async (
    ClaimsPrincipal user,
    RateLimitService rateLimit) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    var role = user.FindFirstValue(ClaimTypes.Role);

    if (!await rateLimit.IsAllowedAsync(userId, role, "upload"))
    {
        return Results.StatusCode(429); // Too Many Requests
    }

    // Process upload...
})
.RequireAuthorization();
```

**Configuration** (`appsettings.json`):
```json
{
  "RateLimit": {
    "User": { "Upload": 10, "Chat": 100 },
    "Editor": { "Upload": 50, "Chat": 500 },
    "Admin": { "Upload": 1000, "Chat": 10000 }
  }
}
```

---

## CSRF Protection (SameSite Cookies)

### Pattern: Secure Cookie Configuration

**Implementation** (`Program.cs`):

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "MeepleAI.Session";
        options.Cookie.HttpOnly = true; // ✅ Prevent XSS
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // ✅ HTTPS only
        options.Cookie.SameSite = SameSiteMode.Lax; // ✅ CSRF protection

        // For strict CSRF (breaks some OAuth flows)
        // options.Cookie.SameSite = SameSiteMode.Strict;

        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });
```

**CORS Configuration** (if frontend separate domain):

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://app.meepleai.dev") // ✅ Explicit origin
              .AllowCredentials() // ✅ Required for cookies
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ❌ NEVER in production:
// policy.AllowAnyOrigin().AllowCredentials(); // SECURITY VIOLATION!
```

---

## XSS Prevention

### Pattern 1: Output Encoding (React Auto-Escapes)

React automatically escapes JSX content:

```tsx
// ✅ SAFE: React auto-escapes
function Component({ userInput }: Props) {
  return <div>{userInput}</div>; // Auto-escaped
}

// ⚠️ DANGEROUS: dangerouslySetInnerHTML
function Component({ html }: Props) {
  return <div dangerouslySetInnerHTML={{ __html: html }} />; // Bypasses escaping!
}
```

**Rule**: Avoid `dangerouslySetInnerHTML` unless absolutely necessary + sanitize input

---

### Pattern 2: Input Sanitization (Backend)

```csharp
public static class InputSanitizer
{
    public static string SanitizeHtml(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        // Use HtmlAgilityPack or similar
        var doc = new HtmlDocument();
        doc.LoadHtml(input);

        // Remove dangerous tags
        var dangerousTags = new[] { "script", "iframe", "object", "embed", "link" };
        foreach (var tag in dangerousTags)
        {
            var nodes = doc.DocumentNode.SelectNodes($"//{tag}");
            if (nodes != null)
            {
                foreach (var node in nodes)
                    node.Remove();
            }
        }

        // Remove dangerous attributes
        var dangerousAttrs = new[] { "onclick", "onerror", "onload" };
        foreach (var node in doc.DocumentNode.DescendantsAndSelf())
        {
            foreach (var attr in dangerousAttrs)
            {
                node.Attributes.Remove(attr);
            }
        }

        return doc.DocumentNode.OuterHtml;
    }
}
```

**Use**: Before saving rich text content (TipTap editor, markdown, etc.)

---

## Security Checklist (All Features)

### Before Deploying New Feature

- [ ] **Input Validation**: All user inputs validated (length, format, type)
- [ ] **Path Security**: File uploads use PathSecurity.GenerateSafeFilename()
- [ ] **PII Masking**: Sensitive data masked in logs (PiiMaskingEnricher)
- [ ] **IDisposable**: All resources properly disposed (CA2000 enforced)
- [ ] **SQL Injection**: Parameterized queries only (no string concatenation)
- [ ] **XSS**: No dangerouslySetInnerHTML OR sanitized
- [ ] **CSRF**: SameSite cookies configured
- [ ] **Rate Limiting**: Endpoints have appropriate rate limits
- [ ] **Authorization**: All endpoints have [Authorize] OR require valid session
- [ ] **HTTPS**: SecurePolicy = Always (production)
- [ ] **Security Tests**: 100% coverage on security code

---

## Code Quality Standards

### Enforce with Analyzers

**.editorconfig** (Security + Quality):

```ini
# Security
dotnet_diagnostic.CA2000.severity = error  # Dispose objects
dotnet_diagnostic.CA3001.severity = error  # SQL injection
dotnet_diagnostic.CA3003.severity = error  # File path injection
dotnet_diagnostic.CA3004.severity = error  # Information disclosure
dotnet_diagnostic.CA3005.severity = error  # LDAP injection
dotnet_diagnostic.CA3006.severity = error  # Process command injection
dotnet_diagnostic.CA3007.severity = error  # Open redirect
dotnet_diagnostic.CA3008.severity = error  # XPath injection
dotnet_diagnostic.CA3009.severity = error  # XML injection
dotnet_diagnostic.CA3010.severity = error  # XAML injection
dotnet_diagnostic.CA3011.severity = error  # DLL injection
dotnet_diagnostic.CA3012.severity = error  # Regex injection

# Code Quality
dotnet_diagnostic.CA1001.severity = warning  # Types with disposable fields
dotnet_diagnostic.CA1806.severity = error   # Do not ignore method results
dotnet_diagnostic.CA1816.severity = warning  # Dispose methods
dotnet_diagnostic.CA2007.severity = none    # ConfigureAwait (not needed in ASP.NET Core)
dotnet_diagnostic.CA2213.severity = warning  # Disposable fields

# IDE
dotnet_diagnostic.IDE0067.severity = error  # Dispose objects
dotnet_diagnostic.IDE0068.severity = warning # Dispose pattern
dotnet_diagnostic.IDE0069.severity = error  # Dispose fields
```

**CI Enforcement**: Build fails if errors present

---

**Knowledge extracted from**:
- COMPLETE-INTEGRATION-TEST-FIXES-2025-11-07.md (479 lines)
- code-quality-fixes-2025-11-09.md
- Issue #798 (CODE-02), #814, #801

**Status**: Production-ready security and quality patterns
