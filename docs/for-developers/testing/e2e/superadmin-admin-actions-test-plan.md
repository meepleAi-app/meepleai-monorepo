# SuperAdmin — Test Plan E2E delle azioni via interfaccia web

> **Tipo**: Specifica di test E2E (deliverable `/sc:spec-panel`)
> **Scope**: Tutte le azioni raggiungibili in area `/admin` da un utente con ruolo **SuperAdmin** (superset di Admin).
> **Strategia azioni rischiose**: **dry-run** — le azioni distruttive/outward-facing NON vengono confermate; si verifica solo che il controllo esista, sia abilitato per il ruolo e raggiungibile.
> **Stato**: v1.1 — revisionato dal panel di esperti (Crispin · Gregory · Wiegers · Nygard · Adzic · Hightower).
> **Data**: 2026-07-29

---

## 1. Scopo e contesto

Verificare, tramite l'interfaccia web, che **tutte** le azioni disponibili a un SuperAdmin siano:
1. **Raggiungibili** (route protetta accessibile, navigazione presente, controllo renderizzato);
2. **Abilitate** correttamente per il ruolo (in particolare le azioni *esclusive* SuperAdmin non gated erroneamente, e non nascoste per bug di ruolo);
3. **Funzionalmente corrette** per le azioni sicure (read/reversibili), fino allo stato osservabile atteso;
4. **Presenti ma non eseguite** per le azioni distruttive (protocollo dry-run).

Questo piano è **UI/behaviour-driven** (osservabile dal browser), non un test di unità/integrazione. Fa da mappa di copertura E2E e da gap analysis rispetto alla suite Playwright esistente in `apps/web/e2e/`.

### 1.1 Fuori scope
- Esecuzione reale di azioni distruttive/outward-facing (vedi §4).
- Logica interna backend non osservabile da UI (coperta da xUnit).
- Flussi non-admin (utente/editor/creator), tranne dove servono per i test negativi di autorizzazione (§8).
- Verifica pixel/visual regression (rimossa dal repo il 2026-05-20).

---

## 2. Modello di autorizzazione (riferimento)

Fonte: `apps/api/src/Api` + `apps/web/src/types/auth.ts`.

| Ruolo | Enum `UserRole` | Livello sessione | Note |
|---|---|---|---|
| Admin | `Admin = 0` | 3 | Operations, monitoring, user mgmt — **niente** feature flag globali / azioni critiche |
| Editor | `Editor = 1` | 2 | |
| User | `User = 2` | 0 | |
| SuperAdmin | `SuperAdmin = 3` | 4 | Accesso completo; gestisce altri admin e flag globali; ruolo **immutabile, solo da seed** |
| Creator | `Creator = 4` | 1 | |

**Gerarchia**: SuperAdmin ⊇ Admin ⊇ Editor ⊇ User (per policy `RequireRole("SuperAdmin","Admin",...)`, `HasSufficientRole`, `Role.HasPermission`, `IsAdmin()`).

**Insidia nota** (rilevante per §8): il claim di ruolo è **singolo valore PascalCase**, quindi `IsInRole("Admin")` **nudo** restituisce `false` per un SuperAdmin. L'helper corretto è `ClaimsPrincipalExtensions.IsAdmin()` (`Admin || SuperAdmin`). Residuo latente in `SharedGameCatalogWizardEndpoints.cs:331,443,497`.

### 2.1 Azioni *esclusive* SuperAdmin (non disponibili ad Admin)

| # | Capacità | Endpoint / superficie UI | Classe rischio |
|---|---|---|---|
| E1 | Impersonation utente (start/revoke/active) | `/admin/*` → `POST /admin/impersonation/*`, `/admin/users/{id}/impersonate` | **D** |
| E2 | Provider — probe connettività | `/admin/providers/[name]` → "Run probe" | R (SA) |
| E3 | Provider — rotate API key | `/admin/providers` → "Rotate key" (+ 2FA step-up) | **D** |
| E4 | Restart servizio API / restart-all | `/admin/monitor/services`, `/admin/monitor/containers` | **D** |
| E5 | AI Infrastructure — restart / health-check / config | `/admin/agents/infrastructure` | **D** (config/restart), R (health-check) |
| E6 | Database Sync (schema/data/tunnel) | `/admin/database-sync` | **D** (apply/sync/tunnel), R (compare/preview/history) |
| E7 | Staging allowlist — add/remove email | `/admin/staging-access` | RW (SA) |
| E8 | Cambio ruolo utente / bulk role-change | `/admin/users`, `/admin/users/[id]` | RW (sensibile) |
| E9 | Bulk password reset | (superficie utenti / bulk) | **D** (invia email) |
| E10 | Lista account Admin/SuperAdmin senza 2FA | (compliance) | R (SA) |

---

## 3. Precondizioni globali e dati di test

