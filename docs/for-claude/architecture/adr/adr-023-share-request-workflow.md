# ADR-023: Share Request Workflow Design

## Status

**Accepted** (2026-01-20)

## Context

MeepleAI necessita di un sistema per permettere agli utenti di contribuire giochi dalla loro
libreria personale al catalogo condiviso della community. Il sistema deve:

1. **Facilitare contributi di qualità** dalla community
2. **Garantire controllo qualità** attraverso revisione amministrativa
3. **Prevenire spam e abusi** attraverso rate limiting
4. **Gestire concorrenza** tra admin che revisionano contemporaneamente
5. **Incentivare partecipazione** attraverso gamification (badge system)

### Business Requirements

- **User Experience**: Processo di condivisione semplice e guidato
- **Quality Control**: Ogni contributo validato prima della pubblicazione
- **Scalability**: Gestire centinaia di richieste mensili con piccolo team admin
- **Transparency**: Utenti devono capire stato e feedback sulle loro richieste
- **Engagement**: Sistema di badge per incentivare contributi di qualità

### Technical Constraints

- .NET 9 backend con EF Core e PostgreSQL
- Domain-Driven Design (DDD) architecture
- CQRS pattern con MediatR
- Multi-bounded context (SharedGameCatalog, UserLibrary, Authentication)
- Real-time requirements per admin dashboard

## Decision

Implementiamo una **state machine workflow** con **exclusive review locking** e **tier-based rate limiting**.

### 1. State Machine Workflow

**Stati definiti** (6 stati):

```
Pending → InReview → Approved (terminal)
                   → Rejected (terminal)
                   → ChangesRequested → Pending (loop)
Any     → Withdrawn (terminal, user action)
```

**Transizioni permesse**:

| From | To | Trigger | Actor |
|------|----|----|-------|
| - | Pending | Create request | User |
| Pending | InReview | Start review | Admin |
| ChangesRequested | InReview | Resubmit after changes | User → Admin |
| InReview | Approved | Approve | Admin |
| InReview | Rejected | Reject | Admin |
| InReview | ChangesRequested | Request changes | Admin |
| InReview | Pending | Release lock | Admin |
| Pending, ChangesRequested | Withdrawn | Withdraw | User |

**Invarianti**:
- Solo admin può transizionare da/a InReview
- Utente può modificare solo in stati Pending o ChangesRequested
- Stati terminali (Approved, Rejected, Withdrawn) sono immutabili

### 2. Exclusive Review Lock System

**Design**:

```csharp
public class ShareRequestReviewLock
{
    public Guid ShareRequestId { get; private set; }
    public Guid ReviewerId { get; private set; }  // Admin ID
    public DateTime AcquiredAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private const int DefaultLockDurationMinutes = 30;

    public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;
}
```

**Regole**:
1. **Acquisizione**: Solo richieste in Pending o ChangesRequested
2. **Esclusività**: Max 1 lock attivo per richiesta
3. **Timeout**: 30 minuti default (configurabile)
4. **Auto-release**: Background job rilascia lock scaduti ogni ora
5. **Ownership**: Solo il reviewing admin può rilasciare il proprio lock

**Benefici**:
- ✅ Previene conflitti tra admin (race conditions)
- ✅ Evita lavoro duplicato
- ✅ Garantisce decisioni ponderate (timeout = thinking time)
- ✅ Recupero automatico da abbandoni (auto-release)

**Trade-offs**:
- ❌ Complessità aggiuntiva (lock management logic)
- ❌ Necessità background job per cleanup
- ❌ Possibili blocchi se admin dimentica di rilasciare (mitigato da timeout)

### 3. Tier-Based Rate Limiting

**Design**:

```csharp
public enum UserTier
{
    Free = 0,      // 3 requests/month
    Premium = 1,   // 10 requests/month
    Pro = 2        // Unlimited
}

public class RateLimitConfiguration
{
    public Dictionary<UserTier, int> MonthlyLimits { get; } = new()
    {
        [UserTier.Free] = 3,
        [UserTier.Premium] = 10,
        [UserTier.Pro] = int.MaxValue
    };

    public DateTime ResetDay = 1; // First of month
}
```

