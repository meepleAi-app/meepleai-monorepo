# ADR-012: FluentValidation Integration with CQRS Pipeline

**Status**: Proposed
**Date**: 2025-01-19
**Deciders**: Engineering Lead, Backend Team
**Context**: Code Review - Backend-Frontend Interactions Input Validation

---

## Context

During the code review of backend-frontend interactions (2025-01-19), a gap in input validation was identified:

**Current State**:
- Some endpoints have manual validation (e.g., `if (string.IsNullOrWhiteSpace(payload.Email))`)
- No consistent validation framework
- Validation logic scattered across endpoints
- Poor error messages for users
- No centralized validation testing

**Problems**:
1. **Inconsistent Validation**: Some commands validated, others not
2. **Code Duplication**: Same validation rules repeated
3. **Poor UX**: Generic error messages ("Bad Request")
4. **Security Risk**: Invalid data can reach domain layer
5. **Maintainability**: Hard to update validation rules

**Example** (`AuthEndpoints.cs:96-100`):
```csharp
if (string.IsNullOrWhiteSpace(payload.Email) || string.IsNullOrWhiteSpace(payload.Password))
{
    logger.LogWarning("Login failed: email or password is empty");
    return Results.BadRequest(new { error = "Email and password are required" });
}
```

**Industry Best Practices**:
- ASP.NET Core: FluentValidation for complex validation
- CQRS: Validate commands before handler execution
- MediatR: Pipeline behaviors for cross-cutting concerns

---

## Decision

Implement **FluentValidation with MediatR Pipeline Behavior** for all CQRS Commands and Queries.

### Architecture

```
Request Flow with Validation:
┌──────────────────────────────────────────────────────────┐
│  HTTP Request (JSON)                                     │
└──────────────┬───────────────────────────────────────────┘
               │
               ▼
┌──────────────────────────────────────────────────────────┐
│  Model Binding (ASP.NET Core)                            │
│  Deserialize to Command/Query                            │
└──────────────┬───────────────────────────────────────────┘
               │
               ▼
┌──────────────────────────────────────────────────────────┐
│  IMediator.Send(command)                                 │
└──────────────┬───────────────────────────────────────────┘
               │
               ▼
┌──────────────────────────────────────────────────────────┐
│  ValidationBehavior<TRequest, TResponse>  ← NEW          │
│  ├─ Find IValidator<TRequest>                            │
│  ├─ Validate command                                     │
│  ├─ If valid → continue                                  │
│  └─ If invalid → throw ValidationException              │
└──────────────┬───────────────────────────────────────────┘
               │ (validation passed)
               ▼
┌──────────────────────────────────────────────────────────┐
│  Command/Query Handler                                   │
│  Domain logic executes with VALID data                   │
└──────────────────────────────────────────────────────────┘
```

### Components

#### 1. FluentValidation Validators

**Example: LoginCommandValidator**
```csharp
public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Invalid email format")
            .MaximumLength(256)
            .WithMessage("Email must not exceed 256 characters");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters")
            .MaximumLength(128)
            .WithMessage("Password must not exceed 128 characters");
    }
}
```

#### 2. ValidationBehavior Pipeline

```csharp
public class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    {
        _validators = validators;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken))
        );

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Any())
        {
            _logger.LogWarning(
                "Validation failed for {RequestType}: {Errors}",
                typeof(TRequest).Name,
                string.Join("; ", failures.Select(f => f.ErrorMessage))
            );

            throw new ValidationException(failures);
        }

        return await next();
    }
}
```

#### 3. Global Exception Handler

```csharp
// ApiExceptionHandlerMiddleware.cs
catch (FluentValidation.ValidationException ex)
{
    context.Response.StatusCode = 422; // Unprocessable Entity

    var errors = ex.Errors
        .GroupBy(e => e.PropertyName)
        .ToDictionary(
            g => g.Key,
            g => g.Select(e => e.ErrorMessage).ToArray()
        );

    await context.Response.WriteAsJsonAsync(new
    {
        type = "ValidationError",
        title = "One or more validation errors occurred",
        status = 422,
        errors = errors,
        traceId = context.TraceIdentifier
    });
}
```