| ID | Precondizione |
|---|---|
| PRE-1 | Ambiente in esecuzione (`make dev` da `infra/`) — API `:8080`, Web `:3000`, Postgres/Redis up |
| PRE-2 | Account SuperAdmin seedato: `badsworm@gmail.com` (seed `SeedBadswormUserCommandHandler`) con 2FA configurata (necessaria per E3 rotate-key) |
| PRE-3 | Almeno 1 utente Admin, 1 Editor, 1 User seedati (per test cambio ruolo, filtri, impersonation, test negativi) |
| PRE-4 | Almeno 1 gioco condiviso + 1 PDF indicizzato (per aree KB/RAG/shared-games) |
| PRE-5 | `view_mode` = admin (non `user`) — vedi SHELL-04 |
| PRE-6 | Feature flag `Features.DatabaseSync` **abilitato** (altrimenti `/admin/database-sync` non attivo) |
| PRE-7 | Provider AI configurato con almeno 1 chiave (per probe/rotate/quota) |
| PRE-8 ⚠️ | **Database Sync tocca STAGING**: qualsiasi azione DBS (anche `compare`/`preview`) apre un tunnel SSH verso lo staging reale (chiave `~/.ssh/meepleai-staging`). Eseguire i test DBS **solo** con autorizzazione esplicita e verso un ambiente designato non-produttivo. In assenza, l'intera area §6.14 DBS resta **dry-run/skip**. |

**Nota dati distruttivi**: i test dry-run (§4) non richiedono dati "sacrificabili"; i test reversibili (RW) devono usare entità di test dedicate e ripristinare lo stato in teardown.

### 3.1 Dataset canonico di test (esempi concreti — Adzic)

Per rendere i test eseguibili e non ambigui, usare questi valori di riferimento:

| Riferimento | Valore |
|---|---|
| SuperAdmin | `badsworm@gmail.com` |
| Utente target ruolo/impersonation | `editor-qa@example.com` (ruolo Editor) |
| Email allowlist staging | `qa+staging@example.com` |
| Provider per probe/rotate | `openrouter` (o il primo provider seedato) |
| Servizio per restart | `embedding-service` |
| Gioco/PDF di test | gioco seed con ≥1 PDF `Ready` |

### 3.2 Requisito trasversale: AUDIT TRAIL (compliance — Nygard)

**Ogni** azione SuperAdmin sensibile (cambio ruolo, rotate-key, restart, impersonation start/revoke, staging allowlist add/remove, bulk role-change, feature-flag globali) **deve** generare un record di audit **osservabile via UI**:
- Cambio ruolo → riga in **Role History** (`/admin/users/[id]`) + Audit Log.
- Altre azioni critiche → **Audit tab** (`/admin/analytics?tab=audit`) o Config history (`ConfigAuditLogDialog`).

**Regola**: per le azioni **RW** verificare la riga di audit *dopo* l'azione (parte dei criteri di uscita). Per le azioni **D** in dry-run l'audit non viene generato (azione non confermata) — annotare che la verifica dell'audit di quelle azioni resta **scoperta** e va coperta in una fase 2 su ambiente sacrificale.

### 3.3 Strategia anti-flakiness (Crispin)

Aree async da gestire con wait deterministici (non `sleep`): SSE seeding (`SeedLogStream`), processing PDF (stati Embedding→Ready), polling dashboard/monitor. Attendere lo **stato osservabile finale** (es. badge `Ready`, riga comparsa) con timeout esplicito, non un intervallo fisso.

---

## 4. Strategia di rischio e protocollo dry-run

Ogni test case è classificato:

| Classe | Significato | Esecuzione |
|---|---|---|
| **R** | Read-only (navigazione, lettura, filtri, apertura dialog senza submit) | Esegui completamente |
| **RW** | Muta stato ma reversibile con undo verificabile | Esegui + ripristina in teardown |
| **D** | Distruttiva / irreversibile / outward-facing (email, restart, rotate-key, db apply, impersonation) | **DRY-RUN** |

### 4.1 Protocollo DRY-RUN (classe D)
Per ogni azione classe **D** il test verifica, **senza confermare l'azione finale**:
1. Il controllo (bottone/toggle) è **presente** nella pagina per il SuperAdmin;
2. È **abilitato** (non `disabled`, nessun tooltip "Requires SuperAdmin" per il SA);
3. All'attivazione compare il **gate di sicurezza atteso** (dialog di conferma, richiesta di digitare il nome del servizio, prompt 2FA step-up, conferma "Level2");
4. Il test **si ferma al gate**: chiude il dialog / annulla, **non** invia la richiesta finale.
5. (Opz.) Verifica che per un ruolo Admin lo stesso controllo sia assente/disabilitato con tooltip corretto (rafforza il gate SA).

> ⚠️ **Vietato in dry-run**: cliccare "Conferma restart", "Ruota chiave", "Applica schema", "Sync", "Invia notifica", "Reset password", "Avvia impersonazione". La chiave provider ruotata è irreversibile; un restart interrompe l'API; il db-sync apply modifica lo schema; le email partono verso utenti reali.

---

## 5. Convenzioni test case

- **ID**: `SA-<AREA>-<n>`.
- **Ruolo**: `SA` (esclusivo SuperAdmin) o `A` (Admin, ereditato da SA).
- **Rischio**: R / RW / D.
- **Cov.**: file spec E2E esistente *inferito dal nome* (da confermare a livello di codice — vedi §10) o `GAP`.
- Formato dettagliato Given/When/Then solo per i test critici SuperAdmin-exclusive (§7).

---

## 6. Matrice test case per area

### 6.1 Shell / Auth / Navigazione