**Override System**:
```csharp
public class RateLimitOverride
{
    public Guid UserId { get; private set; }
    public int CustomLimit { get; private set; }  // -1 = unlimited
    public DateTime? ExpiresAt { get; private set; }
    public string Reason { get; private set; }
    public Guid CreatedBy { get; private set; }  // Admin audit trail
}
```

**Enforcement**:
- Validazione in `CreateShareRequestCommandValidator`
- Query per conteggio richieste nel mese corrente
- Override hanno precedenza su limiti tier
- Audit completo di creazione/modifica override

**Benefici**:
- ✅ Previene spam e abusi sistemici
- ✅ Incentiva upgrade a tier superiori
- ✅ Flessibilità per eventi speciali (contest, beta test)
- ✅ Fairness (tutti i tier hanno accesso, diversa velocità)

**Trade-offs**:
- ❌ Potrebbe frustrare utenti free tier molto attivi
- ❌ Richiede gestione override manuale per eccezioni
- ❌ Logica aggiuntiva per calcolo limiti

### 4. Badge System (Gamification)

**Design**: Auto-assegnazione al evento `ShareRequestApprovedDomainEvent`

**Badge Tiers**:
```yaml
FirstContribution:
  requirement: 1 approved
  tier: Bronze

Contributor5:
  requirement: 5 approved
  tier: Silver

Contributor10:
  requirement: 10 approved
  tier: Gold

Contributor25:
  requirement: 25 approved
  tier: Diamond

ContributorElite:
  requirement: 50 approved
  tier: Elite

QualityPremium:
  requirement: 10 approved without changes requested
  tier: Premium
  special: true
```

**Evaluation Logic**:
- Handler `BadgeEvaluationOnApprovalHandler` ascolta `ShareRequestApprovedDomainEvent`
- Calcola badge eligibility per il contributore
- Assegna nuovi badge automaticamente
- Trigger notifica `BadgeEarnedNotification`

**Benefici**:
- ✅ Incentiva contributi di qualità
- ✅ Gamification engagement
- ✅ Riconoscimento pubblico contributori
- ✅ Competizione sana (leaderboard)

---

## Consequences

### Positive

✅ **Workflow chiaro e prevedibile**: Stati ben definiti, transizioni esplicite
✅ **Concurrency safety**: Lock esclusivo previene race conditions
✅ **Quality assurance**: Ogni contributo validato da admin
✅ **Spam prevention**: Rate limiting efficace
✅ **User engagement**: Badge system incentiva partecipazione
✅ **Scalability**: Lock timeout e background jobs gestiscono abbandoni
✅ **Flexibility**: Override system per casi speciali
✅ **Auditability**: Tracking completo di decisioni e modifiche

### Negative

❌ **Complessità implementativa**: State machine + lock + rate limit = ~15 issue di sviluppo
❌ **Overhead admin**: Richiede team admin attivo per revisioni tempestive
❌ **User friction**: Rate limit potrebbe frustrare utenti free tier molto attivi
❌ **Background jobs**: Necessità infrastruttura per lock cleanup
❌ **Testing complexity**: Più stati e transizioni = più test cases

### Risks

⚠️ **Admin bottleneck**: Se volume richieste supera capacità team admin
**Mitigation**: Monitorare SLA (< 48h), espandere team se necessario, considerare auto-approval per contributori trusted

⚠️ **Lock starvation**: Admin interrotti frequentemente potrebbero non completare mai revisioni
**Mitigation**: Timeout 30min aggressivo, notifiche lock scadenza, possibilità estensione

⚠️ **Rate limit bypass**: Utenti potrebbero creare account multipli
**Mitigation**: Verifica email, detection pattern sospetti, ban account duplicati

---

## Alternatives Considered

### Alternative 1: Approval Queue senza Lock

**Design**: Richieste processate in FIFO, nessun lock esclusivo

**Pro**:
- Più semplice da implementare
- Nessuna gestione lock/timeout
- Parallelismo admin più facile

**Con**:
- ❌ Rischio race condition (2 admin approvano stessa richiesta)
- ❌ Lavoro duplicato (2 admin revisionano in parallelo)
- ❌ Necessità conflict resolution complessa

