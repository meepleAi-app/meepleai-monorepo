# UserNotifications Bounded Context

## Responsabilita

Gestisce l'intero ciclo di vita delle notifiche utente su tutti i canali: in-app, email, push (Web Push/VAPID) e Slack. Include code email con retry, template email versionati, integrazione Slack con OAuth e message builders, preferenze di notifica multi-canale e job di background per elaborazione asincrona.

## Funzionalita Principali

- **Notifiche In-App**: Creazione, lettura, conteggio non lette, streaming SSE in tempo reale
- **Coda Email Asincrona**: Accodamento, invio con retry esponenziale (1m, 5m, 30m), dead letter queue
- **Template Email**: CRUD con versionamento, pubblicazione, anteprima e placeholder dinamici
- **Push Notifications**: Sottoscrizione/cancellazione Web Push con chiavi VAPID, invio test
- **Integrazione Slack**: OAuth connect/disconnect, invio messaggi per tipo (game night, badge, PDF, share request, admin alert), gestione interazioni, canali team
- **Preferenze Notifiche**: Configurazione multi-canale per utente (in-app, email, push, Slack)
- **Unsubscribe Email**: Token-based one-click unsubscribe (RFC 8058)
- **Event Handlers**: Reazione automatica a domain events da altri BC (PDF processing, game night, badge, share request, ecc.)
- **Background Jobs**: Elaborazione coda email, pulizia notifiche scadute, monitoraggio dead letter, digest admin, promemoria Slack

## Struttura