| ID | Azione | Ruolo | Rischio | Risultato atteso | Cov. |
|---|---|---|---|---|---|
| SA-SHELL-01 | Login come `badsworm` | SA | R | Sessione creata, redirect a landing autenticata | `admin-login-real.spec.ts` |
| SA-SHELL-02 | Accesso a `/admin` → redirect `/admin/overview` | SA | R | Overview renderizzata, `AdminShell` presente | `admin.spec.ts`, `admin/admin-overview.spec.ts` |
| SA-SHELL-03 | Nav admin mostra voci fino a `minRole` SA | SA | R | Voce **Staging Access** (`minRole: superadmin`) visibile | `admin/admin-mobile-nav.spec.ts` (parziale) |
| SA-SHELL-04 | Toggle view-mode admin↔user (`UserMenuDropdown`) | SA | RW | `view_mode=user` → redirect `/`; ripristino admin ok | `admin/admin-dark-scope.spec.ts` (parziale) |
| SA-SHELL-05 | Hub a tab persistono `?tab=` (config/ai/content/analytics) | A | R | Reload mantiene tab (`AdminTabPersistence`) | GAP |

### 6.2 Overview & System Health

| ID | Azione | Ruolo | Rischio | Risultato atteso | Cov. |
|---|---|---|---|---|---|
| SA-OVW-01 | Dashboard KPI overview | A | R | KPI caricati senza errori | `admin/admin-overview.spec.ts`, `admin-dashboard-*.spec.ts` |
| SA-OVW-02 | Activity Log (`/admin/overview/activity`) | A | R | Timeline eventi renderizzata, paginazione | `user-activity.spec.ts` (parziale) |
| SA-OVW-03 | System Health (`/admin/overview/system`) | A | R | Stato componenti/health card | `system-health.spec.ts` |

### 6.3 Gestione utenti

| ID | Azione | Ruolo | Rischio | Risultato atteso | Cov. |
|---|---|---|---|---|---|
| SA-USR-01 | Lista utenti + ricerca (debounce) | A | R | Risultati filtrati per query | `admin-users.spec.ts` |
| SA-USR-02 | Filtro chip per ruolo (all/user/editor/admin/superadmin) | A | R | Lista filtrata; chip `superadmin` presente | `admin-users.spec.ts` |
| SA-USR-03 | Paginazione + "Aggiorna" (refetch) | A | R | Pagina/refetch corretti | `admin-users.spec.ts` |
| SA-USR-04 | Cambio ruolo inline (`InlineRoleSelect`) su utente non-SA | SA | RW | Ruolo aggiornato; ripristino | `admin-user-management-epic3686.spec.ts` |
| SA-USR-05 | Utente `superadmin` mostrato **read-only** (non modificabile) | SA | R | Select ruolo disabilitato/assente per riga SA | `admin-users.spec.ts` (verificare) |
| SA-USR-06 | Dettaglio `/admin/users/[id]` — tab Overview/Role/Activity | A | R | Tab navigabili, dati caricati | `admin/admin-user-detail.spec.ts` |
| SA-USR-07 | Change Role da dettaglio (Select + reason + AlertDialog) | SA | RW | Conferma → ruolo aggiornato, riga in Role History | `admin/admin-user-detail.spec.ts` |
| SA-USR-08 | Role History + Audit Log paginato (espandi dettagli) | A | R | History/audit leggibili | `audit-log.spec.ts` |
| SA-USR-09 | Ruoli & Permessi (`/admin/users/roles`, `PermissionsMatrix`) | A | R | Matrice permessi renderizzata | GAP (verificare) |
| SA-USR-10 | **Bulk role-change** (selezione multipla) | SA | RW→**D-boundary** | Dry-run al gate conferma; verificare limite batch (SA 1000 vs Admin 100) | GAP |
| SA-USR-11 | **Bulk password reset** | SA | **D** | Dry-run: controllo presente/abilitato, stop al gate (invia email) | GAP |

### 6.4 Inviti & Richieste accesso

