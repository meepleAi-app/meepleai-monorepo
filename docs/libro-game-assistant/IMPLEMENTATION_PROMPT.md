# 🎲 Implementazione MVP Libro Game AI Assistant — Phase 1

## Context

Sto avviando l'implementazione del **MVP Phase 1** del Libro Game AI Assistant per MeepleAI: app companion per giocatori casual italiani che vogliono giocare gamebook in lingua estera (Tainted Grail, ISS Vanguard, Stuffed Fables, Andor Chronicles, 7th Continent).

Il prodotto è già stato progettato attraverso una sessione completa di brainstorming + spec-panel review + ultrathink critical review (chat 2026-05-04 con Claude Opus 4.7). **Tutti i documenti di design sono mergiati su `main-dev`** (PR #698, commit `b9f3fdb25`):

- 📐 **Vision document**: `docs/superpowers/specs/2026-05-04-libro-game-assistant-vision.md` (967 righe)
  - Persona Sara casual italian boardgamer
  - Vincoli architetturali, mapping su BC esistenti, NFR cumulativi (perf, a11y, offline, privacy, GDPR, i18n, AI Narrator roadmap, LLM provider control, infra Hetzner CAX31)
  - Roadmap Phase 1.5/v2/v2.5/v3, risk register 15 entries, competitive moat (Porter)
  - **§6 MVP Phase 1 Scope explicit** (cosa è IN/OUT)
- 📋 **Implementation plan v2** (USA QUESTO): `docs/superpowers/plans/2026-05-04-libro-game-assistant-mvp-phase1-v2.md` (3441 righe)
  - Pre-implementation spike completato (pattern reali codebase verificati via Explore subagent)
  - Phase 0 prerequisites + Phase 1 fully detailed con TDD red→green→refactor steps
  - Phase 2-4 reference list con correction notes per pattern compliance
- 📋 **Implementation plan v1** (riferimento storico, deprecato): `docs/superpowers/plans/2026-05-04-libro-game-assistant-mvp-phase1.md`
  - NON USARE per implementazione — mantenuto per audit history
- 📊 **Feature tracking**: `docs/libro-game-assistant/` (creato durante kickoff implementation)
  - README + IMPLEMENTATION_STATUS + DECISIONS + BLOCKERS

## Setup workspace

Working tree esistente (NON nuovo clone): `D:\Repositories\meepleai-monorepo-dev` su branch `feature/libro-game-mvp-phase1` (parent: `main-dev`).

```bash
# 1. Verifica branch corretto
git branch --show-current  # → feature/libro-game-mvp-phase1
git config branch.feature/libro-game-mvp-phase1.parent  # → main-dev

# 2. Verifica file presenti
ls -la docs/superpowers/specs/2026-05-04-libro-game-assistant-vision.md
ls -la docs/superpowers/plans/2026-05-04-libro-game-assistant-mvp-phase1-v2.md
ls -la docs/libro-game-assistant/

# 3. Setup dev environment standard MeepleAI (se non già fatto)
cd apps/api/src/Api && dotnet restore
cd ../../../web && pnpm install
cd ../../infra && make secrets-setup && make secrets-sync
```

## Documenti da leggere PRIMA di scrivere codice

1. **Vision doc §6 (MVP Phase 1 Scope)** — definisce esattamente cosa è IN/OUT
2. **Plan v2 sezione "Pattern audit reference"** — pattern reali del codebase MeepleAI da rispettare (NON inventare):
   - `ICommandHandler<TCmd,TRes>` / `IQueryHandler<TQ,TRes>` (NON `IRequestHandler` MediatR base)
   - `IBlobStorageService` in `Api.Services.Pdf` namespace (NON `IBlobStorage` invented)
   - `IBackgroundTaskService.ExecuteWithCancellation()` per fire-and-forget + Quartz.NET `IJob` per scheduled (NON Hangfire)
   - UserId passato come parametro in command/query (NO `IUserContext`)
   - RAG composto da `SearchQueryHandler` + `IEmbeddingService` + `ILlmService` + `ISemanticResponseCache` + `IRagValidationPipelineService` (NO `IRagPipeline` monolitico)
   - **NSubstitute** + FluentAssertions per testing (NON Moq)
   - Frontend `httpClient` da `lib/api/core/httpClient.ts` con Zod (NON `apiClient` invented)
   - Aggregate-specific repositories extending `IRepository<TEntity,TKey>` da `Api.SharedKernel.Infrastructure.Persistence`
3. **Plan v2 Phase 0 + Phase 1** — task dettagliate con TDD steps + code snippets verificati
4. **CLAUDE.md** del repo per:
   - 🔴 CQRS rule: endpoints usano SOLO `IMediator.Send()`
   - PR target rule: PR vanno a parent branch (`feature/libro-game-mvp-phase1`), NON direttamente a `main-dev`

## Strategia di esecuzione

Invoca la skill `superpowers:subagent-driven-development` per orchestrare l'esecuzione task-by-task con review tra task.

**Hybrid risk-aware sprint** (decisione kickoff 2026-05-04):

- **Sprint 0 — Foundation code-only** (2 weeks): code-implementable subset di Phase 0 + Phase 1 backend fino a Task 1.4a (smoldocling `/preprocess` endpoint).
- **Aaron procurement parallelo** (weeks 1-4): manuali fisici, contractor IT native speaker, legal advisor, Hetzner/OpenRouter/Stripe accounts.
- **Sprint 1 — Risk gate** (1 week): Task 0.1 step 5-7 (run OCR validation appena manuali arrivano). 🚦 **Hard gate**: se OCR FAIL → STOP, scope review.
- **Sprint 2-3 — Phase 1 completa** (3-4 weeks): Task 1.4b → 1.5 → 1.6 → 1.7 → 1.8 → 1.9.

### Ordine sequenziale Phase 0 (alcune in parallelo — vedi plan v2 "Parallel work opportunities")

| Task                                           | Effort                    | Owner                                              | Tipo      |
| ---------------------------------------------- | ------------------------- | -------------------------------------------------- | --------- |
| 0.1 PR-2 OCR validation                        | 2 weeks                   | Computer Vision Eng + image acquisition contractor | Risk gate |
| 0.2 PR-3 Golden test set                       | 4 weeks lead, 2 effective | ML eng + IT native speaker contractor              | Risk gate |
| 0.3 PR-1 Legal review                          | 2-3 weeks                 | Aaron + legal advisor                              | Risk gate |
| 0.4 Hetzner CAX31 + observability + backup     | 1 week                    | DevOps                                             | Infra     |
| 0.5 OpenRouter setup + cost alerting           | 1 day                     | Backend lead                                       | Infra     |
| 0.6 Stripe account + webhook config            | 1 day                     | Backend lead                                       | Infra     |
| 0.7 SharedGameCatalog integration verification | 2 days                    | Backend lead                                       | Code      |

Solo dopo Phase 0 acceptance gate (OCR pass, legal sign-off, golden set ready, infra deployed) → procedi a **Phase 1 (Tasks 1.1-1.9)** dettagliate nel plan v2 con TDD red→green→refactor steps + code snippets verificati pattern-compliant.

## Governance & constraint

- **CQRS rule** (CLAUDE.md): endpoints usano SOLO `IMediator.Send()` — ZERO direct service injection
- **DDD pattern** (verificato in audit): entities con factory + private setters; value objects immutable; repos in Domain con impl in Infrastructure
- **TDD obbligatorio** per ogni task code-producing: red test → green impl → refactor → commit. Plan v2 Phase 1 mostra esempi.
- **PR target rule** (CLAUDE.md): tutte le PR vanno a `feature/libro-game-mvp-phase1` (parent branch). Solo merge finale phase-by-phase va a `main-dev`.
- **Commit convention**: `feat|fix|docs|refactor|test|chore(scope): description`
- **NON modificare file unrelated**: branch `feature/issue-581-wave-c1-game-detail-fe-v2` (e altri feature branch) hanno lavoro in flight — NON conflittare.
- **Effort honest**: 6-7 mesi calendar (27-28 weeks), 3 fullstack + 1 ML + 1 designer + part-time legal + part-time UX. Non over-promise.
- **Pre-launch prerequisites checklist** (Plan v2 §6.5 / Phase 4 Task 4.4): tutti gli 8 item devono essere green prima di lanciare in production.
- **Secret hygiene**: file `.secret` mai committed (solo `.example` template con placeholder `<your_key>`, NON `sk_test_XXX` style — GitHub secret scanning trigger).

## Dependencies esterne da arrangiare PRIMA di codice

- [ ] Legal advisor identificato (Aaron) — budget €3000-€8000
- [ ] IT native speaker contractor con gamebook expertise — budget ~$3000
- [ ] Hetzner Cloud account + SSH key + Storage Box BX11
- [ ] OpenRouter account + $50 starter credit
- [ ] Stripe account + webhook endpoint configurato
- [ ] Computer Vision Engineer per Task 0.1 (OpenCV expertise required, NON generic ML eng)

## Primo passo concreto

Esegui in questo ordine:

1. Apri `docs/libro-game-assistant/IMPLEMENTATION_STATUS.md` per stato corrente.
2. Leggi vision doc §6 (MVP Phase 1 scope) e Plan v2 "Pattern audit reference".
3. Avvia subagent-driven-development con scope iniziale **Sprint 0 code-only**:
   - Task 0.4 (parziale): bootstrap script + observability/backup config files
   - Task 0.7: SharedGameCatalog integration verification (read-only audit)
   - Task 0.1 step 3: OCR validation Python script artifact
   - Task 0.2 step 2-3: Golden test set JSONL schema + converter
   - Task 1.1: PhotoBatchUpload aggregate root + value objects + events
   - Task 1.2: Repository + EF migration
   - Task 1.3: UploadPhotoBatchCommand + Validator
   - Task 1.4a: IPhotoPreprocessor interface + smoldocling `/preprocess` endpoint
4. Per ogni task: TDD red → green → refactor → commit → PR → code review → merge → next task.
5. Riporta blocker o decision points che richiedono input Aaron.
6. Per task non chiari nei plan v2 Phase 2-4 (compressed reference list), ricompila steps dettagliati basandoti sul pattern di Phase 1 (Tasks 1.1-1.9) come template strutturale.

**Hard gate critico** post-Sprint 0: Task 0.1 step 5-7 (OCR validation con manuali fisici di Aaron). Se OCR confidence è < 0.7 su 3+ manuali, lo scope dell'MVP va rivisto PRIMA di scrivere altro codice Phase 1.
