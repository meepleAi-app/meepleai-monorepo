# ADMIN_AUDIT.md — Audit codebase vs handoff Admin Console SP5

> **Scope**: audit **read-only** del monorepo MeepleAI per valutare l'integrazione dei 18 mockup admin SP5 (`design_handoff_admin/admin/`). Nessun file di codice è stato modificato.
> **Data**: 2026-05-24 · **Branch**: `main-dev` · **Metodo**: lettura dei 5 doc di handoff + analisi mirata di backend (`apps/api/src/Api/`) e frontend (`apps/web/`).
> **Nota nome file**: il `README.md` chiede `CODEBASE_AUDIT.md`, mentre `QUICK_START.md` chiede `ADMIN_AUDIT.md`. Uso `ADMIN_AUDIT.md` (istruzione esplicita). Incoerenza interna al handoff da sanare.

---

## 0. TL;DR — Verdetto

**Questo NON è un greenfield.** MeepleAI ha già una **console admin estremamente estesa**, più ampia dei 18 mockup SP5:

- **Frontend**: ~90 `page.tsx` sotto `apps/web/src/app/admin/(dashboard)/` con shell admin, navigazione, SSE, ~220 componenti in `apps/web/src/components/admin/**`.
- **Backend**: bounded context `Administration` completo + ~70 file `Routing/Admin*Endpoints.cs`, con **audit log, impersonate, 2FA TOTP, gate RBAC superadmin già implementati**.

Di conseguenza il lavoro reale **non è "costruire da zero"**, ma un **delta mirato**: (a) re-skin/allineamento al look-and-feel SP5, (b) colmare gap specifici (moderation, step-up 2FA, schema audit, alcune route/endpoint), (c) riconciliare divergenze di architettura (ruoli, routing, convenzioni API).

Il handoff sovrastima massicciamente lo sforzo: dei ~38 componenti "nuovi", **~33-36 sono già coperti** da riuso/adattamento; solo **~2-3 sono realmente da creare**.

**Premessa critica al handoff**: il modello a 4 ruoli `user`/`premium`/`admin`/`superadmin` **non corrisponde** al modello reale (`user`/`editor`/`creator`/`admin`/`superadmin`, con `premium` modellato come *tier* di subscription, non come ruolo). Va riconciliato prima di toccare l'RBAC.

---

## 1. Risposte rapide alle domande di handoff

### README.md (5 domande)

| # | Domanda | Risposta sintetica |
|---|---|---|
| 1 | Esiste già una sezione admin? Dove/quali pagine | **Sì, estesa**. `apps/web/src/app/admin/(dashboard)/`: overview, users, ai, agents (~25 route), analytics, config, content, knowledge-base, monitor, catalog-ingestion, shared-games, rag-quality, providers, notifications. |
| 2 | Modello RBAC oggi? Compatibile con user/admin/superadmin? | **Parzialmente**. Esistono `user`/`admin`/`superadmin` (✅) ma anche `editor`/`creator`; **`premium` NON esiste come ruolo** (è `UserTier`). Gate admin+superadmin già attivo. |
| 3 | Esistono `AdminDataTable`, `KPISparkline`, `LiveEventLog`? | `DataTable` ✅ (`ui/data-display/data-table.tsx`, senza pagination/virtualization); KPI cards ✅ (no "Sparkline" dedicato, Recharts disponibile); LiveEventLog 🚧 **in corso F4.1 #1718** (BE outbox `DomainEventLog` esiste post-S1 PR #1532; gap solo SSE broadcast `events/live` admin-scoped; FE component ex-novo, mockup `sp5-admin-monitor.html`). |
| 4 | Quali endpoint admin esistono / mancano? | Vasta copertura sotto `/api/v1/admin/*`. **Mancano**: moderation (0%), editor/versions generico, SSE `events/live` globale, `force-logout`, `flag-hallucination`, `kb/tree`, `idempotency-check`. Dettaglio §5. |
| 5 | Schermata pilota? | **A1 Overview** (P0, già full, basso rischio → valida shell + design-system SP5), poi **A2 Users** come stress-test sicurezza. §10. |

### QUICK_START.md (7 domande)