| ID | Azione | Ruolo | Rischio | Risultato atteso | Cov. |
|---|---|---|---|---|---|
| SA-INV-01 | "Invita Utente" (`InviteUserDialog`) | A | **D** | Dry-run: dialog si apre, submit invia email → stop al submit | `admin/admin-invite-flow.spec.ts`, `admin/invitations.spec.ts` |
| SA-INV-02 | Invito multiplo (`BulkInviteDialog`) | A | **D** | Dry-run al submit | `admin/invitations.spec.ts` |
| SA-INV-03 | Reinvia / Revoca invito (`InvitationRow`) | A | RW/**D** | Revoca RW (stato pending→revoked); reinvia = email (dry-run) | `admin/invitations.spec.ts` |
| SA-INV-04 | Filtri stato + ricerca email + KPI invitations | A | R | Contatori e filtri corretti | `admin/invitations.spec.ts` |
| SA-ACR-01 | Access-requests: KPI + filtro stato | A | R | Lista/contatori | `admin-share-requests.spec.ts` (verificare se copre access-requests) |
| SA-ACR-02 | Approva richiesta (per riga) | A | RW | Stato → approvato; utente abilitato | GAP (verificare) |
| SA-ACR-03 | Rifiuta con motivo (`RejectDialog`) | A | RW | Stato → rifiutato con reason | GAP |
| SA-ACR-04 | Bulk approva selezionati (max 25) | A | RW | Approvazione multipla, rispetto limite 25 | GAP |

### 6.5 Configurazione

| ID | Azione | Ruolo | Rischio | Risultato atteso | Cov. |
|---|---|---|---|---|---|
| SA-CFG-01 | Registration Mode toggle (`config?tab=general`) | A | RW | Switch + Dialog conferma → stato persistito; ripristino | `invite-only-registration.spec.ts`, `admin-configuration.spec.ts` |
| SA-CFG-02 | Feature Flags tab — toggle flag | SA | RW | Flag aggiornato (flag globali = SA); ripristino | `admin/admin-feature-flags.spec.ts`, `tier-feature-flags.spec.ts` |
| SA-CFG-03 | PDF Limits (`config?tab=limits`, `PdfLimitsConfig`) | A | RW | Limiti per tier salvati; ripristino | `admin/pdf-limits-config.spec.ts` |
| SA-CFG-04 | Rate Limits tab (read-only) | A | R | Visualizzazione; nessun editor (atteso) | `admin-configuration.spec.ts` (verificare) |
| SA-CFG-05 | Status Banner admin | A | RW | Banner impostato/rimosso; ripristino | GAP |
| SA-CFG-06 | Config history / undo (`ConfigHistoryDialog`, `DirtyStateBar`) | A | R/RW | History mostrata; undo ripristina | `admin/admin-config-history.spec.ts`, `config-import-export.spec.ts` |
| SA-CFG-07 | Config export / import | A | RW | Export scarica; import applica (usare file di test) | `config-import-export.spec.ts` |
| SA-TIER-01 | Tier Management — "Nuovo Tier" (dialog, 9 limiti) | A | RW | Tier creato; cleanup | `admin/admin-tier-management.spec.ts` |
| SA-TIER-02 | Tier — Modifica (LLM tier, limiti, sessionSave/isDefault) | A | RW | Update persistito; ripristino | `admin/admin-tier-management.spec.ts` |
| SA-CFG-08 | n8n Workflow Integration (`config/n8n`) | A | R/RW | Config n8n visualizzata/salvata | GAP |

### 6.6 Providers (AI)

| ID | Azione | Ruolo | Rischio | Risultato atteso | Cov. |
|---|---|---|---|---|---|
| SA-PRV-01 | Lista provider + toolbar refresh + KPI/quota | A | R | Tabella provider, circuit breaker grid | GAP (verificare) |
| SA-PRV-02 | Config provider (link "Config") | A | R/RW | Pagina config provider | GAP |
| SA-PRV-03 | **Run probe** (`/admin/providers/[name]`) | SA | R (SA) | Probe eseguito (connettività) — esegui; per Admin: controllo assente/tooltip "richiede SuperAdmin" | `admin-providers-rotate-key.spec.ts` (verificare probe) |
| SA-PRV-04 | **Rotate key** (`RotateKeyModal` + 2FA) | SA | **D** | Dry-run: modal apre, richiede 2FA step-up, **stop** prima del submit; disabilitato per Admin | `admin-providers-rotate-key.spec.ts` |

### 6.7 Monitor & Operations

| ID | Azione | Ruolo | Rischio | Risultato atteso | Cov. |
|---|---|---|---|---|---|
| SA-MON-01 | Grafana embed (`monitor/grafana`) | A | R | Iframe/embed caricato | GAP |
| SA-MON-02 | Logs (`monitor/logs`) | A | R | Log stream renderizzato | GAP |
| SA-MON-03 | MAU dashboard (`monitor/mau`) | A | R | Metriche MAU | GAP |
| SA-MON-04 | Operations console (health/metrics/emails) | A | R | Pannelli operations | `admin/operations-console.spec.ts` |
| SA-MON-05 | Service-calls history | A | R | Storico chiamate | `service-status.spec.ts` (verificare) |
| SA-MON-06 | **Restart singolo servizio** (`RestartServicePanel`) | SA | **D** | Dry-run: badge "SuperAdmin", richiede digitare nome servizio, **stop** prima conferma | `service-status.spec.ts` (verificare) |
| SA-MON-07 | **Restart all / container control** (`RestartAllPanel`) | SA | **D** | Dry-run: badge SA, stop al gate | GAP |
| SA-MON-08 | Wikidata dead-letters — retry/gestione | A | RW/**D** | Dry-run su retry se outward; lettura lista | `admin-wikidata-bulk-acknowledge-flow.spec.ts` |
| SA-MON-09 | System alerts / alert rules CRUD (`AlertRuleForm`) | A | RW | Crea/edita/elimina regola di test; cleanup | `admin-alert-config.spec.ts`, `system-alerts.spec.ts` |

### 6.8 Notifiche

| ID | Azione | Ruolo | Rischio | Risultato atteso | Cov. |
|---|---|---|---|---|---|
| SA-NOTIF-01 | Compose: selezione canali (inapp/email) + destinatari (all/role/userIds) | A | R | Selettori funzionanti, anteprima live | `notifications.spec.ts` |
| SA-NOTIF-02 | **Send Notification** | A | **D** | Dry-run: form valido abilita "Send", **stop** prima dell'invio (email/in-app reali) | `notifications.spec.ts` (verificare stop) |

### 6.9 Contenuti / Shared Games

| ID | Azione | Ruolo | Rischio | Risultato atteso | Cov. |
|---|---|---|---|---|---|
| SA-CNT-01 | Content hub tab shared/kb (`/admin/content`) | A | R | Tab navigabili | GAP |
| SA-CNT-02 | Email templates (`content/email-templates`) | A | RW | Template editabile; ripristino | GAP |
| SA-SG-01 | Lista shared games (`/admin/shared-games/all`) | A | R | Griglia giochi | `shared-games-workflow.spec.ts` |
| SA-SG-02 | Add New Game (path BGG) | A | RW | Gioco creato (server-to-server BGG admin-side lecito) | `shared-games-bgg-import.spec.ts`, `admin-game-creation.spec.ts` |
| SA-SG-03 | Import da PDF (wizard) | A | RW | Wizard completa import | `game-import-wizard.spec.ts`, `admin-game-wizard-e2e.spec.ts` |
| SA-SG-04 | Categories | A | RW | CRUD categorie; cleanup | GAP |
| SA-SG-05 | Seeding SSE con BGG | A | RW/**D** | Dry-run se avvia job pesante; lettura stream | `seeding.spec.ts`, `catalog-seed.spec.ts` |
| SA-SG-06 | Catalog Content Wizard | A | RW | Wizard funzionante | `admin-wizard.spec.ts` |
| SA-SG-07 | Approval queue giochi condivisi | A | RW | Approva/rifiuta contenuto di test | `shared-games-approval-queue.spec.ts` |
| SA-SG-08 | Detail `[id]` + KB + RAG Setup | A | RW | Setup RAG per gioco | `shared-games-rag-full-flow.spec.ts`, `shared-games-detail-page.spec.ts` |

### 6.10 Catalog ingestion (BGG)

| ID | Azione | Ruolo | Rischio | Risultato atteso | Cov. |
|---|---|---|---|---|---|
| SA-CAT-01 | `/admin/catalog-ingestion` — Export catalog | A | R | Download export | `admin/catalog-ingestion-reskin.spec.ts` |
| SA-CAT-02 | CSV Import (`CsvImportModal`) | A | RW | Import CSV di test | `catalog-ingestion-reskin.spec.ts` (verificare) |
| SA-CAT-03 | Manual Assign BGG id (`ManualAssignModal`) | A | RW | Assegnazione | GAP |
| SA-CAT-04 | Timeline run + drill-down log (`LogStream`) | A | R | Log per run | GAP |
| SA-CAT-05 | Seed queue (`/admin/catalog/seed-queue`) — single/bulk/wikidata add | A | RW | Voci accodate; cleanup | `admin/catalog-seed.spec.ts` |

### 6.11 Knowledge Base / RAG

| ID | Azione | Ruolo | Rischio | Risultato atteso | Cov. |
|---|---|---|---|---|---|
| SA-KB-01 | Documents library | A | R | Lista documenti | `admin/kb-documents-library.spec.ts` |
| SA-KB-02 | Upload PDF | A | RW | Upload + accodamento processing | `admin/kb-pdf-upload.spec.ts`, `admin-pdf-upload-flow.spec.ts` |
| SA-KB-03 | Doc actions (preview/tab/azioni) | A | RW | Azioni doc | `admin/kb-doc-actions.spec.ts`, `admin/kb-doc-preview-tab.spec.ts` |
| SA-KB-04 | Embeddings viewer / vectors | A | R | Visualizzazione embeddings/vettori | `admin/kb-embeddings-viewer.spec.ts`, `admin/kb-vectors.spec.ts` |
| SA-KB-05 | Pipeline / processing status / queue | A | R/RW | Stato pipeline; trigger su doc di test | `rag-pipeline.spec.ts`, `admin/rag-pipeline.spec.ts` |
| SA-KB-06 | Snapshots | A | R/RW | Lista/gestione snapshot | GAP |
| SA-KB-07 | Feedback KB | A | R | Feedback list | GAP |
| SA-KB-08 | Quality eval trigger (`EvaluationTriggerButton`) | A | RW | Job qualità avviato su set di test | `admin/kb-quality-eval-happy-path.spec.ts`, `admin-analytics-quality.spec.ts` |
| SA-KB-09 | Mechanic-extractor: analyses/dashboard/metrics/golden | A | R/RW | Review golden set, metriche | `admin-mechanic-extractor-validation/*` |

### 6.12 AI / Agents

| ID | Azione | Ruolo | Rischio | Risultato atteso | Cov. |
|---|---|---|---|---|---|
| SA-AI-01 | AI hub tabs (agents/typologies/definitions/lab/prompts/models/requests/rag/config) | A | R | Tab navigabili | GAP (parziale) |
| SA-AGT-01 | Agent Definitions — lista / create / edit | A | RW | CRUD definizione; cleanup | `admin/agent-builder-kb-cards.spec.ts` |
| SA-AGT-02 | Agent Builder (`BuilderClient`) | A | RW | Build agent di test | `rag-strategy-builder.spec.ts` |
| SA-AGT-03 | Config: limits / models / strategy | SA/A | RW | Config salvata (flag/modelli globali → SA) | `rag-strategy-config.spec.ts`, `admin/session-limits-config.spec.ts` |
| SA-AGT-04 | Set primary model (`SetPrimaryModelDialog`) | A | RW | Modello primario impostato; ripristino | GAP |
| SA-AGT-05 | Playground / sandbox / test chat | A | R/RW | Esecuzione chat di test | `rag-debug-console.spec.ts` |
| SA-AGT-06 | Prompts management | A | RW | Prompt editato; ripristino | `admin-prompts-management.spec.ts` |
| SA-AGT-07 | A/B testing (new/[id]/results) | A | RW | Crea test A/B; risultati | `batch-jobs.spec.ts` (verificare) |
| SA-AGT-08 | Usage & Costi / token balance | A | R | Metriche uso/costi | `admin-analytics.spec.ts` (parziale) |
| SA-AGT-09 | **AI Infrastructure** — restart / health-check / config servizi | SA | **D**/R | Dry-run restart/config; health-check eseguibile; per Admin controlli disabilitati "Requires SuperAdmin" | `admin-infrastructure.spec.ts` |

