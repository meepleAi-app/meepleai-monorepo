# Authentication - Flussi API

## Panoramica

Il bounded context Authentication gestisce registrazione, login, sessioni, 2FA, OAuth, API keys, password reset e verifica email.

---

## 1. Registrazione e Login

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/auth/register` | `RegisterCommand` | `{ email, password, displayName?, role? }` | `[P]` |
| POST | `/auth/login` | `LoginCommand` | `{ email, password }` | `[P]` |
| POST | `/auth/logout` | `LogoutCommand` | — (cookie) | `[P]` |
| GET | `/auth/me` | — (reads context) | — | `[P]` |

### Flusso Registrazione → Login

```
┌─────────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐
│  Register    │────▶│  Login   │────▶│  /auth/me│────▶│  Logout  │
│  POST        │     │  POST    │     │  GET     │     │  POST    │
└─────────────┘     └──────────┘     └──────────┘     └──────────┘
       │                  │
       ▼                  ▼
  Email Verif.      Set Cookie
  (async)           + Session
```

---

## 2. Gestione Sessioni

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| GET | `/auth/session/status` | — | — | `[S]` |
| POST | `/auth/session/extend` | `ExtendSessionCommand` | — | `[S]` |
| GET | `/users/me/sessions` | `GetUserSessionsQuery` | — | `[S]` |
| GET | `/auth/sessions/{sessionId}/status` | `GetSessionStatusQuery` | — | `[S]` |
| POST | `/auth/sessions/{sessionId}/extend` | `ExtendSessionCommand` | — | `[S]` |
| POST | `/auth/sessions/{sessionId}/revoke` | `RevokeSessionCommand` | — | `[S]` |
| POST | `/auth/sessions/revoke-all` | `LogoutAllDevicesCommand` | `{ includeCurrentSession?, password? }` | `[S]` |

### Admin Session Management

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/admin/sessions` | `GetAllSessionsQuery` | `limit=100, userId?` | `[A]` |
| DELETE | `/admin/sessions/{sessionId}` | `RevokeSessionCommand` | — | `[A]` |
| DELETE | `/admin/users/{userId}/sessions` | `RevokeAllUserSessionsCommand` | — | `[A]` |

---

## 3. OAuth

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/auth/oauth/{provider}/login` | `InitiateOAuthLoginCommand` | provider: google/discord/github | `[P]` |
| GET | `/auth/oauth/{provider}/callback` | `HandleOAuthCallbackCommand` | `code, state` | `[P]` |
| DELETE | `/auth/oauth/{provider}/unlink` | `UnlinkOAuthAccountCommand` | — | `[S]` |
| GET | `/users/me/oauth-accounts` | `GetLinkedOAuthAccountsQuery` | — | `[S]` |

### Flusso OAuth Login

```
┌──────────┐     ┌───────────────┐     ┌──────────────┐     ┌──────────┐
│  Client   │────▶│ /oauth/{p}/   │────▶│  Provider    │────▶│ /callback│
│  Browser  │     │   login       │     │  (Google/    │     │  GET     │
└──────────┘     └───────────────┘     │  Discord/    │     └────┬─────┘
                       │                │  GitHub)     │          │
                       ▼                └──────────────┘          ▼
                  Redirect to                              Create/Link
                  Provider                                 Account +
                  (state param)                            Set Cookie
```

**Rate Limiting**: 10 requests/min per IP

---

## 4. Two-Factor Authentication (2FA)

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/auth/2fa/setup` | `GenerateTotpSetupCommand` | — | `[S]` |
| POST | `/auth/2fa/enable` | `Enable2FACommand` | `{ code }` | `[S]` |
| POST | `/auth/2fa/verify` | `Verify2FACommand` | `{ sessionToken, code }` | `[P]` |
| POST | `/auth/2fa/disable` | `Disable2FACommand` | `{ password, code }` | `[S]` |
| GET | `/users/me/2fa/status` | `Get2FAStatusQuery` | — | `[S]` |
| POST | `/auth/admin/2fa/disable` | `AdminDisable2FACommand` | `{ targetUserId }` | `[A]` |

### Flusso Setup 2FA

```
┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐
│  Setup    │────▶│  QR Code │────▶│  Scan +  │────▶│  Enable  │
│  POST     │     │  Return  │     │  Enter   │     │  POST    │
└──────────┘     └──────────┘     │  Code    │     └──────────┘
                                   └──────────┘
```

### Flusso Login con 2FA

```
POST /auth/login → 200 { requires2FA: true, sessionToken }
                 → POST /auth/2fa/verify { sessionToken, code }
                 → 200 { token, ... } (session completa)
```

**Rate Limiting**: 3 attempts/min per session token

---

## 5. API Keys

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| POST | `/auth/apikey/login` | `LoginWithApiKeyCommand` | `{ apiKey }` | `[P]` |
| POST | `/auth/apikey/logout` | `LogoutApiKeyCommand` | — | `[P]` |
| POST | `/api-keys` | `CreateApiKeyManagementCommand` | `{ keyName, ... }` | `[S]` |
| GET | `/api-keys` | `ListApiKeysQuery` | `includeRevoked?, page?, pageSize?` | `[S]` |
| GET | `/api-keys/{keyId}` | `GetApiKeyQuery` | — | `[S]` |
| PUT | `/api-keys/{keyId}` | `UpdateApiKeyManagementCommand` | `{ keyName?, ... }` | `[S]` |
| DELETE | `/api-keys/{keyId}` | `RevokeApiKeyManagementCommand` | — | `[S]` |
| POST | `/api-keys/{keyId}/rotate` | `RotateApiKeyCommand` | — | `[S]` |
| GET | `/api-keys/{keyId}/usage` | `GetApiKeyUsageQuery` | — | `[S]` |
| GET | `/api-keys/{keyId}/stats` | `GetApiKeyUsageStatsQuery` | — | `[S]` |
| GET | `/api-keys/{keyId}/logs` | `GetApiKeyUsageLogsQuery` | `skip?, take?` | `[S]` |

