# #535 — ME-M3.3 Admin notification on card suppression (spec)

**Parent ADR**: ADR-051 · **Depends on**: #534 (suppression + event) · **BC**: UserNotifications (+ SharedGameCatalog event) · **Date**: 2026-07-12

## Obiettivo
Quando una `MechanicCard` viene soppressa (auto #534 o manuale), notificare **tutti gli admin** in-app, con **email opzionale**
per-admin (opt-in), includendo gioco + motivo + deep-link.

## Stato AC
- **AC-1** ✅ già fatto: `MechanicCardSuppressedEvent(CardId, SharedGameId, ActorId, Reason)` alzato da `MechanicCard.Suppress` (#534).
- **AC-2**: handler in UserNotifications BC → notifica in-app a tutti gli admin (incl. superadmin).
- **AC-3**: email opzionale se admin ha `EmailOnCardSuppressed=true` (default false, opt-in). **Scelta utente**: backend completo +
  settabile via API; checkbox FE = **follow-up** (nuova issue).
- **AC-4**: payload include gioco (title risolto) + motivo; deep-link.

## Decisioni (discovery 2026-07-12)
1. **Handler** `DomainEventHandlerBase<MechanicCardSuppressedEvent>` (pattern raccomandato `SharedGameSubmittedForApprovalNotificationHandler`),
   admin via `IUserRepository.GetAdminUsersAsync()` (**include superadmin**, vs il literal `"admin"` di `NewShareRequestAdminAlertHandler`).
   Dispatch cross-BC ok (stesso assembly, evento `internal`). Auto-discovered da MediatR (no DI manuale).
2. **Email**: il dispatcher accoda l'email come `NotificationQueueItem` col **payload generico** (title/body/deeplink) — **nessun
   template per-tipo necessario**. AC-3 = solo pref `EmailOnCardSuppressed` + map in `IsEmailEnabledForType`.
   ⚠️ Il dispatcher fa `if (preferences == null || IsEmailEnabledForType(...))` → admin **senza** riga preferenze ricevono email
   (default-on per null-prefs, comportamento esistente); solo chi ha una riga con `EmailOnCardSuppressed=false` non la riceve.
3. **In-app sempre**: `DispatchAsync` persiste la Notification (in-app) prima del routing canali → AC-2 soddisfatto a prescindere dalle pref.
4. **Dedup** (CF-1): `SourceEventId = domainEvent.EventId` → una notifica per (admin, event); fan-out per admin ok.
5. **DeepLink** (AC-4): metrics (#532) e re-process queue (#534 deferito) **non esistono** → punto a `/admin/knowledge-base/mechanic-extractor/dashboard`
   (la "AI Comprehension Validation dashboard", proxy metrics reale). Aggiornabile quando #532 atterra.
6. **Settabilità** (AC-3): NON estendere `UpdateNotificationPreferencesCommand` (il FE invia 10 campi Document; un campo extra verrebbe
   resettato a false a ogni save). Command **dedicato** `UpdateCardSuppressionEmailPreferenceCommand` + endpoint `PUT /notifications/preferences/card-suppression`
   (pattern per-gruppo come `UpdateSlackPreferencesCommand`).

## Componenti

### 1. Nuovo `NotificationType.AdminMechanicCardSuppressed` = `"admin_mechanic_card_suppressed"`
File `Domain/ValueObjects/NotificationType.cs`: aggiungi il member + il caso in `FromString`.

### 2. Dispatcher wiring (`Infrastructure/Services/NotificationDispatcher.cs`)
- `IsEmailEnabledForType`: `if (type == AdminMechanicCardSuppressed) return prefs.EmailOnCardSuppressed;` (prima del default `return true`).
- `ResolveTitle`: `"[Admin] Scheda Meccanica Soppressa"`.
- `ResolveSeverity`: block `Warning`.
- `IsSlackEnabledForType`: aggiungi all'esclusione admin (→ `false`, mai user-DM).
- `Infrastructure/Slack/AdminAlertSlackBuilder.cs` `CanHandle`: aggiungi alla OR-chain (formatting canale admin).

### 3. Preferenza `EmailOnCardSuppressed` (default false)
- Aggregate `Domain/Aggregates/NotificationPreferences.cs`: prop `{ get; private set; }` (no `= true`) + param in coda a `Reconstitute` (`bool emailOnCardSuppressed = false`) + metodo `UpdateCardSuppressionEmailPreference(bool email)`.
- Entity `Infrastructure/Entities/UserNotifications/NotificationPreferencesEntity.cs`: `public bool EmailOnCardSuppressed { get; set; }` (default false).
- Config `.../NotificationPreferencesEntityConfiguration.cs`: `builder.Property(e => e.EmailOnCardSuppressed).IsRequired().HasDefaultValue(false);`.
- Repo mapper `Infrastructure/Persistence/NotificationPreferencesRepository.cs`: `MapToDomain` (append `entity.EmailOnCardSuppressed`) + `MapToPersistence` (`EmailOnCardSuppressed = domain.EmailOnCardSuppressed`).
- Migration generata (`dotnet ef migrations add`, nome colonna automatico).

### 4. Event handler (nuovo) `Application/EventHandlers/MechanicCardSuppressedAdminNotificationHandler.cs`
`internal sealed : DomainEventHandlerBase<MechanicCardSuppressedEvent>`, ctor `(MeepleAiDbContext, INotificationDispatcher, IUserRepository, ISharedGameRepository, ILogger<>)` : base(dbContext, logger). Override `HandleEventAsync`:
- risolve title gioco via `ISharedGameRepository.GetByIdAsync(evt.SharedGameId)` (fallback `"un gioco"`);
- `admins = await _userRepository.GetAdminUsersAsync(ct)`;
- per ogni admin: `_dispatcher.DispatchAsync(new NotificationMessage { Type = AdminMechanicCardSuppressed, RecipientUserId = admin.Id, Payload = new GenericPayload("[Admin] Scheda Meccanica Soppressa", $"La scheda meccaniche di «{title}» è stata soppressa. Motivo: {evt.Reason}"), DeepLinkPath = "/admin/knowledge-base/mechanic-extractor/dashboard", SourceEventId = evt.EventId }, ct)`.

### 5. Settabilità (nuovo) `Application/Commands/UpdateCardSuppressionEmailPreferenceCommand.cs` + Handler
`record UpdateCardSuppressionEmailPreferenceCommand(Guid UserId, bool EmailOnCardSuppressed) : ICommand`. Handler: `prefs = GetByUserIdAsync ?? new NotificationPreferences(userId)` → `prefs.UpdateCardSuppressionEmailPreference(cmd.EmailOnCardSuppressed)` → Add/Update (mirror `UpdateNotificationPreferencesCommandHandler`).
Endpoint in `Routing/NotificationPreferencesEndpoints.cs`: `PUT /notifications/preferences/card-suppression` (auth utente → UserId dal claim/command).

## Test (TDD)
- **Aggregate unit**: `UpdateCardSuppressionEmailPreference(true)` setta il bool; default false su `new NotificationPreferences(userId)`.
- **NotificationType unit**: `FromString("admin_mechanic_card_suppressed") == AdminMechanicCardSuppressed`.
- **Handler integration** (Testcontainers, evento reale via `card.Suppress` + `repo.Update` + `SaveChanges`):
  - 2 admin (1 admin + 1 superadmin) → entrambi ricevono 1 Notification in-app `AdminMechanicCardSuppressed`, body contiene game title + reason, DeepLinkPath set, dedup per `SourceEventId`.
  - admin con `EmailOnCardSuppressed=true` → email queue item creato; admin con riga pref `false` → nessun email item (in-app comunque presente).
- **Command integration**: `UpdateCardSuppressionEmailPreferenceCommand` persiste il bool (settable API) + endpoint `PUT` 200.

## Fuori scope (follow-up)
- **Checkbox FE** in `apps/web/.../notifications/preferences/` → nuova issue.
- Deep-link a metrics (#532) / re-process queue (#534 deferito) quando le route esisteranno.
- Slack canale admin: `CanHandle` incluso per consistenza ma la consegna dipende da config Slack (fuori AC).