### 6.13 Analytics & Business

| ID | Azione | Ruolo | Rischio | Risultato atteso | Cov. |
|---|---|---|---|---|---|
| SA-ANL-01 | Analytics hub — overview / ai-usage / reports | A | R | Dashboard | `admin-analytics.spec.ts` |
| SA-ANL-02 | Audit tab | A | R | Audit log | `audit-log.spec.ts` |
| SA-ANL-03 | API keys tab (`ApiKeysTab` + filtri) | A | RW | Gestione API keys (crea/revoca chiave di test) | `admin/api-keys-ui-security.spec.ts` |
| SA-ANL-04 | Export usage (`ExportUsageButton`) | A | R | Download report | `admin-bulk-export.spec.ts` |
| SA-BIZ-01 | Business — Budget & Cost | A | R | Dashboard costi | GAP |
| SA-RAGQ-01 | RAG quality dashboard | A | R | Metriche qualità RAG | `admin-analytics-quality.spec.ts` (verificare) |

### 6.14 SuperAdmin-exclusive: Staging, Impersonation, Database Sync

| ID | Azione | Ruolo | Rischio | Risultato atteso | Cov. |
|---|---|---|---|---|---|
| SA-STG-01 | `/admin/staging-access` — lista allowlist | SA | R | Tabella email/note | GAP |
| SA-STG-02 | Add email (form + note) | SA | RW | Voce aggiunta; cleanup remove | GAP |
| SA-STG-03 | Remove (window.confirm) | SA | RW | Voce rimossa | GAP |
| SA-STG-04 | Voce nav visibile solo a SA | SA vs A | R | Nav mostra "Staging Access" a SA, nascosta ad Admin | GAP |
| SA-IMP-01 | Impersonation — start (da `/admin/users/{id}` o operations) | SA | **D** | Dry-run: controllo presente, conferma "Level2" appare, **stop** prima di avviare | GAP (verificare `admin/admin-workflow-actions.spec.ts`) |
| SA-IMP-02 | Impersonation — active list | SA | R | Lista sessioni attive (leggibile) | GAP |
| SA-IMP-03 | Impersonation — revoke (kill-switch altrui) | SA | **D** | Dry-run al gate | GAP |
| SA-DBS-01 | `/admin/database-sync` — tunnel status | SA | R | Stato tunnel (`TunnelStatusBanner`) — solo lettura banner, non apre tunnel | GAP |
| SA-DBS-02 | Tunnel open / close | SA | **D**⚠staging | Dry-run: controllo presente, **stop al gate** (aprirebbe tunnel SSH verso staging) | GAP |
| SA-DBS-03 | Schema compare (tab Schema) | SA | **R⚠staging** | Legge lo schema di **staging** via tunnel → NON innocuo. Eseguire solo se PRE-8 soddisfatta, altrimenti skip | GAP |
| SA-DBS-04 | Schema preview SQL | SA | **R⚠staging** | Come DBS-03; SQL preview → skip se PRE-8 non soddisfatta | GAP |
| SA-DBS-05 | Schema apply | SA | **D** | Dry-run: stop prima di applicare | GAP |
| SA-DBS-06 | Tables compare / sync (tab Data) | SA | **R⚠staging** / **D** | Compare tocca staging (skip se no PRE-8); sync dry-run | GAP |
| SA-DBS-07 | Operations history (tab History) | SA | R | Storico operazioni (lettura locale) | GAP |
| SA-2FA-01 | Lista account Admin/SA senza 2FA | SA | R | Elenco (compliance) — verificare esposizione UI | GAP |