### Admin API Keys

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| DELETE | `/admin/api-keys/{keyId}` | `DeleteApiKeyCommand` | — | `[A]` |
| GET | `/admin/api-keys/stats` | `GetAllApiKeysWithStatsQuery` | `userId?, includeRevoked?` | `[A]` |
| GET | `/admin/api-keys/bulk/export` | `BulkExportApiKeysQuery` | `userId?, isActive?, searchTerm?` | `[A]` |
| POST | `/admin/api-keys/bulk/import` | `BulkImportApiKeysCommand` | CSV body | `[A]` |

---

## 6. Password Reset

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| POST | `/auth/password-reset/request` | `RequestPasswordResetCommand` | `{ email }` | `[P]` |
| GET | `/auth/password-reset/verify` | `ValidatePasswordResetTokenQuery` | `token` (query) | `[P]` |
| PUT | `/auth/password-reset/confirm` | `ResetPasswordCommand` | `{ token, newPassword }` | `[P]` |

### Flusso Password Reset

```
POST /password-reset/request { email }
       │
       ▼
  Email con token → GET /password-reset/verify?token=xxx
                          │
                          ▼
                    PUT /password-reset/confirm { token, newPassword }
```

---

## 7. Email Verification

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/auth/email/verify` | `VerifyEmailCommand` | `{ token }` | `[P]` |
| POST | `/auth/email/resend` | `ResendVerificationCommand` | `{ email }` | `[P]` |

**Rate Limiting**: 1 resend/min

---

## 8. Profilo Utente

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/users/profile` | `GetUserProfileQuery` | — | `[S]` |
| PUT | `/users/profile` | `UpdateUserProfileCommand` | `{ displayName?, email? }` | `[S]` |
| PUT | `/users/profile/password` | `ChangePasswordCommand` | `{ currentPassword, newPassword }` | `[S]` |
| GET | `/users/me/upload-quota` | `GetUserUploadQuotaQuery` | — | `[S]` |
| PUT | `/users/preferences` | `UpdatePreferencesCommand` | `{ language, theme, emailNotifications, dataRetentionDays }` | `[S]` |
| GET | `/users/preferences` | `GetUserProfileQuery` | — | `[S]` |
| GET | `/users/me/activity` | `GetUserActivityQuery` | `actionFilter?, resourceFilter?, startDate?, endDate?, limit?` | `[S]` |
| GET | `/users/me/ai-usage` | `GetUserDetailedAiUsageQuery` | `days?` | `[S]` |
| GET | `/users/me/features` | `GetUserAvailableFeaturesQuery` | — | `[S]` |

---

## 9. Device Management

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/users/me/devices` | `GetUserDevicesQuery` | — | `[S]` |
| DELETE | `/users/me/devices/{deviceId}` | `RevokeSessionCommand` | — | `[S]` |

---

## Metodi di Autenticazione

| Metodo | Header/Cookie | Descrizione |
|--------|--------------|-------------|
| Session Cookie | Cookie automatico | Login standard via browser |
| API Key | `Authorization: ApiKey <value>` | Accesso programmatico |
| Admin Session | `RequireAdminSession()` | Sessione con ruolo Admin/Editor |

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 1,325 |
| **Passati** | 1,320 |
| **Falliti** | 0 |
| **Ignorati** | 5 |
| **Pass Rate** | 100% |
| **Durata** | 42s |

### Fix Applicati (2026-02-15)

| Test | Fix |
|------|-----|
| `Parse_InvalidTier_ThrowsValidationException("enterprise")` | Rimosso "enterprise" da tier invalidi (valido dal Epic #4068) |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| Login/Register | `LoginCommandHandlerTests.cs`, `RegisterCommandHandlerTests.cs` | Passato |
| Session Management | `CreateSessionCommandHandlerTests.cs`, `ExtendSessionTests.cs` | Passato |
| OAuth | `InitiateOAuthLoginTests.cs`, `HandleOAuthCallbackTests.cs` | Passato |
| 2FA | `GenerateTotpSetupTests.cs`, `Enable2FATests.cs`, `Verify2FATests.cs` | Passato |
| API Keys | `CreateApiKeyTests.cs`, `RotateApiKeyTests.cs` | Passato |
| Password Reset | `RequestPasswordResetTests.cs`, `ResetPasswordTests.cs` | Passato |
| Email Verification | `VerifyEmailTests.cs`, `ResendVerificationTests.cs` | Passato |
| User Profile | `UpdateUserProfileTests.cs`, `ChangePasswordTests.cs` | Passato |
| Device Management | `GetUserDevicesTests.cs` | Passato |
| Validators | 24 file di validazione | Passato |
| Domain Entities | `User.cs`, `Session.cs`, `ApiKey.cs`, `OAuthAccount.cs` | Passato |
| Event Handlers | 13 handler eventi | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*
