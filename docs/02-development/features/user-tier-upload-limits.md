# User Tier-Based PDF Upload Limits

## Overview

Il sistema di limiti di upload PDF basato su tier utente permette agli amministratori di configurare quanti PDF ogni classe di utenti (free, normal, premium) può caricare giornalmente e settimanalmente.

## Architettura

### 1. User Tier System

**Value Object**: `UserTier`
- Percorso: `BoundedContexts/Authentication/Domain/ValueObjects/UserTier.cs`
- Valori: `Free`, `Normal`, `Premium`
- Metodi: `IsFree()`, `IsNormal()`, `IsPremium()`, `GetLevel()`, `HasLevel()`

**Domain Entity**: `User`
- Aggiunta proprietà: `UserTier Tier` (default: `Free`)
- Metodo: `UpdateTier(UserTier newTier)` - solo admin possono cambiare tier
- Domain Event: `UserTierChangedEvent`

### 2. Upload Quota Service

**Interface**: `IPdfUploadQuotaService`
- Percorso: `BoundedContexts/DocumentProcessing/Domain/Services/IPdfUploadQuotaService.cs`

**Implementation**: `PdfUploadQuotaService`
- Percorso: `BoundedContexts/DocumentProcessing/Infrastructure/Services/PdfUploadQuotaService.cs`
- Storage: Redis-based (chiavi: `pdf:upload:daily:{userId}:{date}` e `pdf:upload:weekly:{userId}:{week}`)
- TTL automatico: 25h per daily, 8 giorni per weekly
- Fail-open pattern: se Redis è down, l'upload è permesso (priorità availability)

**Metodi**:
```csharp
Task<PdfUploadQuotaResult> CheckQuotaAsync(Guid userId, UserTier userTier, Role userRole, CancellationToken ct)
Task IncrementUploadCountAsync(Guid userId, CancellationToken ct)
Task<PdfUploadQuotaInfo> GetQuotaInfoAsync(Guid userId, UserTier userTier, Role userRole, CancellationToken ct)
```

### 3. Integration Point

**UploadPdfCommandHandler**
- Verifica quota PRIMA dell'upload (dopo validazione game)
- Admin e Editor: bypass completo (upload illimitato)
- User: controllo quota basato su tier
- Incrementa contatore DOPO upload riuscito

## Configurazione

### Limiti Default (appsettings.json)

```json
"UploadLimits": {
  "free": {
    "DailyLimit": 5,
    "WeeklyLimit": 20
  },
  "normal": {
    "DailyLimit": 20,
    "WeeklyLimit": 100
  },
  "premium": {
    "DailyLimit": 100,
    "WeeklyLimit": 500
  }
}
```

### Configurazione Dinamica

Gli admin possono modificare i limiti tramite il sistema di configurazione dinamica:

**Chiavi configurazione**:
- `UploadLimits:free:DailyLimit`
- `UploadLimits:free:WeeklyLimit`
- `UploadLimits:normal:DailyLimit`
- `UploadLimits:normal:WeeklyLimit`
- `UploadLimits:premium:DailyLimit`
- `UploadLimits:premium:WeeklyLimit`

**Fallback chain**:
1. Database (SystemConfiguration)
2. appsettings.json
3. Hardcoded defaults nel service

## Database

### Migration

Esegui la migration per aggiungere il campo `Tier` alla tabella `Users`:

```bash
cd apps/api/src/Api
dotnet ef migrations add AddUserTierAndUploadQuotas --project .
dotnet ef database update
```

**Modifiche**:
- Tabella `Users`: aggiunta colonna `Tier` VARCHAR(20) DEFAULT 'free'
- Tutti gli utenti esistenti avranno tier = 'free'

### Schema

```sql
ALTER TABLE "Users" ADD COLUMN "Tier" VARCHAR(20) NOT NULL DEFAULT 'free';
```

## Comportamento

### Bypass per Admin/Editor

Admin e Editor hanno upload **illimitato** (bypass completo del sistema di quota).

```csharp
if (userRole.IsAdmin() || userRole.IsEditor())
{
    return PdfUploadQuotaResult.Success(0, int.MaxValue, 0, int.MaxValue, ...);
}
```

### Controllo Quota per User

1. **Recupero limiti** da configurazione (con fallback)
2. **Recupero usage** da Redis (contatori daily/weekly)
3. **Verifica limiti**:
   - Se `dailyUsed >= dailyLimit`: negato
   - Se `weeklyUsed >= weeklyLimit`: negato
   - Altrimenti: permesso

### Reset Automatico

- **Daily**: Reset a mezzanotte UTC (TTL 25h)
- **Weekly**: Reset ogni lunedì a mezzanotte UTC (TTL 8 giorni)

## Esempi di Utilizzo

### Controllare Quota Programmaticamente

```csharp
var quotaInfo = await _quotaService.GetQuotaInfoAsync(userId, userTier, userRole, ct);

Console.WriteLine($"Daily: {quotaInfo.DailyRemaining}/{quotaInfo.DailyLimit}");
Console.WriteLine($"Weekly: {quotaInfo.WeeklyRemaining}/{quotaInfo.WeeklyLimit}");
Console.WriteLine($"Reset giornaliero: {quotaInfo.DailyResetAt}");
Console.WriteLine($"Reset settimanale: {quotaInfo.WeeklyResetAt}");
```