**Rejected**: Lock esclusivo è critico per data integrity e efficienza admin.

---

### Alternative 2: Auto-Approval con Community Review

**Design**: Richieste approvate automaticamente, community può flaggare problemi

**Pro**:
- Zero overhead admin
- Velocità massima per utenti
- Scalabilità illimitata

**Con**:
- ❌ Rischio spam nel catalogo
- ❌ Qualità inconsistente
- ❌ Cleanup post-pubblicazione più costoso
- ❌ Esperienza utente peggiore se molti flag

**Rejected**: Quality-first approach richiede approval admin preventiva.

---

### Alternative 3: Sistema di Voto Community

**Design**: Community vota su richieste pendenti, threshold → auto-approval

**Pro**:
- Engagement community massimo
- Decisioni democratiche
- Scalabilità migliore

**Con**:
- ❌ Troppo complesso per MVP
- ❌ Rischio vote brigading
- ❌ Slow approval times (attesa voti)
- ❌ Quality non garantita (voto ≠ expertise)

**Rejected**: Troppo complesso per Phase 7. Possibile futuro enhancement.

---

### Alternative 4: Soft Lock (Advisory, Non-Exclusive)

**Design**: Lock advisory che suggerisce chi sta revisionando ma non blocca altri admin

**Pro**:
- Flessibilità maggiore
- Nessun problema di lock scaduti
- Admin possono collaborare su richieste complesse

**Con**:
- ❌ Possibili race conditions
- ❌ Lavoro duplicato comunque possibile
- ❌ Conflitti da risolvere manualmente

**Rejected**: Exclusive lock fornisce garanzie più forti, complessità gestibile.

---

## Implementation Notes

### Domain Events

```csharp
// Generati da ShareRequest entity
ShareRequestCreatedDomainEvent
ShareRequestApprovedDomainEvent
ShareRequestRejectedDomainEvent
ShareRequestChangesRequestedDomainEvent
ShareRequestWithdrawnDomainEvent
ReviewLockAcquiredDomainEvent
ReviewLockReleasedDomainEvent
```

### Background Jobs

**LockCleanupJob** (Hangfire, ogni ora):
```csharp
public async Task Execute()
{
    var expiredLocks = await _repository.GetExpiredLocksAsync();
    foreach (var lock in expiredLocks)
    {
        await _mediator.Send(new ReleaseReviewLockCommand(
            lock.ShareRequestId,
            lock.ReviewerId,
            isAutomatic: true
        ));
    }
}
```

### Bounded Context Interactions

**SharedGameCatalog** (owner):
- ShareRequest aggregate
- Review lock management
- Badge evaluation

**UserLibrary** (dependency):
- Source game data (read-only)
- Validation: game exists in user library

**Authentication** (dependency):
- User identity and tier information
- Admin role verification

**UserNotifications** (subscriber):
- Email notifications per state changes
- Badge earned celebrations

---

## Related Decisions

- **ADR-001**: Domain-Driven Design Architecture
- **ADR-003**: CQRS Pattern with MediatR
- **ADR-007**: Event-Driven Notifications
- **ADR-012**: Rate Limiting Strategy

## Future Enhancements

**Considered for future phases**:

1. **Trusted Contributors Auto-Approval** (Post-MVP)
   - Utenti con track record eccellente (es. 20 approved, 0 rejected, 95% approval rate)
   - Skip admin review, pubblicazione immediata
   - Audit trail per review post-pubblicazione

2. **Community Voting System** (Phase 8+)
   - Community vota su richieste pendenti
   - Threshold di voti → fast-track review
   - Non sostituisce admin approval, ma prioritizza queue

3. **AI-Assisted Pre-Screening** (Future)
   - ML model pre-valida qualità descrizione
   - Auto-detect duplicati tramite embedding similarity
   - Suggerimenti miglioramento automatici all'utente

4. **Collaborative Review** (Future)
   - Multiple admin possono collaborare su richieste complesse
   - Shared lock con co-ownership
   - Chat interno per discussione casi edge

---

**Date**: 2026-01-20
**Authors**: Team MeepleAI
**Reviewers**: Architecture Team
**Status**: Implemented (Epic #2718)