> **⚠️ Legenda rischio DBS**: `⚠staging` = azione che, pur essendo di lettura, apre un tunnel e legge un **ambiente condiviso reale** (staging). Va trattata con la stessa cautela di un'azione D: richiede PRE-8 (autorizzazione + ambiente designato). In assenza → **skip**, non eseguire.

---

## 7. Dettaglio Given/When/Then (test critici SuperAdmin-exclusive)

### SA-PRV-04 — Rotate provider key (DRY-RUN)
```
Given  sono loggato come SuperAdmin (badsworm) con 2FA attiva
  And  sono su /admin/providers con almeno un provider con chiave
When   clicco "Rotate key" sulla riga del provider
Then   si apre RotateKeyModal
  And  viene richiesto lo step-up 2FA
  And  il pulsante di conferma finale è presente e abilitato
DRY-RUN STOP: chiudo il modal / annullo — NON inserisco il codice 2FA né confermo
Regression (ruolo): loggato come Admin, il trigger "Rotate key" è disabilitato
  And  compare il tooltip "Solo superadmin può ruotare le chiavi"
```

### SA-MON-06 — Restart servizio (DRY-RUN)
```
Given  SuperAdmin su /admin/monitor/services
When   apro RestartServicePanel per un servizio
Then   è mostrato il badge "SuperAdmin"
  And  è richiesto di digitare il nome del servizio per abilitare la conferma
DRY-RUN STOP: non digito il nome / non confermo il restart
Regression: come Admin il pannello è assente o disabilitato
```

### SA-DBS-05 — Database schema apply (DRY-RUN)
```
Given  SuperAdmin, feature flag Features.DatabaseSync attivo, su /admin/database-sync tab Schema
When   eseguo "Schema compare" e poi "Preview SQL"
Then   vedo il diff e l'SQL generato (azioni R, eseguibili)
When   procedo verso "Apply"
Then   compare il gate di conferma
DRY-RUN STOP: annullo — NON applico lo schema
Regression: come Admin l'intera route /admin/database-sync ritorna 403/redirect (RequireSuperAdmin)
```

