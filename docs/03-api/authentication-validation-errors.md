# Authentication API Validation Errors

**Issue #1449**: FluentValidation for Authentication CQRS Pipeline

This document describes the validation errors returned by Authentication endpoints when request validation fails.

## HTTP Status Codes

- **422 Unprocessable Entity**: Request validation failed (FluentValidation errors)
- **400 Bad Request**: Domain validation failed (business rule violations)

## Error Response Format

When validation fails, the API returns a structured JSON response:

```json
{
  "error": "validation_error",
  "message": "One or more validation errors occurred",
  "errors": {
    "PropertyName": [
      "Error message 1",
      "Error message 2"
    ]
  },
  "correlationId": "trace-id-for-debugging",
  "timestamp": "2025-11-21T12:00:00Z"
}
```

## Login Endpoint (`POST /api/v1/auth/login`)

### Email Validation Errors

| Error Message | Cause | Example |
|--------------|-------|---------|
| `Email is required` | Email field is empty, null, or whitespace | `""`, `null`, `"   "` |
| `Email must be a valid email address` | Email format is invalid | `"notanemail"`, `"@example.com"`, `"test@"` |
| `Email must not exceed 255 characters` | Email is too long | 256+ character email |

### Password Validation Errors

| Error Message | Cause | Example |
|--------------|-------|---------|
| `Password is required` | Password field is empty, null, or whitespace | `""`, `null`, `"   "` |
| `Password must be at least 8 characters` | Password is too short | `"short"`, `"1234567"` |

### Example Error Response

```json
{
  "error": "validation_error",
  "message": "One or more validation errors occurred",
  "errors": {
    "Email": ["Email must be a valid email address"],
    "Password": ["Password must be at least 8 characters"]
  },
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": "2025-11-21T12:00:00Z"
}
```

## Register Endpoint (`POST /api/v1/auth/register`)

### Email Validation Errors

Same as Login endpoint.

### Password Validation Errors

| Error Message | Cause | Example |
|--------------|-------|---------|
| `Password is required` | Password is empty | `""`, `null` |
| `Password must be at least 8 characters` | Too short | `"short"` |
| `Password must not exceed 128 characters` | Too long | 129+ characters |
| `Password must contain at least one uppercase letter` | No uppercase | `"password123!"` |
| `Password must contain at least one lowercase letter` | No lowercase | `"PASSWORD123!"` |
| `Password must contain at least one digit` | No digit | `"PasswordTest!"` |
| `Password must contain at least one special character` | No special char | `"PasswordTest123"` |

### Display Name Validation Errors

| Error Message | Cause | Example |
|--------------|-------|---------|
| `Display name is required` | Display name is empty | `""`, `null` |
| `Display name must be at least 2 characters` | Too short | `"A"` |
| `Display name must not exceed 100 characters` | Too long | 101+ characters |
| `Display name can only contain letters, numbers, spaces, hyphens, underscores, and periods` | Invalid characters | `"Invalid@Name"`, `"Test#User"` |

**Valid characters**: `a-z`, `A-Z`, `0-9`, space, `-`, `_`, `.`

### Role Validation Errors

| Error Message | Cause | Example |
|--------------|-------|---------|
| `Role must be one of: user, editor, admin` | Invalid role | `"superadmin"`, `"moderator"` |

**Valid roles**: `user`, `editor`, `admin` (case-insensitive)

### Example Error Response

```json
{
  "error": "validation_error",
  "message": "One or more validation errors occurred",
  "errors": {
    "Email": ["Email must be a valid email address"],
    "Password": [
      "Password must be at least 8 characters",
      "Password must contain at least one uppercase letter",
      "Password must contain at least one digit",
      "Password must contain at least one special character"
    ],
    "DisplayName": ["Display name must be at least 2 characters"]
  },
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": "2025-11-21T12:00:00Z"
}
```

## Change Password Endpoint (`PUT /api/v1/auth/profile/password`)

### User ID Validation Errors

| Error Message | Cause | Example |
|--------------|-------|---------|
| `User ID is required` | User ID is empty GUID | `00000000-0000-0000-0000-000000000000` |

### Current Password Validation Errors

| Error Message | Cause | Example |
|--------------|-------|---------|
| `Current password is required` | Current password is empty | `""`, `null` |
| `Current password must be at least 8 characters` | Too short | `"short"` |

### New Password Validation Errors

Same as Register endpoint password validation, plus:

| Error Message | Cause | Example |
|--------------|-------|---------|
| `New password must be different from current password` | Same as current | Current: `"Pass123!"`, New: `"Pass123!"` |

### Example Error Response

```json
{
  "error": "validation_error",
  "message": "One or more validation errors occurred",
  "errors": {
    "UserId": ["User ID is required"],
    "NewPassword": [
      "New password must contain at least one uppercase letter",
      "New password must be different from current password"
    ]
  },
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": "2025-11-21T12:00:00Z"
}
```

## Enable 2FA Endpoint (`POST /api/v1/auth/2fa/enable`)

### User ID Validation Errors

Same as Change Password endpoint.

### TOTP Code Validation Errors

| Error Message | Cause | Example |
|--------------|-------|---------|
| `TOTP code is required` | Code is empty | `""`, `null` |
| `TOTP code must be exactly 6 digits` | Wrong length | `"12345"`, `"1234567"` |
| `TOTP code must contain only digits` | Non-numeric | `"12345a"`, `"123 56"` |

**Valid format**: 6-digit numeric code (e.g., `123456`, `000000`, `999999`)

### Example Error Response

```json
{
  "error": "validation_error",
  "message": "One or more validation errors occurred",
  "errors": {
    "TotpCode": [
      "TOTP code must be exactly 6 digits",
      "TOTP code must contain only digits"
    ]
  },
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": "2025-11-21T12:00:00Z"
}
```

## Reset Password Endpoint (`POST /api/v1/auth/password/reset`)

### Token Validation Errors

| Error Message | Cause | Example |
|--------------|-------|---------|
| `Reset token is required` | Token is empty | `""`, `null` |
| `Reset token must be a valid GUID` | Invalid GUID format | `"not-a-guid"`, `"12345"` |

**Valid format**: Standard GUID with or without hyphens
- With hyphens: `12345678-1234-1234-1234-123456789abc`
- Without hyphens: `12345678123412341234123456789abc`

### New Password Validation Errors

Same as Register endpoint password validation.

### Example Error Response

```json
{
  "error": "validation_error",
  "message": "One or more validation errors occurred",
  "errors": {
    "Token": ["Reset token must be a valid GUID"],
    "NewPassword": [
      "New password must be at least 8 characters",
      "New password must contain at least one uppercase letter"
    ]
  },
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": "2025-11-21T12:00:00Z"
}
```

## Client-Side Validation

Frontend applications should implement client-side validation matching these rules to provide immediate feedback before submitting requests:

### Email Validation
- Check for non-empty value
- Validate email format using standard email regex
- Enforce 255 character limit

### Password Validation (Registration/Reset)
- Minimum 8 characters
- Maximum 128 characters
- At least one uppercase letter (`[A-Z]`)
- At least one lowercase letter (`[a-z]`)
- At least one digit (`[0-9]`)
- At least one special character (non-alphanumeric)

### Display Name Validation
- Minimum 2 characters
- Maximum 100 characters
- Only allowed characters: `a-z`, `A-Z`, `0-9`, space, `-`, `_`, `.`

### TOTP Code Validation
- Exactly 6 digits
- Only numeric characters

### Password Change Validation
- Current password must be different from new password

## Testing with curl

### Valid Request

```bash
curl -X POST http://localhost:5080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "ValidPassword123!"
  }'
```

### Invalid Request (Validation Errors)

```bash
curl -X POST http://localhost:5080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "invalidemail",
    "password": "short"
  }'
```

**Response** (HTTP 422):
```json
{
  "error": "validation_error",
  "message": "One or more validation errors occurred",
  "errors": {
    "Email": ["Email must be a valid email address"],
    "Password": ["Password must be at least 8 characters"]
  },
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": "2025-11-21T12:00:00Z"
}
```

## Implementation Details

- **Validation Framework**: FluentValidation 11.11.0
- **Pipeline**: MediatR ValidationBehavior intercepts requests before handler execution
- **Status Code**: HTTP 422 Unprocessable Entity for validation errors
- **Error Format**: Structured JSON with grouped errors by property name
- **Correlation ID**: Included in all error responses for debugging

## Related Documentation

- [API Specification](board-game-ai-api-specification.md)
- [Authentication Endpoints](../01-architecture/overview/system-architecture.md#authentication)
- [OAuth Security](../06-security/oauth-security.md)
- [Error Handling](../02-development/error-handling.md)

---

**Version**: 1.0
**Last Updated**: 2025-11-21
**Issue**: #1449
**Owner**: Engineering Lead