### Modificare Tier Utente

```csharp
// Solo admin possono cambiare tier (requesterRole viene verificato)
var requesterRole = Role.Admin; // Role del richiedente (deve essere Admin)
var user = await _userRepository.GetByIdAsync(userId, ct);
user.UpdateTier(UserTier.Premium, requesterRole);
await _userRepository.UpdateAsync(user, ct);
await _unitOfWork.SaveChangesAsync(ct);
```

### Modificare Limiti via Configurazione

```csharp
// Aumentare limite giornaliero per tier Premium
await _configService.CreateConfigurationAsync(new CreateConfigurationCommand
{
    Key = "UploadLimits:premium:DailyLimit",
    Value = "200",
    ValueType = "int",
    Category = "UploadLimits",
    RequiresRestart = false,
    RequesterRole = Role.Admin
}, ct);
```

## Messaggi di Errore

### Daily Limit Reached

```
Daily upload limit reached (5 PDF/day for free tier). Limit resets in 12.3 hours.
```

### Weekly Limit Reached

```
Weekly upload limit reached (20 PDF/week for free tier). Limit resets in 3.5 days.
```

## Metriche e Logging

### Metriche Prometheus

```csharp
RecordUploadMetricSafely("quota_exceeded", file.Length);
```

### Logging

```csharp
_logger.LogWarning(
    "PDF upload denied for user {UserId} ({Tier}): {Reason}",
    userId,
    user.Tier,
    quotaResult.ErrorMessage);

_logger.LogDebug(
    "PDF upload quota check passed for user {UserId} ({Tier}): Daily {DailyUsed}/{DailyLimit}, Weekly {WeeklyUsed}/{WeeklyLimit}",
    userId,
    user.Tier,
    quotaResult.DailyUploadsUsed,
    quotaResult.DailyLimit,
    quotaResult.WeeklyUploadsUsed,
    quotaResult.WeeklyLimit);
```

## Testing

### Unit Tests

Creare test per:
- `UserTier` Value Object (parsing, validazione)
- `PdfUploadQuotaService` (check quota, increment, get info)
- `UploadPdfCommandHandler` (integrazione quota check)

### Integration Tests

Testare:
- Upload con diversi tier
- Raggiungimento limiti daily/weekly
- Reset automatico
- Bypass admin/editor
- Fail-open quando Redis è down

## Sicurezza

1. **Nessun privilege escalation**: Solo admin possono cambiare tier utente (verificato in `UpdateTier` con parametro `requesterRole`)
2. **Fail-open**: Se Redis è down, l'upload è permesso (priorità availability)
3. **Rate limiting separato**: Il rate limiting (token bucket) rimane indipendente
4. **Validazione input**: UserTier validato con whitelist (free/normal/premium)
5. **Atomic operations**: Redis increment+TTL eseguito atomicamente con Lua script (previene race conditions)
6. **Null safety**: User validation in UploadPdfCommandHandler previene null reference exceptions

## Performance

- **Redis per tracking**: O(1) read/write
- **TTL automatico**: Nessuna pulizia manuale necessaria
- **Caching configurazione**: Limiti cachati dal ConfigurationService
- **Fail-open**: Non blocca upload se Redis è irraggiungibile

## Bug Fixes (2025-11-22)

### Critical Fixes
1. ✅ **GetWeekKey ISO 8601 Year Transition**: Fixed edge case for Dec 29-31 (week 1 of next year)
2. ✅ **UpdateTier Authorization**: Added `requesterRole` parameter with admin-only check
3. ✅ **Redis Race Condition**: Made increment+TTL atomic using Lua script
4. ✅ **Null User Handling**: Added null check in UploadPdfCommandHandler

### Code Quality Improvements
5. ✅ **Default Quotas Constants**: Extracted hardcoded limits to `DefaultQuotas` nested class

## Roadmap Future

- [ ] UI admin per gestire tier utenti (pagina `/admin/users`)
- [ ] UI admin per configurare limiti (sezione in `/admin/configuration`)
- [ ] Endpoint API per ottenere quota info (`GET /api/v1/users/me/upload-quota`)
- [ ] Dashboard utente con visualizzazione quota
- [ ] Notifiche email quando si avvicina al limite
- [ ] Analytics: tracking tier usage per ottimizzare limiti
- [ ] Unit tests per PdfUploadQuotaService
- [ ] Integration tests per quota enforcement

## References

- **DDD Architecture**: Seguita architettura esistente (bounded contexts, value objects, domain services)
- **Pattern usati**: Repository, Unit of Work, Domain Events, Fail-Open
- **Simile a**: `RateLimitService` (token bucket su Redis)
- **Documentazione**: `docs/01-architecture/adr/` (per decisioni architetturali)

---

**Versione**: 1.0
**Data**: 2025-11-22
**Autore**: Development Team