| # | Domanda | Risposta |
|---|---|---|
| 1 | Sezione `/admin/*` esiste? Lista pagine | Sì — vedi §6 per l'albero completo. |
| 2 | Ruoli: come gestiti, quali esistono | VO `Role` (string) in `apps/api/src/Api/SharedKernel/Domain/ValueObjects/Role.cs` + enum `UserRole` in `apps/api/src/Api/Infrastructure/Entities/Authentication/UserRole.cs`. Valori: `user`, `editor`, `creator`, `admin`, `superadmin`. §2. |
| 3 | Audit log esiste? Schema | **Sì**, tabella `audit_logs`. Schema **insufficiente** vs handoff (manca `impersonated_user_id`, `before_json`, `after_json`, `step_up_token_id`). §3. |
| 4 | Endpoint `/api/admin/*` esistenti? | Sì, ~70 file di routing. Prefisso reale `/api/v1/admin/*`. §5. |
| 5 | Permission middleware/decorator? Path | `apps/api/src/Api/Filters/RequireAdminSessionFilter.cs` + `apps/api/src/Api/Extensions/SessionValidationExtensions.cs` + policy in `apps/api/src/Api/Extensions/AuthenticationServiceExtensions.cs`. §2. |
| 6 | "Danger zone" pattern (delete user…)? Path | Sì — `RequiresConfirmationAttribute` + `ConfirmationLevel.Level2` (BE), `AdminConfirmationDialog` typed-confirm (FE, `apps/web/src/components/ui/admin/admin-confirmation-dialog.tsx`). §3/§7. |
| 7 | SSE/WebSocket admin? | **Sì, maturo**: ≥10 hook SSE FE + SignalR per chat. Manca solo un bus SSE **globale** `events/live` (oggi SSE per-feature). §8. |

---

## 2. RBAC, ruoli e gate di autorizzazione

### Stato attuale

- **Doppia rappresentazione del ruolo** (da riconciliare):
  - Value Object `Role` (stringa) — `apps/api/src/Api/SharedKernel/Domain/ValueObjects/Role.cs`. `ValidRoles = { user, editor, creator, admin, superadmin }`. Usato dall'aggregate `User` (`apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs`).
  - Enum `UserRole` — `apps/api/src/Api/Infrastructure/Entities/Authentication/UserRole.cs` (`Admin=0, Editor=1, User=2, SuperAdmin=3, Creator=4`). Usato dal gate RBAC.