### SA-IMP-01 — Impersonation start (DRY-RUN)
```
Given  SuperAdmin su /admin/users/[id] di un utente target
When   attivo "Impersonate"
Then   compare conferma "Level2"
DRY-RUN STOP: annullo — NON avvio la sessione di impersonazione
Note   se avviata, ripristino richiede "end-impersonation"; in dry-run si evita del tutto
Regression: come Admin il controllo di impersonation SuperAdmin è assente (endpoint RequireSuperAdmin)
```

### SA-NOTIF-02 — Send notification (DRY-RUN)
```
Given  Admin/SuperAdmin su /admin/notifications/compose
When   seleziono canale, destinatari "role: user", inserisco titolo+corpo validi
Then   l'anteprima live è coerente e "Send Notification" si abilita
DRY-RUN STOP: NON clicco Send (invierebbe email/in-app a utenti reali)
```

### SA-STG-02/03 — Staging allowlist add/remove (RW, eseguibile)
```
Given  SuperAdmin su /admin/staging-access
When   aggiungo email di test "qa+staging@example.com" con nota
Then   la riga appare in tabella
When   clicco Remove sulla riga (window.confirm → OK)
Then   la riga scompare (stato ripristinato)
Regression: come Admin la voce nav "Staging Access" non è visibile
```

---

## 8. Test negativi / autorizzazione

| ID | Scenario | Atteso |
|---|---|---|
| SA-NEG-01 | Utente `User` naviga a `/admin/*` | Redirect a `/` (proxy `isAdmin` gate) |
| SA-NEG-02 | Utente `Editor` naviga a `/admin/*` | Redirect a `/` |
| SA-NEG-03 | Admin apre `/admin/database-sync` | 403 / redirect (route `RequireSuperAdmin`) |
| SA-NEG-04 | Admin su `/admin/staging-access` | Voce nav assente; se URL diretto, comportamento gate atteso da definire (route guard è solo `Admin`, distinzione è a livello nav) ⚠️ |
| SA-NEG-05 | Admin vede controlli rotate-key / restart / infra-config | Disabilitati con tooltip "Requires SuperAdmin" |
| SA-NEG-06 | SuperAdmin **non** può auto-declassarsi né essere declassato | Riga SA read-only; nessun ruolo assegnabile = superadmin |
| SA-NEG-07 | Bulk role-change: Admin non può assegnare ruolo ≥ proprio, batch max 100; SA batch max 1000 | Limiti applicati; guardia "almeno 1 SuperAdmin resta" |
| SA-NEG-08 ⚠️ | **Regressione IsInRole nudo** — SuperAdmin invia contenuto via Shared Game *wizard* (`SharedGameCatalogWizardEndpoints`) | Atteso: auto-approvato come Admin. **Rischio noto**: `IsInRole("Admin")` nudo (`:331,443,497`) tratta il SA come non-admin → `requiresApproval=true`. Test progettato per **catturare** questo difetto |

> **SA-NEG-04 / SA-STG-04**: il gating di `/admin/staging-access` è solo di navigazione (`minRole: superadmin` nel nav-config), mentre il guard di route resta `Admin`. Da chiarire con l'owner se un Admin che digita l'URL diretto debba ricevere 403. Attualmente potrebbe raggiungere la pagina anche se il backend degli endpoint allowlist è `RequireSuperAdminSession` (le azioni fallirebbero, ma la pagina si aprirebbe). **Ambiguità di specifica** da risolvere.

---

## 8.1 Error & edge scenarios (Crispin)

Almeno un caso negativo per ogni azione critica — è dove i sistemi admin si rompono davvero.

| ID | Azione | Scenario d'errore | Atteso |
|---|---|---|---|
| SA-ERR-01 | Rotate key (SA-PRV-04) | Codice 2FA errato al step-up | Rotazione rifiutata, chiave invariata, messaggio errore; nessun lockout inatteso |
| SA-ERR-02 | Restart servizio (SA-MON-06) | Nome servizio digitato **errato** | Conferma resta disabilitata; nessun restart |
| SA-ERR-03 | Probe provider (SA-PRV-03) | Provider/servizio non raggiungibile | Esito probe = fail mostrato chiaramente (non crash UI) |
| SA-ERR-04 | Cambio ruolo (SA-USR-07) | Conflitto di concorrenza (xmin) — due modifiche stesso utente | 409/errore gestito con messaggio, non 500 |
| SA-ERR-05 | Invito (SA-INV-01) | Email già registrata / invito già pending | Validazione inline, nessun duplicato |
| SA-ERR-06 | Bulk role-change (SA-USR-10) | Batch che declasserebbe l'ultimo SuperAdmin | Bloccato dalla guardia "almeno 1 SA resta" |
| SA-ERR-07 | Tunnel DB (SA-DBS-02) | Tunnel già aperto / chiave SSH assente | Stato coerente in `TunnelStatusBanner`, errore leggibile |
| SA-ERR-08 | Feature flag globale (SA-CFG-02) | Admin (non SA) tenta toggle flag globale | Controllo negato/assente per Admin |
| SA-ERR-09 | Sessione scaduta durante azione admin | Token/sessione invalidata mid-flow | Redirect a login, azione non eseguita |

## 9. Criteri di uscita (Definition of Done)

