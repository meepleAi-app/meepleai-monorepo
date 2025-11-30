# Authentication Bounded Context

## Responsabilità

Gestisce autenticazione, autorizzazione, sessioni utente, chiavi API e OAuth integrations.

## Funzionalità Principali

- **Autenticazione Dual-Mode**: Cookie (sessioni) + API Keys
- **OAuth Integration**: Google, Discord, GitHub
- **2FA (Two-Factor Authentication)**: TOTP + backup codes
- **Gestione Sessioni**: Session-based authentication con cookie httpOnly e secure
- **API Keys**: Formato `mpl_{env}_{base64}`, hashing PBKDF2 (210k iterazioni)
- **Token Management**: Encryption/decryption tramite ASP.NET DataProtection

## Struttura

### Domain/
Logica di business pura e modelli di dominio:
- **Entities/**: Aggregates principali (User, Session, ApiKey, OAuthAccount)
- **ValueObjects/**: Oggetti valore immutabili (Email, PasswordHash, ApiKeyValue)
- **Services/**: Domain services per logica complessa (es. password hashing, token generation)
- **Events/**: Domain events (UserRegistered, LoginSuccessful, TwoFactorEnabled, etc.)

### Application/
Orchestrazione e casi d'uso (CQRS pattern con MediatR):
- **Commands/**: Operazioni di scrittura
  - Register, Login, Logout
  - ChangePassword, ResetPassword
  - Enable2FA, Verify2FA, GenerateBackupCodes
  - CreateApiKey, RevokeApiKey
  - LinkOAuthAccount, UnlinkOAuthAccount
- **Queries/**: Operazioni di lettura
  - GetCurrentUser
  - GetUserSessions
  - GetUserApiKeys
  - GetLinkedOAuthAccounts
- **DTOs/**: Data Transfer Objects per le risposte
- **Validators/**: FluentValidation validators per validare i comandi
- **EventHandlers/**: Gestori di domain events
- **Interfaces/**: Contratti per repositories e servizi
- **Services/**: Application services per orchestrazione

### Infrastructure/
Implementazioni concrete e adattatori:
- **Repositories/**: Implementazioni EF Core dei repository
- **Services/**: Implementazioni concrete di servizi esterni (email, OAuth providers)
- **Adapters/**: Adattatori per servizi di terze parti

## Pattern Architetturali

- **CQRS**: Separazione tra Commands (scrittura) e Queries (lettura)
- **MediatR**: Tutti gli endpoint HTTP usano `IMediator.Send()` per invocare handlers
- **Domain-Driven Design**: Aggregates, Value Objects, Domain Events
- **Repository Pattern**: Astrazione dell'accesso ai dati
- **Unit of Work**: Gestito da EF Core DbContext

## Security Features

- **Password Hashing**: PBKDF2 con 210,000 iterazioni
- **API Key Storage**: Solo hash memorizzati nel database
- **Token Encryption**: ASP.NET Core Data Protection per OAuth tokens
- **Session Security**: httpOnly, secure, SameSite cookies
- **2FA Support**: TOTP algorithm con backup codes
- **Temporary Sessions**: 5 minuti per completare 2FA

## API Endpoints

```
POST   /api/v1/auth/register           → RegisterCommand
POST   /api/v1/auth/login              → LoginCommand
POST   /api/v1/auth/logout             → LogoutCommand
POST   /api/v1/auth/change-password    → ChangePasswordCommand
POST   /api/v1/auth/reset-password     → ResetPasswordCommand
POST   /api/v1/auth/enable-2fa         → Enable2FACommand
POST   /api/v1/auth/verify-2fa         → Verify2FACommand
GET    /api/v1/auth/me                 → GetCurrentUserQuery
POST   /api/v1/auth/api-keys           → CreateApiKeyCommand
DELETE /api/v1/auth/api-keys/{id}      → RevokeApiKeyCommand
GET    /api/v1/auth/api-keys           → GetUserApiKeysQuery
POST   /api/v1/auth/oauth/link         → LinkOAuthAccountCommand
DELETE /api/v1/auth/oauth/unlink       → UnlinkOAuthAccountCommand
```

## Database Entities

Vedi `Infrastructure/Entities/Authentication/`:
- `User`: Utente principale
- `Session`: Sessioni attive
- `ApiKey`: Chiavi API per autenticazione programmatica
- `OAuthAccount`: Account OAuth collegati

## Testing

- Unit tests per domain logic
- Integration tests con Testcontainers (PostgreSQL)
- Test coverage: >90%

## Dipendenze

- **EF Core**: Persistence
- **MediatR**: CQRS orchestration
- **FluentValidation**: Input validation
- **ASP.NET Core Identity**: Password hashing e Data Protection
- **OtpNet**: TOTP per 2FA

## Note di Migrazione

Questo context è stato completamente migrato alla nuova architettura DDD/CQRS. Il legacy `AuthService` (346 linee) è stato rimosso e sostituito con handlers specializzati.

## Related Documentation

- `docs/06-security/oauth-security.md`
- `docs/06-security/environment-variables-production.md`
- `SECURITY.md`