---

## Implementation Scope

### Priority 1: Authentication Context (Sprint 2)

1. **LoginCommandValidator**
   - Email: NotEmpty, EmailAddress, MaxLength(256)
   - Password: NotEmpty, MinLength(8), MaxLength(128)

2. **RegisterCommandValidator**
   - Email: NotEmpty, EmailAddress, MaxLength(256), Must be unique
   - Password: NotEmpty, MinLength(8), MaxLength(128), Regex(complexity)
   - DisplayName: MaxLength(100)
   - Role: Must be valid enum

3. **ChangePasswordCommandValidator**
   - CurrentPassword: NotEmpty, MinLength(8), MaxLength(128)
   - NewPassword: NotEmpty, MinLength(8), MaxLength(128)
   - NewPassword: Must be different from CurrentPassword
   - Password complexity rules

4. **Enable2FACommandValidator**
   - Code: NotEmpty, Length(6), Regex(^\d{6}$)

5. **ResetPasswordCommandValidator**
   - Token: NotEmpty, Must be valid GUID
   - NewPassword: NotEmpty, MinLength(8), MaxLength(128), Regex(complexity)
   - Email: NotEmpty, EmailAddress

### Priority 2: GameManagement Context (Sprint 2)

6. **CreateGameCommandValidator**
   - Title: NotEmpty, MaxLength(200)
   - Publisher: MaxLength(100)
   - YearPublished: GreaterThan(1900), LessThan(current year + 1)
   - PlayerCount: Between(1, 100)

7. **UpdateGameCommandValidator**
   - Similar to CreateGameCommandValidator

8. **CreateSessionCommandValidator**
   - GameId: NotEmpty, Must be valid GUID
   - PlayerNames: NotEmpty, MinLength(1), MaxLength(20 per name)

### Priority 3: KnowledgeBase Context (Sprint 3)

9. **AskQuestionCommandValidator** (StreamQaQuery)
   - GameId: NotEmpty, Must be valid GUID
   - Query: NotEmpty, MinLength(3), MaxLength(500)
   - SearchMode: Must be valid enum

10. **CreateChatThreadCommandValidator**
    - GameId: NotEmpty, Must be valid GUID
    - Title: MaxLength(200)

---

## Consequences

### Positive

✅ **Security**:
- Prevents invalid data from reaching domain layer
- Consistent validation across all endpoints
- Reduced attack surface for injection attacks

✅ **User Experience**:
- Clear, specific error messages
- Field-level error reporting (not just "Bad Request")
- Frontend can display errors next to form fields

✅ **Maintainability**:
- Single source of truth for validation rules
- Easy to test (unit test validators independently)
- Self-documenting (validators show business rules)

✅ **Developer Experience**:
- Consistent pattern across codebase
- Reusable validators (composition)
- Less boilerplate in handlers

✅ **Testability**:
```csharp
[Fact]
public void LoginCommandValidator_ShouldFailForEmptyEmail()
{
    // Arrange
    var validator = new LoginCommandValidator();
    var command = new LoginCommand(Email: "", Password: "validpassword");

    // Act
    var result = validator.Validate(command);

    // Assert
    Assert.False(result.IsValid);
    Assert.Contains(result.Errors, e => e.PropertyName == "Email");
}
```

### Negative

⚠️ **Performance Overhead**:
- ~0.5-2ms validation time per request
- **Mitigation**: Negligible compared to I/O operations

⚠️ **Learning Curve**:
- Team needs to learn FluentValidation syntax
- **Mitigation**: Good documentation, examples, code reviews

⚠️ **Breaking Changes**:
- Error response format changes (now returns field-level errors)
- **Mitigation**: Frontend already handles 422 errors

### Risks

🟡 **Complex Validation Rules**:
- **Risk**: Async validators (database lookups) slow down requests
- **Mitigation**: Cache common validation results, limit async validators

🟢 **Over-Validation**:
- **Risk**: Too strict rules frustrate users
- **Mitigation**: Business-driven rules, user testing

---

## Error Response Format

### Before (Inconsistent)
```json
{
  "error": "Email and password are required"
}
```