- **`superadmin` già completo e corretto**: seeded, non assegnabile a runtime (`User.AssignRole` lo vieta), immutabile, `IsSuperAdmin()`. Semantica Aaron/cofounder già rispettata.
- **`premium` NON esiste come ruolo**. Esiste `UserTier` (`User.Tier`, default `Free`) come concetto separato di subscription. **Il handoff confonde ruolo e tier.**
- **Gate admin/superadmin già distinti** (riusabili as-is):
  - Imperativo (per-endpoint): `RequireAdminSession()`, `RequireSuperAdminSession()` (Issue #3696), `RequireAdminOrEditorSession()` in `apps/api/src/Api/Extensions/SessionValidationExtensions.cs`. Gerarchia `SuperAdmin(4) > Admin(3) > Editor(2) > Creator(1) > User(0)`.
  - Dichiarativo (policy): `RequireSuperAdmin`, `RequireAdminOrAbove`, `RequireEditorOrAbove` in `apps/api/src/Api/Extensions/AuthenticationServiceExtensions.cs:44-57`, usate con `.RequireAuthorization(...)`.
  - Filter: `apps/api/src/Api/Filters/RequireAdminSessionFilter.cs`.
- **Sessioni**: **cookie + session table server-side (NO JWT)**. Token hashato in DB, lifetime default **30 giorni**. Middleware `apps/api/src/Api/Middleware/SessionAuthenticationMiddleware.cs`; entity `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/Session.cs`. Sessione validata propagata in `HttpContext.Items[SessionStatusDto]`.

### Gap RBAC

| Aspetto | Handoff richiede | Realtà | Azione |
|---|---|---|---|
| Ruolo `premium` | 4° ruolo | Non esiste (è `UserTier`) | **Decisione**: mappare `premium` → `UserTier.Premium` (consigliato) o aggiungere ruolo. |
| Ruoli extra | solo 4 | `editor`+`creator` presenti | Mantenere; la matrice RBAC SP5 va estesa a 5 ruoli. |
| Doppio tipo ruolo | — | VO `Role` + enum `UserRole` | Riconciliare/documentare; non introdurre un terzo. |
| Gate superadmin | nuovo | **Già esiste** | Riusare `RequireSuperAdminSession()` + policy. |
| Doppio meccanismo gate | — | imperativo vs policy | Standardizzare per i nuovi endpoint admin. |

---

## 3. Audit log

### Stato attuale

- **Tabella `audit_logs`** — entity `apps/api/src/Api/Infrastructure/Entities/Administration/AuditLogEntity.cs`, config `…/Infrastructure/EntityConfigurations/Administration/AuditLogEntityConfiguration.cs`, dominio `apps/api/src/Api/BoundedContexts/Administration/Domain/Entities/AuditLog.cs` (immutabile).
- **Scrittura su due strade**:
  1. **Automatica** via MediatR `AuditLoggingBehavior` (`…/Administration/Application/Behaviors/AuditLoggingBehavior.cs`), opt-in per comando con attributo `[AuditableAction]` — applicato a **soli ~8 comandi** (Suspend/ChangeRole/Delete/UpdateTier/UpdateAiConsent/ExportData/DeleteLlmData/DeleteOwnAccount).
  2. **Manuale** nei handler (es. impersonate). Repo `…/Administration/Infrastructure/Persistence/AuditLogRepository.cs` (Update/Delete lanciano eccezione → immutabilità).
- **Read endpoints**: `apps/api/src/Api/Routing/AdminAuditLogEndpoints.cs` (`GET /admin/audit-log`, `…/export`, `GET /admin/users/{id}/audit-log`).
- ❌ **Nessun middleware globale** "logga ogni mutazione `/admin/*`". La copertura è frammentaria.

### Schema attuale vs richiesto

| Campo handoff | Colonna reale | Stato |
|---|---|---|
| `actor_id` | `UserId` | ✅ (rinominare semanticamente) |
| `action` | `Action` (maxlen 64) | ✅ |
| `target_type` | `Resource` (maxlen 128) | ✅ |
| `target_id` | `ResourceId` | ✅ |
| `ip` | `IpAddress` | ✅ |
| `user_agent` | `UserAgent` | ✅ |
| `created_at` | `CreatedAt` (indexed) | ✅ |
| `impersonated_user_id` | — | ❌ manca |
| `before_json` | — (solo `Details` blob, **max 1024 char**) | ❌ manca |
| `after_json` | — | ❌ manca |
| `step_up_token_id` | — | ❌ manca |

### Gap audit

1. **Estendere lo schema**: aggiungere `impersonated_user_id`, `before_json`, `after_json`, `step_up_token_id`; allargare/sostituire `Details` (1024 char insufficiente per before+after).
2. **Cattura before/after**: oggi il behavior serializza solo le proprietà del *comando*, non lo stato dell'entità. Serve catturare pre/post nei handler.
3. **Sistematizzare**: estendere `[AuditableAction]` a tutti gli handler admin (o un wrapper più generale) per soddisfare il requisito "ogni mutazione admin logga".
4. ⚠️ **Rischio — doppia entità AuditLog**: esiste anche `apps/api/src/Api/BoundedContexts/SecurityAudit/Infrastructure/Entities/AuditLogEntity.cs` con config propria. **Chiarire quale è canonica** (`Administration` è quella effettivamente usata) prima di estendere lo schema, per non costruire sul ramo morto.

---

## 4. Impersonate e 2FA / step-up

### Impersonate — esiste, ma non conforme al token model

- **Esiste** end-to-end: `ImpersonateUserCommandHandler` (`…/Administration/Application/Commands/`), endpoint `POST /admin/users/{userId}/impersonate` (`apps/api/src/Api/Routing/Admin/AdminUserActivityDetailEndpoints.cs:152`, SuperAdmin-gated + `RequiresConfirmationAttribute`), end via `POST /admin/impersonation/end`. Guard sui target privilegiati (no impersonate di admin/superadmin), audit start/end doppio.
- ❌ **Gap**: usa **session cookie a 30 giorni** invece di JWT/token **15 min**; **nessun claim `actor`** (l'admin originale vive solo in audit, non nel token) → manca data-source affidabile per il **banner persistente**.
- **Raccomandazione**: estendere la `Session` entity con `ImpersonatedByUserId` + `ImpersonatedUntil` + lifetime override 15 min (più coerente dell'introdurre JWT), sfruttando `SessionStatusDto` già propagato al FE per il banner.

### 2FA / Step-up — TOTP esiste, step-up NO

- **2FA TOTP completo**: enrollment (`GenerateTotpSetupCommand`), anti-replay (`UsedTotpCodeEntity`), backup codes; su `User` (`IsTwoFactorEnabled`, `TotpSecret`). Tipo TOTP (no SMS).
- ❌ **Step-up assente**: `TwoFactorEnforcementBehavior` (`…/Authentication/Application/Behaviors/`) è in **shadow mode** — logga e **prosegue, non blocca**. `MaxAgeMinutes=30` (handoff chiede 5). **Nessun header `X-StepUp-Token`** (0 occorrenze in `apps/api/src`). Manca `LastTotpVerifiedAt` sulla `Session` (esplicitamente previsto nei commenti del behavior).
- **Riusabile**: tutta l'infra TOTP; `RequireTwoFactorAttribute` (già su Impersonate/Delete/Suspend/ChangeRole); `ConfirmationLevel.Level2` come base del typed-confirm UI.
- **Gap per lo step-up del handoff**: (1) `LastTotpVerifiedAt` su Session; (2) endpoint challenge che emette `step_up_token` (→ `audit.step_up_token_id`); (3) header `X-StepUp-Token` + validazione; (4) behavior shadow→strict (403 `STEP_UP_REQUIRED`, MaxAge 30→5); (5) coprire trigger mancanti (rotate API key, emergency shutdown, mass delete>5, change flag prod, promote→superadmin).

---

## 5. Endpoint backend — copertura per gruppo

> Prefisso globale reale **`/api/v1`** (`apps/api/src/Api/Program.cs:700`): un path di design `/api/admin/x` corrisponde a `/api/v1/admin/x`. ⚠️ Esiste **duplicazione** dei file user (`Routing/AdminUser*.cs` legacy vs `Routing/Admin/AdminUser*.cs` — registrati questi ultimi).

| Gruppo | Copertura | Gap principali |
|---|---|---|
| Overview/monitor/health | ~55% | manca **SSE `events/live` globale**; `metrics` frammentato per dominio (no `?range=`); health non sotto `/admin` |
| Users | ~85% | manca **`force-logout`** (esiste `unlock`≠) e **`GET .../sessions`**; PATCH→**PUT**; paginazione **page/limit** (non cursor) |
| **Content moderation** | **0%** | **gruppo interamente assente** (queue/approve/reject/delete-comment/mute). Nessun aggregate riusabile |
| AI/RAG | ~70% | manca **`flag-hallucination`** e **`queries?sort=worst`**; path `agents/*` invece di `ai/*`; drill via `rag-executions/{id}/replay` (SSE) |
| KB | ~75% | manca **`kb/tree`**, **`docs/{id}/chunks`**, **`idempotency-check`**; upload OK ma progress SSE su endpoint separato (`/admin/queue/{jobId}/stream`) |
| Catalog | ~90% | coperto (BGG queue + ingestion); "runs" storici non come da design |
| Config/Flags | ~80% | path `feature-flags`; PATCH→PUT/toggle; no `apply` esplicito (modifiche inline) |
| Notifications | ~70% | solo canale email-template; `test-send` reale incerto (esiste `preview`); broadcast = `POST /admin/notifications/send` |
| **Editor/Versions** | ~25% | **nessun editor/versioning generico**; frammentato per dominio (email-template, config history/rollback, RuleSpec) |
| Pipeline/n8n | ~70% | n8n configs+templates+test OK; **no `rotate` key dedicato**; webhooks = error-log workflow |
| Upload avanzato | ~80% | bulk+queue OK; `upload/history` solo come `recently-processed` |
| Play records | ~80% | path **`/play-records`** (non `/me/...`); **manca `DELETE`** |
| Private games | ~75% | path **`/private-games`** (non `/me/...`); **manca `publish-checklist`**; submit-for-review solo via "propose-to-catalog"/toolkit |

**Gap backend più critici**: (1) Content Moderation 100% assente; (2) bus SSE admin globale `events/live`; (3) Editor/Versions generico; (4) Users `force-logout`+`sessions`; (5) AI `flag-hallucination`; (6) personal endpoints su `/play-records` e `/private-games` (non `/me`), con `DELETE`/`publish-checklist` mancanti.

**Aree admin EXTRA non previste dai 18 mockup** (il backend è più ricco del design): Mechanic Extractor, LLM/Provider ops (OpenRouter, circuit-breaker, emergency), Docker/Infra ops, Slack, business stats, secrets, database-sync, staging-allowlist, A/B test, sandbox, RAG backup.

---

## 6. Frontend — 18 schermate SP5 vs realtà

### Shell e navigazione (diverge dal design)

- Layout: `apps/web/src/app/admin/(dashboard)/layout.tsx` → guard (`view_mode` cookie + `RequireRole ['Admin']`) → `AdminShell`.
- `AdminShell` (`apps/web/src/components/layout/AdminShell/AdminShell.tsx`) = **`TopBar` (riusata da UserShell, `adminMode`) + `AdminSideDrawer` off-canvas (overlay 280px) + SearchOverlay**. **NON** è una sidebar persistente + topbar fissa come implica SP5.
- Navigazione (`AdminSideDrawer.tsx`): ~20 voci curate, ma esistono ~90 page.tsx admin → gran parte delle route non è in nav primaria.

### Mappa schermate

| ID | Funzione | Stato | Route reale / file |
|---|---|---|---|
| A1 Overview | KPI+activity+alerts+charts | ✅ | `admin/(dashboard)/overview/page.tsx` (+`activity/`, `system/`) |
| A2 Users | lista+drill+impersonate+suspend+delete | 🟡 | `users/page.tsx` + `users/[id]/page.tsx` (impersonate endpoint BE ✅; wiring FE delete/impersonate **da verificare**) |
| A3 Content moderation | queue approve/reject | 🟡 | `content/page.tsx` è gestione giochi/KB, **non** moderation queue |
| A4 AI/RAG quality | metrics + query drill-down | ✅ | `rag-quality/page.tsx` + `agents/inspector/page.tsx` + `ai?tab=rag` |
| A5 KB management | tree + viewer + chunk table | ✅ | `knowledge-base/page.tsx` (hub) + `documents/`, `vectors/` |
| A5b KB upload flow | FSM 5 stati + SSE | ✅ | `knowledge-base/upload/page.tsx` |
| A6 Catalog ingestion | runs + run-now | ✅ | `catalog-ingestion/page.tsx` |
| A7 Config/Flags | rows + dirty bar + apply | ✅ | `config/page.tsx?tab=flags` (dirty-bar **da verificare**) |
| A8 Monitor | metrics + LiveEventLog SSE | 🚧 in corso #1718 | `monitor/page.tsx` (12 tab consolidati #5040/#5053; **NB:** `LogsTab` = Loki app logs, NON LiveEventLog. F4.1 aggiunge 13° tab "events" su `DomainEventLog` outbox + SSE). |
| A9 Notifications | template list + editor + test-send | 🟡 | solo `notifications/compose/page.tsx`; template in `content/email-templates` (scollegato) |
| B1 Editor | block editor + version history | 🟡 | `(authenticated)/editor/page.tsx` — **fuori da /admin**, `?gameId=` non `[type]/[id]` |
| B2 Pipeline builder | canvas + nodes | 🟡 | `(authenticated)/pipeline-builder/page.tsx` — fuori da /admin, no `[id]` |
| B3 n8n | workflows + webhook log + keys | 🟡 | `(authenticated)/n8n/` + `admin/config/n8n/`; copre keys/config, **no** workflow/webhook log |
| B4 Upload avanzato | bulk + queue + history | 🟡 | `(authenticated)/upload/page.tsx` — fuori da /admin; overlap con A5b |
| B5 Play records | personal/cross-user | 🟡 | `(authenticated)/play-records/new` + `[id]/edit` — **manca index** |
| B6 Versions | timeline + diff + publish | 🟡 | `(authenticated)/versions/page.tsx` — fuori da /admin, `?gameId=` non `[artifact]` |
| B7 Private games | publish checklist | 🟡 | `(authenticated)/private-games/[id]` — manca index + checklist |
| B8 Dev tools | scenario/auth-switcher/store-inspector | ❌ | solo `(public)/dev/meeple-card` (showcase) — concetto SP5 assente |

**Copertura FE**: ✅ 7 full · 🟡 10 parziali · ❌ 1 mancante.

### Pattern di routing (3 coesistono, con duplicazione di IA)

1. **Hub `?tab=`** (server component): `ai` (9 tab), `monitor` (12 tab), `config` (5 tab), `content` (2 tab), `analytics`.
2. **Tabs in-page** (shadcn, stato locale): `catalog-ingestion`.
3. **Route separate vere**: `overview`, `users`, `users/[id]`, `rag-quality`, `knowledge-base/*`, `agents/inspector`.

⚠️ **Duplicazione IA**: AI/RAG/KB raggiungibili da 2-3 path (`/admin/ai?tab=rag` ≈ `/admin/agents/inspector` ≈ `/admin/rag-quality`; `/admin/content?tab=kb` ≈ `/admin/knowledge-base`). Il design SP5 assume route-separate 1:1.

---

## 7. Componenti — riuso vs nuovi

**~33-36 dei 38 componenti SP5 già coperti.** Solo **~2-3 realmente nuovi**.

Riuso diretto/quasi-diretto (✅): AdminShell, AdminTopbar (`TopBar`), BulkActionsBar (`BulkActionBar`), AdminKPICard (`KPICard`/`KPIStatCard`), AdminPanel (`Card`), AdminTabs (`Tabs`/`AdminHubTabBar`), RoleChip (`UserRoleBadge`, 5 ruoli), StatusChip (`Badge`+status-badge), Admin{Input,Select,Toggle,Textarea} (primitives), AdminFormRow (`Form`/`settings-row`), RetrievalChunkList (`RetrievedChunkCard`), LatencyBreakdownBar (`WaterfallChart`), DocumentViewer (`PdfViewerModal`), ProcessingTimeline (`PdfStatusTimeline`), ConfirmModal (`AdminConfirmationDialog`), AuditLogTimeline (`AuditTab`/`UserActivityTimeline`), VersionHistory/Timeline, VersionDiffViewer (`components/diff/`), FlowCanvas/NodePalette/NodeConfigPanel (ReactFlow `@xyflow/react`, **2 impl. esistenti**), TemplateEditor/BlockEditor (Tiptap `RichTextEditor`), FlagRow (`FeatureFlagsTab`).

Adattamento/estensione (🟡): AdminSidebar (drawer→sidebar persistente, **se richiesta**), AdminDataTable (+pagination +virtualization), KPISparkline (wrapper Recharts), EnvPill, StatusDot, QueryDrillDown, ChunkTable, IngestionLog, SyncStatusHero, DirtyStateBar, LiveEventLog (UI + SSE — tracciato F4.1 #1718, mockup `admin/sp5-admin-monitor.html`), TimeRangePicker (`DateRangePicker`+presets), PreviewFrame, PublishChecklist (`setup-checklist`), ScenarioPicker/StoreInspector, DangerZoneBox.

Da creare ex-novo (❌, ~2-3): **KBTree** (tree-view generico file/cartelle), **MobileFallback** "desktop-only gate" (se richiesto come componente), eventuale **density system** admin-wide.

**Collocazione (rispettare il freeze de-versioning — NO `components/v2/`)**:
- Primitive trasversali → `apps/web/src/components/ui/<primitive>/` (es. `ui/status-dot/`, `ui/sparkline/`; esiste già `ui/admin/`).
- Composizioni admin → `apps/web/src/components/admin/<feature>/` (home dei ~220 componenti admin esistenti).
- ❌ vietato ricreare `components/admin/v2/`, `components/v2/**`, `components/ui/v2/**`.

**Note tecniche**:
- `AdminConfirmationDialog` Level2 chiede di digitare letteralmente `CONFIRM` (hardcoded) → parametrizzare per il "type the resource name" del handoff.
- Diversi componenti admin hanno `eslint-disable local/no-hardcoded-color-utility` (debito DS-15). I nuovi SP5 devono usare i token canonici (`bg-card`, `text-foreground`, `bg-entity-*`).
- `FlowCanvas`: 2 impl. ReactFlow esistenti — generalizzarne una, non crearne una terza.

---

## 8. SSE/realtime, virtualization, design tokens

- **SSE FE maturo** (niente da creare per A8/A5b): pattern di riferimento `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/hooks/use-queue-sse.ts` (EventSource `withCredentials`, named events, backoff esponenziale, stato tipizzato). Indicator riusabile: `…/queue/components/sse-connection-indicator.tsx`. Hook per-job: `use-job-sse.ts` (→ A5b). Chat RAG via SignalR (`@microsoft/signalr`) + `apps/web/src/lib/agent/sse-handler.ts`. **Gap = solo il bus BE globale `events/live`** (vedi §5).
- **Virtualization**: `react-window ^2.2.7` ✅ (NON `@tanstack/react-virtual`). Precedente: `apps/web/src/components/pdf/PdfTableRow.tsx`. Il `DataTable` generico **non** è virtualizzato → estendere per il requisito SP5 ">200 righe".
- **Design tokens**: sistema canonicalizzato (semantic `bg-background`/`bg-card`/`text-foreground`/`border-border`, entity `bg-entity-*`), tema `[data-theme="light|dark"]` (**default light**, mockup cream). ESLint `local/no-hardcoded-color-utility` (error). Il handoff chiede **density alta + dark default** per `/admin/*`: configurabile via scope `[data-theme]` + utility, ma **diverge dal default light** del prodotto → decisione di prodotto.
- **Mobile/density**: shell già responsive ma **nessun** `MobileFallback` "desktop-only" né density system admin-wide (da creare se richiesti).

---

## 9. Rischi e decisioni aperte

| # | Tema | Decisione richiesta |
|---|---|---|
| R1 | `premium` ruolo vs `UserTier` | Mappare a tier (consigliato) o aggiungere ruolo? Impatta tutta la matrice RBAC SP5. |
| R2 | 5 ruoli reali vs 4 del handoff | Estendere matrice RBAC a `editor`/`creator`. |
| R3 | Doppia entità `AuditLog` (Administration vs SecurityAudit) | Stabilire quale è canonica **prima** di estendere lo schema. |
| R4 | Impersonate: session 30gg vs token 15min + claim actor | Estendere `Session` (consigliato) o introdurre JWT. |
| R5 | Step-up shadow→strict | Trasformare il behavior, aggiungere `LastTotpVerifiedAt`, header, endpoint challenge. |
| R6 | Shell drawer off-canvas vs sidebar persistente SP5 | Re-skin verso sidebar fissa o mantenere drawer? (impatto UX ampio) |
| R7 | Duplicazione IA (AI/RAG/KB su 2-3 path) | Consolidare prima di re-skin, o il design SP5 moltiplica la confusione. |
| R8 | Convenzioni API divergenti | `/api/v1` (non `/api/admin`), PUT (non PATCH), page/limit (non cursor), `/play-records` & `/private-games` (non `/me`). Allineare contratto FE ai path reali. |
| R9 | Tema dark default per admin vs light di prodotto | Decisione di prodotto. |
| R10 | Sovrastima del handoff (38 "nuovi" componenti) | Ripianificare gli sprint SP5 sul **delta** reale, non sul greenfield. |

---

## 10. Schermata pilota raccomandata

**Pilota: A1 Overview** (`/admin/overview`) — P0, già `✅ full`, basso rischio. Obiettivo del pilota: validare il *workflow di traduzione mock SP5 → componente* e le fondamenta (shell, design-system/token SP5, tree-shaking admin, mobile fallback) **senza** dipendere da nuovi endpoint o RBAC. Allineato a `SCREENS.md` (Sprint 1) e `QUICK_START.md` (step 5).

**Secondo step (stress-test sicurezza): A2 Users** (`/admin/users` + `[id]`) — P0, esercita TUTTI i pattern rischiosi: `AdminDataTable` (sort/multi-select/pagination/virtualization), drill-down, impersonate (con i gap §4), delete con typed-confirm + step-up, audit timeline. È il vero banco di prova prima di scalare agli altri 16.

**Sconsigliato** partire da B1-B8: stanno fuori da `/admin`, hanno gap di routing (path param) e in parte di endpoint; vanno affrontati dopo aver consolidato shell + pattern sicurezza.

---

## Appendice — Convenzioni divergenti (contratto FE)

| Tema | Handoff SP5 | Realtà codebase |
|---|---|---|
| Prefisso API | `/api/admin/*` | `/api/v1/admin/*` |
| Mutazione update | `PATCH` | `PUT` / `POST .../toggle` |
| Paginazione liste | cursor | page/limit (alcune) |
| Endpoint personali | `/api/me/play-records`, `/api/me/private-games` | `/play-records`, `/private-games` |
| Token sessione | JWT short-lived | cookie + session table (hash, 30gg) |
| Ruoli | `user/premium/admin/superadmin` | `user/editor/creator/admin/superadmin` (+ `UserTier`) |
| Nav admin | sidebar+topbar fissi | TopBar + drawer off-canvas |

---

*Audit prodotto in sola lettura. Nessuna modifica al codice. Prossimo passo suggerito: condividere §9 (decisioni aperte) con il design owner prima di avviare il pilota A1.*