### Domain/
Logica di business pura e modelli di dominio:
- **Aggregates/**: Aggregate root principali
  - `Notification`: Notifica in-app con tipo, severita, stato lettura, link e metadata JSON
  - `NotificationPreferences`: Preferenze multi-canale per utente
  - `EmailQueueItem`: Email in coda con lifecycle (pending, processing, sent, failed, dead letter) e retry esponenziale
  - `EmailTemplate`: Template email versionato con subject, body HTML e placeholder
  - `NotificationQueueItem`: Elemento coda notifiche generiche
  - `SlackConnection`: Connessione OAuth Slack con bot access token, team e DM channel
- **ValueObjects/**: Oggetti valore immutabili
  - `EmailQueueStatus`: Pending, Processing, Sent, Failed, DeadLetter
  - `NotificationType`: Tipi di notifica (PDF, GameNight, Badge, ShareRequest, ecc.)
  - `NotificationSeverity`: Info, Warning, Error, Success
  - `NotificationChannelType`: InApp, Email, Push, Slack
  - `NotificationQueueStatus`: Stato coda notifiche
  - `NotificationPayloads`: Payload tipizzati per ogni tipo di notifica
- **Repositories/**: Interfacce repository
  - `INotificationRepository`, `INotificationPreferencesRepository`
  - `IEmailQueueRepository`, `IEmailTemplateRepository`
  - `INotificationQueueRepository`, `ISlackConnectionRepository`
- **EmailTemplatePlaceholders**: Costanti per placeholder dei template email

### Application/
Orchestrazione e casi d'uso (CQRS pattern con MediatR):
- **Commands/** (23 comandi):
  - *Notifiche*: MarkNotificationRead, MarkAllNotificationsRead, SendManualNotification
  - *Email*: EnqueueEmail, ResendFailedEmail, AdminRetryEmail, RetryDeadLetter, RetryAllDeadLetters, SendTestEmail
  - *Template*: CreateEmailTemplate, UpdateEmailTemplate, PublishEmailTemplate, PreviewEmailTemplate
  - *Push*: SubscribePushNotifications, UnsubscribePushNotifications, SendTestPushNotification
  - *Slack*: ConnectSlack, DisconnectSlack, SlackOAuthCallback, HandleSlackInteraction, UpdateSlackPreferences, UpdateSlackTeamChannel
  - *Unsubscribe*: UnsubscribeEmail
- **Queries/** (16 query):
  - *Notifiche*: GetNotifications, GetUnreadCount, GetNotificationMetrics, GetNotificationQueue, GetDeadLetterQueue, GetLegacyQueueCount
  - *Email*: GetEmailQueueStats, GetEmailHistory, GetAdminEmailHistory, GetDeadLetterEmails
  - *Template*: GetEmailTemplates, GetEmailTemplateById, GetEmailTemplateVersions
  - *Push*: GetVapidPublicKey
  - *Slack*: GetSlackConnectionStatus, GetSlackConnections, GetSlackTeamChannels
  - *Preferenze*: GetNotificationPreferences
- **DTOs/**: Data Transfer Objects
  - `NotificationDto`, `NotificationPreferencesDto`, `NotificationMetricsDto`
  - `EmailQueueItemDto`, `EmailQueueStatsDto`, `EmailTemplateDto`
  - `NotificationQueueItemDto`, `SlackConnectionDto`, `SlackConnectionStatusDto`, `SlackTeamChannelDto`
- **Validators/**: FluentValidation per ogni command
- **EventHandlers/** (22 handler): Reazione a domain events cross-BC
  - *Game Night*: Published, Cancelled, RsvpReceived, Reminder1h, Reminder24h
  - *Gamification*: BadgeEarned, MilestoneBadge
  - *Document Processing*: PdfNotification, ProcessingJobNotification, VectorDocumentReady
  - *Knowledge Base*: AgentLinked, NotifyAgentReady
  - *SharedGameCatalog*: ShareRequestCreated, ReviewStarted, Approved, Rejected, ChangesRequested, SubmittedForApproval, NewShareRequestAdminAlert
  - *Administration*: UserSuspended, UserUnsuspended, RateLimitApproaching
- **Services/**: Interfacce application services
  - `INotificationDispatcher`: Dispatcher multi-canale
  - `IEmailTemplateService`: Rendering template con placeholder
  - `IUnsubscribeTokenService`: Generazione/validazione token unsubscribe
  - `IUserNotificationBroadcaster`: Broadcasting SSE real-time

### Infrastructure/
Implementazioni concrete e adattatori:
- **Persistence/**: Implementazioni EF Core dei repository
  - `NotificationRepository`, `NotificationPreferencesRepository`
  - `EmailQueueRepository`, `EmailTemplateRepository`
  - `NotificationQueueRepository`, `SlackConnectionRepository`
- **Services/**:
  - `NotificationDispatcher`: Dispatch multi-canale (in-app, email, push, Slack)
  - `EmailTemplateService`: Rendering Scriban/Razor dei template email
  - `UnsubscribeTokenService`: Token HMAC per unsubscribe sicuro
  - `InMemoryUserNotificationBroadcaster`: Broadcasting SSE in-memory
- **Slack/**: Integrazione Slack completa
  - `ISlackMessageBuilder`: Interfaccia builder per messaggi Block Kit
  - `SlackMessageBuilderFactory`: Factory per selezionare il builder corretto
  - `GameNightSlackBuilder`: Messaggi per eventi game night
  - `BadgeSlackBuilder`: Messaggi per badge e achievement
  - `PdfProcessingSlackBuilder`: Messaggi per stato elaborazione PDF
  - `ShareRequestSlackBuilder`: Messaggi per richieste condivisione giochi
  - `AdminAlertSlackBuilder`: Alert amministrativi
  - `GenericSlackBuilder`: Fallback per notifiche generiche
  - `SlackSignatureValidator`: Validazione firma richieste Slack (HMAC-SHA256)
- **Scheduling/**: Background jobs (Hangfire/hosted services)
  - `EmailProcessorJob`: Elaborazione coda email con retry
  - `SlackNotificationProcessorJob`: Invio notifiche Slack in batch
  - `NotificationCleanupJob`: Pulizia notifiche vecchie
  - `DeadLetterMonitorJob`: Monitoraggio e alerting dead letter queue
  - `AdminShareRequestDigestJob`: Digest periodico richieste condivisione per admin
  - `StaleShareRequestWarningJob`: Warning per richieste in attesa troppo a lungo
  - `CooldownEndReminderJob`: Promemoria fine periodo cooldown
- **Configuration/**: `SlackNotificationConfiguration` (client ID, secret, signing secret)
- **HealthChecks/**: `SlackApiHealthCheck`, `SlackQueueHealthCheck`
- **DependencyInjection/**: `UserNotificationsServiceExtensions` (registrazione DI)

## Pattern Architetturali

- **CQRS**: Separazione tra Commands (scrittura) e Queries (lettura)
- **MediatR**: Tutti gli endpoint HTTP usano `IMediator.Send()` per invocare handlers
- **Domain-Driven Design**: Aggregates con private setters, factory methods, Value Objects immutabili
- **Repository Pattern**: Astrazione dell'accesso ai dati con interfacce nel Domain
- **Factory Pattern**: `SlackMessageBuilderFactory` seleziona il builder corretto per tipo notifica
- **Strategy Pattern**: `ISlackMessageBuilder` con implementazioni specifiche per dominio
- **Event-Driven Architecture**: 22 event handler reagiscono a domain events da altri BC
- **Retry con Exponential Backoff**: Email queue con policy 1m, 5m, 30m prima di dead letter
- **Dead Letter Queue**: Email fallite dopo max retry vengono spostate per revisione manuale

## API Endpoints

### User Endpoints (`/api/v1/`)

```
GET    /api/v1/notifications                          → GetNotificationsQuery
GET    /api/v1/notifications/unread-count              → GetUnreadCountQuery
POST   /api/v1/notifications/{id}/mark-read            → MarkNotificationReadCommand
POST   /api/v1/notifications/mark-all-read             → MarkAllNotificationsReadCommand
GET    /api/v1/notifications/stream                    → SSE real-time stream

GET    /api/v1/notifications/preferences               → GetNotificationPreferencesQuery
PUT    /api/v1/notifications/preferences               → UpdateNotificationPreferencesCommand

POST   /api/v1/notifications/push/subscribe            → SubscribePushNotificationsCommand
DELETE /api/v1/notifications/push/unsubscribe          → UnsubscribePushNotificationsCommand
POST   /api/v1/notifications/push/test                 → SendTestPushNotificationCommand
GET    /api/v1/notifications/push/vapid-key            → GetVapidPublicKeyQuery

GET    /api/v1/emails                                  → GetEmailHistoryQuery
POST   /api/v1/emails/{id}/resend                      → ResendFailedEmailCommand
```

### Slack Integration (`/api/v1/integrations/slack/`)

```
GET    /api/v1/integrations/slack/connect              → ConnectSlackCommand (OAuth start)
GET    /api/v1/integrations/slack/callback             → SlackOAuthCallbackCommand
DELETE /api/v1/integrations/slack/disconnect            → DisconnectSlackCommand
GET    /api/v1/integrations/slack/status               → GetSlackConnectionStatusQuery
POST   /api/v1/integrations/slack/interactions         → HandleSlackInteractionCommand
PUT    /api/v1/integrations/slack/notifications/preferences/slack → UpdateSlackPreferencesCommand
```

### Admin Email Queue (`/admin/emails/`)

```
GET    /admin/emails/stats                             → GetEmailQueueStatsQuery
GET    /admin/emails/dead-letter                       → GetDeadLetterEmailsQuery
POST   /admin/emails/{id}/retry                        → AdminRetryEmailCommand
GET    /admin/emails/history                            → GetAdminEmailHistoryQuery
POST   /admin/emails/retry-all-dead-letters            → RetryAllDeadLettersCommand
POST   /admin/emails/test                              → SendTestEmailCommand
```

### Admin Email Templates (`/admin/email-templates/`)

```
GET    /admin/email-templates                          → GetEmailTemplatesQuery
GET    /admin/email-templates/{id}                     → GetEmailTemplateByIdQuery
POST   /admin/email-templates                          → CreateEmailTemplateCommand
PUT    /admin/email-templates/{id}                     → UpdateEmailTemplateCommand
POST   /admin/email-templates/{id}/publish             → PublishEmailTemplateCommand
POST   /admin/email-templates/{id}/preview             → PreviewEmailTemplateCommand
GET    /admin/email-templates/{name}/versions          → GetEmailTemplateVersionsQuery
```

### Admin Notification Queue (`/admin/notifications/`)

```
GET    /admin/notifications/queue                      → GetNotificationQueueQuery
GET    /admin/notifications/dead-letter                → GetDeadLetterQueueQuery
POST   /admin/notifications/dead-letter/{id}/retry     → RetryDeadLetterCommand
GET    /admin/notifications/metrics                    → GetNotificationMetricsQuery
GET    /admin/notifications/legacy-queue/count         → GetLegacyQueueCountQuery
POST   /admin/notifications/send                       → SendManualNotificationCommand
```

### Admin Slack (`/admin/slack/`)

```
GET    /admin/slack/connections                        → GetSlackConnectionsQuery
GET    /admin/slack/team-channels                      → GetSlackTeamChannelsQuery
PUT    /admin/slack/team-channels/{id}                 → UpdateSlackTeamChannelCommand
```

### Public

```
GET    /notifications/unsubscribe?token=...            → UnsubscribeEmailCommand
```

## Modelli di Dominio

### Notification Aggregate
- **Identita**: NotificationId (GUID)
- **Proprieta**: UserId, Type, Severity, Title, Message, Link, Metadata (JSON), IsRead, ReadAt, CorrelationId
- **Invarianti**:
  - Title e Message obbligatori
  - ReadAt impostato solo al momento della lettura

### EmailQueueItem Aggregate
- **Identita**: EmailId (GUID)
- **Proprieta**: UserId, To, Subject, HtmlBody, Status, RetryCount, MaxRetries, NextRetryAt, ErrorMessage, CorrelationId
- **Invarianti**:
  - To, Subject, HtmlBody non vuoti
  - Retry con backoff esponenziale: 1m, 5m, 30m
  - Dopo max retry passa a DeadLetter

### SlackConnection Aggregate
- **Identita**: ConnectionId (GUID)
- **Proprieta**: UserId, SlackUserId, SlackTeamId, SlackTeamName, BotAccessToken (encrypted), DmChannelId, IsActive
- **Invarianti**:
  - SlackUserId, SlackTeamId, SlackTeamName, BotAccessToken, DmChannelId non vuoti
  - BotAccessToken criptato tramite EF ValueConverter

### EmailTemplate Aggregate
- **Identita**: TemplateId (GUID)
- **Proprieta**: Name, Subject, HtmlBody, Version, IsPublished
- **Invarianti**:
  - Name univoco per versione
  - Solo una versione pubblicata per template

### NotificationPreferences Aggregate
- **Identita**: PreferencesId (GUID)
- **Proprieta**: UserId, canali abilitati/disabilitati per tipo notifica

## Dipendenze

- **EF Core**: Persistence (PostgreSQL)
- **MediatR**: CQRS orchestration
- **FluentValidation**: Input validation per tutti i comandi
- **Slack.NetStandard / Slack Web API**: Comunicazione con Slack
- **Web Push (VAPID)**: Push notifications browser
- **HMAC-SHA256**: Token unsubscribe sicuri

## Dipendenze Cross-BC (Event Handlers)

Questo BC riceve domain events da:
- **GameManagement**: Game night (pubblicazione, cancellazione, RSVP, promemoria)
- **Gamification**: Badge guadagnati, milestone
- **DocumentProcessing**: Stato elaborazione PDF, vector document pronto
- **KnowledgeBase**: Agent collegato, agent pronto
- **SharedGameCatalog**: Richieste condivisione (creazione, review, approvazione, rifiuto)
- **Administration**: Sospensione/riattivazione utente, rate limit

## Testing

- Unit tests per domain logic, invarianti e retry policy
- Integration tests con Testcontainers (PostgreSQL)
- Test coverage: >90%

## Related Documentation

- `docs/01-architecture/overview/system-architecture.md`
- `docs/03-api/board-game-ai-api-specification.md`
