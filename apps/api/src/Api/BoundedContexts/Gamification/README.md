# Gamification Bounded Context

## Responsabilita

Gestisce il sistema di achievement (obiettivi), badge e progressi degli utenti. Valuta regole basate sull'attivita degli utenti e sblocca achievement quando le soglie vengono raggiunte.

## Funzionalita Principali

- **Catalogo Achievement**: Definizione di achievement con codice univoco, nome, descrizione, icona, punti, rarita e categoria
- **Tracciamento Progressi**: Monitoraggio del progresso utente (0-100%) per ogni achievement
- **Sblocco Automatico**: Valutazione giornaliera delle regole e sblocco automatico al raggiungimento della soglia
- **Notifiche**: Invio notifica all'utente quando un achievement viene sbloccato
- **Cache**: Risultati cachati con HybridCache per ridurre il carico sui query
- **Achievement Recenti**: Query per gli achievement sbloccati piu di recente

## Struttura

### Domain/
Logica di business pura e modelli di dominio:
- **Entities/**: Aggregates principali (Achievement, UserAchievement)
- **Enums/**: Enumerazioni di dominio (AchievementCategory, AchievementRarity)
- **Repositories/**: Interfacce repository (IAchievementRepository, IUserAchievementRepository)

### Application/
Orchestrazione e casi d'uso (CQRS pattern con MediatR):
- **Queries/**: Operazioni di lettura
  - GetAchievements: Tutti gli achievement attivi con stato di sblocco dell'utente
  - GetRecentAchievements: Achievement sbloccati piu di recente (con limite configurabile, default 5)
- **DTOs/**: Data Transfer Objects per le risposte (AchievementDto, RecentAchievementDto)
- **Services/**: Servizi applicativi
  - IAchievementRuleEvaluator / AchievementRuleEvaluator: Valuta il progresso per ogni codice achievement interrogando tabelle cross-context
- **Jobs/**: Background jobs schedulati
  - AchievementEvaluationJob: Job Quartz.NET giornaliero (04:00 UTC) che valuta tutte le regole per tutti gli utenti attivi

### Infrastructure/
Implementazioni concrete e adattatori:
- **Persistence/**: Implementazioni EF Core dei repository (AchievementRepository, UserAchievementRepository)
- **DependencyInjection/**: Registrazione servizi tramite `AddGamificationContext()`

## Pattern Architetturali

- **CQRS**: Separazione tra Commands (scrittura) e Queries (lettura) via MediatR
- **MediatR**: Tutti gli endpoint HTTP usano `IMediator.Send()` per invocare handlers
- **Domain-Driven Design**: Aggregates con factory method (`Create()`) e private setters, Reconstitute per reidratazione da persistenza
- **Repository Pattern**: Astrazione dell'accesso ai dati con mapping manuale Domain <-> Persistence Entity
- **Background Jobs**: Quartz.NET per valutazione periodica delle regole (`[DisallowConcurrentExecution]`)
- **HybridCache**: Cache a due livelli (memoria + Redis) con invalidazione per tag

## API Endpoints

```
GET /api/v1/achievements          -> GetAchievementsQuery
GET /api/v1/achievements/recent   -> GetRecentAchievementsQuery (?limit=5)
```

Entrambi gli endpoint richiedono autenticazione e restituiscono i dati relativi all'utente autenticato.

## Modelli di Dominio

### Achievement Aggregate
- **Identita**: Id (GUID)
- **Proprieta**: Code, Name, Description, IconUrl, Points, Rarity, Category, Threshold, IsActive, CreatedAt
- **Invarianti**:
  - Code, Name, Description non possono essere vuoti
  - Points e Threshold devono essere positivi
- **Comportamento**: Activate(), Deactivate()

### UserAchievement Aggregate
- **Identita**: Id (GUID)
- **Proprieta**: UserId, AchievementId, Progress (0-100), UnlockedAt, CreatedAt, UpdatedAt
- **Invarianti**:
  - UserId e AchievementId non possono essere Guid.Empty
  - Progress clampato tra 0 e 100
  - Sblocco automatico quando Progress raggiunge 100
- **Comportamento**: UpdateProgress(int) -> bool (true se appena sbloccato), IsUnlocked (computed)

### AchievementCategory (Enum)
Streak, Milestone, Expertise, Social, Special

### AchievementRarity (Enum)
Common, Uncommon, Rare, Epic, Legendary

## Regole di Valutazione Achievement

L'`AchievementRuleEvaluator` interroga tabelle cross-context per calcolare il progresso:

| Codice | Regola | Soglia Tipica |
|--------|--------|---------------|
| `FIRST_GAME` | Conteggio giochi in libreria | 1 |
| `COLLECTOR_100` | Conteggio giochi in libreria | 100 |
| `SESSION_MASTER` | Sessioni di gioco completate | variabile |
| `EXPLORER_10` | Giochi distinti giocati | 10 |
| `AI_EXPERT_50` | Sessioni chat AI | 50 |
| `SHARER` | Richieste di condivisione approvate | variabile |
| `STREAK_7_DAYS` | Giorni consecutivi di gioco | 7 |
| `STREAK_30_DAYS` | Giorni consecutivi di gioco | 30 |
| `REVIEWER_5` | Note nella libreria | 5 |
| `VETERAN_365` | Giorni dall'iscrizione | 365 |

## Dipendenze

- **EF Core**: Persistence e query cross-context
- **MediatR**: CQRS orchestration
- **FluentValidation**: Input validation (UserId required, Limit 1-20)
- **Quartz.NET**: Scheduling del job di valutazione giornaliero
- **HybridCache**: Caching dei risultati query (TTL 5-10 min)

### Dipendenze Cross-Context
- **UserLibrary**: Conteggio giochi in libreria, note, sessioni di gioco
- **KnowledgeBase**: Conteggio sessioni chat AI, richieste di condivisione
- **UserNotifications**: Invio notifiche di sblocco achievement
- **Authentication**: Verifica utenti attivi (non sospesi)

## Related Documentation

- `docs/01-architecture/overview/system-architecture.md`
- `docs/03-api/board-game-ai-api-specification.md`
