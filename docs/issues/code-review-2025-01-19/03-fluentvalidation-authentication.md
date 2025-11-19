# ✅ [Validation] FluentValidation for Authentication Context

**Priority**: 🟡 HIGH
**Complexity**: Medium
**Estimated Time**: 5-6 hours
**Dependencies**: None

## 🎯 Objective

Implement FluentValidation validators for all Authentication bounded context Commands/Queries to prevent invalid data from entering the system.

## 📋 Context

**Source**: Code Review Backend-Frontend Interactions
**Issue**: Missing input validation on CQRS commands/queries
**Impact**: High - Prevents invalid data, improves error messages, reduces attack surface

## ✅ Validators to Implement

### 1. LoginCommandValidator
```csharp
- Email: NotEmpty, EmailAddress, MaxLength(256)
- Password: NotEmpty, MinLength(8), MaxLength(128)
```

### 2. RegisterCommandValidator
```csharp
- Email: NotEmpty, EmailAddress, MaxLength(256)
- Password: NotEmpty, MinLength(8), MaxLength(128), Regex(password complexity)
- DisplayName: MaxLength(100)
- Role: Must be valid enum value
```

### 3. ChangePasswordCommandValidator
```csharp
- CurrentPassword: NotEmpty, MinLength(8), MaxLength(128)
- NewPassword: NotEmpty, MinLength(8), MaxLength(128), MustBeDifferentFrom(CurrentPassword)
- NewPassword: Regex(password complexity rules)
```

### 4. Enable2FACommandValidator
```csharp
- Code: NotEmpty, Length(6), Regex(^\d{6}$)
```

### 5. ResetPasswordCommandValidator
```csharp
- Token: NotEmpty, Guid
- NewPassword: NotEmpty, MinLength(8), MaxLength(128), Regex(password complexity)
- Email: NotEmpty, EmailAddress
```

## ✅ Task Checklist

### Implementation
- [ ] Create validators directory: `apps/api/src/Api/BoundedContexts/Authentication/Application/Validators/`
- [ ] Implement `LoginCommandValidator`
- [ ] Implement `RegisterCommandValidator`
- [ ] Implement `ChangePasswordCommandValidator`
- [ ] Implement `Enable2FACommandValidator`
- [ ] Implement `ResetPasswordCommandValidator`
- [ ] Create `ValidationBehavior<TRequest, TResponse>` MediatR pipeline behavior
- [ ] Register validators in DI container
- [ ] Register `ValidationBehavior` in MediatR pipeline

### Testing
- [ ] Unit tests for each validator (valid/invalid cases)
- [ ] Test ValidationBehavior integration with MediatR
- [ ] Test error response format (422 with validation errors)
- [ ] Test multiple validation errors returned simultaneously
- [ ] Integration tests for endpoints with invalid data

### Documentation
- [ ] Document validation rules in API docs
- [ ] Add examples of validation error responses
- [ ] Update `docs/02-development/validation.md`
- [ ] Document custom validators (if any)

### Configuration
- [ ] Add validation configuration in `appsettings.json` (e.g., password complexity)
- [ ] Make validation rules configurable where appropriate

## 📁 Files to Create

```
apps/api/src/Api/BoundedContexts/Authentication/Application/Validators/
├── LoginCommandValidator.cs (NEW)
├── RegisterCommandValidator.cs (NEW)
├── ChangePasswordCommandValidator.cs (NEW)
├── Enable2FACommandValidator.cs (NEW)
└── ResetPasswordCommandValidator.cs (NEW)

apps/api/src/Api/Infrastructure/Behaviors/
└── ValidationBehavior.cs (NEW)

apps/api/tests/Api.Tests/BoundedContexts/Authentication/Validators/
├── LoginCommandValidatorTests.cs (NEW)
├── RegisterCommandValidatorTests.cs (NEW)
├── ChangePasswordCommandValidatorTests.cs (NEW)
├── Enable2FACommandValidatorTests.cs (NEW)
└── ResetPasswordCommandValidatorTests.cs (NEW)

apps/api/tests/Api.Tests/Infrastructure/Behaviors/
└── ValidationBehaviorTests.cs (NEW)
```

## 🔗 References

- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
- [MediatR Pipeline Behaviors](https://github.com/jbogard/MediatR/wiki/Behaviors)
- [ASP.NET Core Model Validation](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation)

## 📊 Acceptance Criteria

- ✅ All 5 validators implemented and registered
- ✅ ValidationBehavior catches validation errors before handler execution
- ✅ HTTP 422 returned with structured validation errors
- ✅ Test coverage >= 95% for validators
- ✅ API docs updated with validation rules
- ✅ No breaking changes to existing endpoints

## 🔄 Follow-up Tasks

After Authentication context validation is complete:
- [ ] Issue #TBD: FluentValidation for GameManagement context
- [ ] Issue #TBD: FluentValidation for KnowledgeBase context

## 🏷️ Labels

`priority: high`, `type: enhancement`, `area: backend`, `effort: medium`, `sprint: 2`