- [ ] **Blocker pre-esecuzione risolti** (three-amigos con owner): SA-NEG-04 (URL diretto staging-access), rate-limits read-only, limiti bulk 100/1000. Nessun test parte finché gli "attesi" ambigui non sono confermati.
- [ ] Tutti i test **R/RW** eseguiti e verdi con **oracolo osservabile** (elemento atteso visibile + assenza di `role=alert` d'errore), non semplice "renderizzato".
- [ ] **Audit assertion** (§3.2): per ogni azione RW verificata la riga di audit corrispondente (Role History / Audit tab / Config history).
- [ ] Tutti i test **D** verificati in dry-run: controllo presente + abilitato per SA + gate di sicurezza mostrato + azione **non** confermata.
- [ ] Tutti i test negativi/authz (§8) verdi, incluso SA-NEG-05.
- [ ] Tutti gli **error/edge scenarios** (§8.1) verdi.
- [ ] SA-NEG-08 eseguito: esito documentato (pass = auto-approvato; fail = conferma bug `IsInRole` nudo → apri issue).
- [ ] Area **DBS** (§6.14): se PRE-8 non soddisfatta, i test `⚠staging` sono **skippati** (non falliti) e annotati; nessun tunnel verso staging aperto senza autorizzazione.
- [ ] Ogni `GAP` di §6/§10 o convertito in nuovo spec o registrato come debito di copertura tracciato.
- [ ] Nessuna azione outward-facing (email/restart/rotate/db-apply/impersonation) eseguita realmente.
- [ ] **Copertura audit scoperta** annotata: le azioni D in dry-run non generano audit → verifica audit rimandata a fase 2 su ambiente sacrificale.

---

## 10. Gap analysis di copertura (sintesi)

> ⚠️ La colonna "Cov." di §6 è **inferita dal nome dei file spec** in `apps/web/e2e/`; va confermata leggendo i test (un file può testare l'area senza coprire l'azione specifica). Trattare come ipotesi di copertura, non come garanzia.

**Aree SuperAdmin-exclusive con copertura assente o incerta (priorità alta):**
- **Database Sync** (`SA-DBS-*`) — nessuno spec dedicato individuato → **GAP critico** (azioni D + route SA-only).
- **Staging allowlist** (`SA-STG-*`) — nessuno spec → **GAP**.
- **Impersonation** (`SA-IMP-*`) — copertura incerta (`admin-workflow-actions`, `operations-console`) → **GAP da verificare**.
- **Bulk password reset** (`SA-USR-11`) / **Bulk role-change** (`SA-USR-10`) → **GAP**.
- **Lista no-2FA** (`SA-2FA-01`) → **GAP**.

**Aree Admin con copertura assente/incerta:** access-requests (approva/rifiuta/bulk), status banner, n8n, providers list/config, monitor grafana/logs/mau, business budget, content hub/email-templates, catalog manual-assign, KB snapshots/feedback, set-primary-model.

---

## 11. Appendice — Mapping route → spec E2E (riferimento rapido)

| Route/area | Spec candidati in `apps/web/e2e/` |
|---|---|
| Login/overview | `admin-login-real`, `admin.spec`, `admin/admin-overview`, `admin-dashboard-*` |
| Users/roles/detail | `admin-users`, `admin-user-management-epic3686`, `admin/admin-user-detail`, `audit-log`, `user-activity` |
| Invitations/access | `admin/admin-invite-flow`, `admin/invitations`, `admin-share-requests`, `invite-only-registration` |
| Config/tiers/flags | `admin-configuration`, `config-import-export`, `admin/admin-config-history`, `admin/pdf-limits-config`, `admin/session-limits-config`, `admin/admin-feature-flags`, `tier-feature-flags`, `admin/admin-tier-management` |
| Providers | `admin-providers-rotate-key` |
| Monitor/ops/alerts | `admin/operations-console`, `service-status`, `system-health`, `system-alerts`, `admin-alert-config`, `monitor-events-tab`, `admin-wikidata-bulk-acknowledge-flow` |
| Notifications | `notifications.spec`, `notifications/` |
| Shared games/catalog | `shared-games-*`, `admin-game-*`, `game-import-wizard`, `admin/catalog-ingestion-reskin`, `admin/catalog-seed`, `seeding` |
| KB/RAG/agents | `admin/kb-*`, `rag-*`, `admin-prompts-management`, `batch-jobs`, `admin/agent-builder-kb-cards`, `admin-kb-explorer`, `admin-mechanic-extractor-validation/*` |
| Analytics/API keys | `admin-analytics`, `admin-analytics-quality`, `audit-log`, `admin/api-keys-ui-security`, `admin-bulk-export` |
| Infrastructure | `admin-infrastructure` |
| First-time setup | `admin-first-time-setup/*` |

---

## 12. Changelog review panel (v1 → v1.1)

| Finding | Esperto | Applicato |
|---|---|---|
| DBS compare/preview tocca staging reale | Hightower | Riclassificati `R⚠staging` + PRE-8 + regola skip (§3, §6.14, §9) |
| Audit trail mai asserito | Nygard | Requisito trasversale §3.2 + DoD |
| Criteri non misurabili | Wiegers | Oracolo osservabile in DoD |
| Error/edge scenarios assenti | Crispin | Nuova §8.1 (9 casi) |
| Ambiguità non risolte | Gregory | Blocker pre-esecuzione in DoD |
| Esempi concreti scarsi | Adzic | Dataset canonico §3.1 |
| Flakiness async | Crispin | Strategia wait §3.3 |

---

*v1.1 revisionata. Prossimo passo suggerito: (a) three-amigos con l'owner per sciogliere i blocker §9, (b) prioritizzare i GAP SuperAdmin-exclusive (Database Sync, Staging, Impersonation) come nuovi spec Playwright.*