### After (RFC 7807 Problem Details)
```json
{
  "type": "ValidationError",
  "title": "One or more validation errors occurred",
  "status": 422,
  "errors": {
    "Email": [
      "Email is required"
    ],
    "Password": [
      "Password must be at least 8 characters"
    ]
  },
  "traceId": "0HMVD9QJKV8J9:00000001"
}
```

---

## Alternatives Considered

### Alternative 1: Data Annotations
**Description**: Use `[Required]`, `[EmailAddress]`, `[StringLength]` attributes

**Pros**:
- Built into ASP.NET Core
- Simpler for basic validation
- No external dependencies

**Cons**:
- Limited expressiveness (hard to write complex rules)
- Harder to test
- Attributes pollute domain models

**Decision**: Rejected - FluentValidation more powerful

### Alternative 2: Manual Validation in Handlers
**Description**: Keep validation logic in each handler

**Pros**:
- No framework dependency
- Full control

**Cons**:
- Code duplication
- Inconsistent error messages
- Hard to maintain

**Decision**: Rejected - Violates DRY principle

### Alternative 3: Domain Model Validation
**Description**: Validate in domain entity constructors

**Pros**:
- Domain-driven design alignment
- Validation close to data

**Cons**:
- Can't return user-friendly errors
- Mixes validation with domain logic
- Harder to test

**Decision**: Partial adoption - Domain validates invariants, CQRS validates input

---

## Testing Strategy

### Unit Tests (Validators)
```csharp
public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Theory]
    [InlineData("", "Password must be at least 8 characters")]
    [InlineData("short", "Password must be at least 8 characters")]
    [InlineData("a".PadRight(129, 'a'), "Password must not exceed 128 characters")]
    public void Validate_Password_ShouldFailForInvalidPassword(string password, string expectedError)
    {
        // Arrange
        var command = new LoginCommand(Email: "test@example.com", Password: password);

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == expectedError);
    }
}
```

### Integration Tests (Pipeline)
```csharp
[Fact]
public async Task LoginEndpoint_ShouldReturn422ForInvalidEmail()
{
    // Arrange
    var request = new { email = "not-an-email", password = "validpassword123" };

    // Act
    var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

    // Assert
    Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
    Assert.Contains("Email", problem.Errors.Keys);
}
```

---

## Migration Plan

### Phase 1: Infrastructure (Week 1)
1. ✅ Install FluentValidation.AspNetCore
2. ✅ Create ValidationBehavior
3. ✅ Register in DI container
4. ✅ Update ApiExceptionHandlerMiddleware
5. ✅ Test pipeline integration

### Phase 2: Authentication (Week 2)
6. ✅ Create 5 validators (Login, Register, ChangePassword, Enable2FA, ResetPassword)
7. ✅ Unit tests for each validator
8. ✅ Integration tests for endpoints
9. ✅ Remove manual validation from endpoints
10. ✅ Update API documentation

### Phase 3: GameManagement (Week 3)
11. ✅ Create 3 validators (CreateGame, UpdateGame, CreateSession)
12. ✅ Tests
13. ✅ Remove manual validation

### Phase 4: KnowledgeBase (Week 4)
14. ✅ Create 4 validators (AskQuestion, CreateThread, AddMessage, CreateComment)
15. ✅ Tests
16. ✅ Remove manual validation

---

## Dependencies

**NuGet Packages**:
```xml
<PackageReference Include="FluentValidation.AspNetCore" Version="11.3.0" />
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.3.0" />
```

**Registration**:
```csharp
// Program.cs
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});
```

---

## Related Decisions

- **ADR-008**: Streaming CQRS Migration (pipeline behaviors)
- **ADR-009**: Centralized Error Handling (exception middleware)

---

## References

- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
- [MediatR Pipeline Behaviors](https://github.com/jbogard/MediatR/wiki/Behaviors)
- [RFC 7807 Problem Details](https://www.rfc-editor.org/rfc/rfc7807)
- [ASP.NET Core Validation](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation)

---

**Decision Maker**: Engineering Lead
**Approval**: Pending Backend Team Review
**Implementation**: Issue #TBD (Sprint 2)
