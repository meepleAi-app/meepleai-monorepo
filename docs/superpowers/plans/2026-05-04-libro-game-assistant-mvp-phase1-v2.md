# Libro Game AI Assistant — MVP Phase 1 Implementation Plan v2

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> ## ⚠️ Plan v2 — Comprehensive rewrite post-ultrathink-review
>
> v1 (commit `0890cd91e`) aveva ~20 code bug, ~10 pattern assumptions inventate, ~12 missing tasks. v2 corregge tutto basato su audit reale del codebase MeepleAI (vedi sezione "Pattern audit reference" sotto).
>
> **Effort revised**: 22-25 weeks calendar (5-6 mesi), NOT 4-5 mesi.

**Goal:** Build MVP Phase 1 (G1+G3+G4 ridotti) del Libro Game AI Assistant per casual italian boardgamer come definito in `docs/superpowers/specs/2026-05-04-libro-game-assistant-vision.md` §6.

**Architecture:** Estensione cross-cutting su BC esistenti MeepleAI seguendo Clean Architecture + DDD. Photo-first ingestion in `DocumentProcessing` BC. Q&A in `KnowledgeBase` BC (riuso `SearchQueryHandler` + `ILlmService`). Nuovo `TranslationService` cross-cutting in `Infrastructure/Translation`. Hetzner CAX31 baseline. Pricing 2-tier (Free 50 pag/mese + Credits €5/100 pag).

**Tech Stack** (pattern verificati da audit):
- **Backend**: .NET 9 + ASP.NET Minimal APIs + MediatR (`ICommandHandler<TCmd,TRes>`, `IQueryHandler<TQ,TRes>`) + EF Core + PostgreSQL 16 + pgvector + Redis (`IDistributedCache`) + Quartz.NET (jobs) + custom `IBackgroundTaskService` (fire-and-forget) + `IBlobStorageService` (`Api.Services.Pdf` namespace) + IHttpClientFactory (named/typed clients)
- **Frontend**: Next.js 16 + React 19 + Tailwind 4 + TanStack Query + custom `HttpClient` class (`lib/api/core/httpClient.ts`) con Zod schema validation + retry + circuit breaker
- **Testing**: xUnit + NSubstitute + FluentAssertions (NO Moq, NO Testcontainers); Vitest + Playwright frontend
- **AI/ML**: smoldocling-service Python (estensione photo-first preprocessor) + embedding-service Python | OpenRouter (LLM abstraction) + Anthropic Claude Sonnet 4.5/Haiku 4.5 + DeepSeek V3
- **Infra**: Hetzner CAX31 (16 GB ARM64) + Storage Box 1 TB + Cloudflare DNS + Caddy reverse proxy
- **Pricing**: Stripe.NET SDK + webhook signature verification + idempotency keys

**Critical path:** Phase 0 prerequisites (5-6 weeks lead) → Phase 1 G1 ingestion (long pole 6 weeks ML) → Phase 2 G3+TranslationService (parallel partial after week 4) → Phase 3 G4+UI+Pricing → Phase 4 launch prep.

---

## Pattern audit reference

Questo piano usa pattern **verificati** dal codebase reale (audit eseguito 2026-05-04):

| Topic | Pattern reale | Riferimento file |
|-------|---------------|-------------------|
| MediatR handlers | `ICommandHandler<TCmd,TRes>`, `IQueryHandler<TQ,TRes>` (NOT IRequestHandler base) | `BoundedContexts/DocumentProcessing/Application/Commands/AddRulebookCommand.cs:45` |
| Naming convention | `{Feature}Command.cs` + `{Feature}CommandHandler.cs` (no suffisso, sealed class) | Same |
| Domain entity | Factory methods (`Reconstitute`, `CreateCopy`) + private setters | `BoundedContexts/DocumentProcessing/Domain/Entities/PdfDocument.cs:13` |
| Background jobs | **Quartz.NET** `IJob` per scheduled, `IBackgroundTaskService.ExecuteWithCancellation` per fire-and-forget | `Services/IBackgroundTaskService.cs:6`, `BoundedContexts/Administration/Infrastructure/Scheduling/GenerateReportJob.cs` |
| Background enqueue | `_mediator.Send(new Queue.EnqueuePdfCommand(...))` pattern | `BoundedContexts/DocumentProcessing/Application/Commands/AddRulebookCommandHandler.cs:431` |
| Blob storage | `IBlobStorageService` in `Api.Services.Pdf` namespace; methods: `StoreAsync`, `RetrieveAsync`, `GetPresignedDownloadUrlAsync(fileId, gameId, expirySeconds?)` | `Services/Pdf/IBlobStorageService.cs:10` |
| Storage factory | `BlobStorageServiceFactory.Create()` con `STORAGE_PROVIDER` env var | `Services/Pdf/BlobStorageServiceFactory.cs:19` |
| Repository | Aggregate-specific extending `IRepository<TEntity,TKey>` da `Api.SharedKernel.Infrastructure.Persistence`; DbContext direct in handlers OK | `BoundedContexts/DocumentProcessing/Domain/Repositories/IPdfDocumentRepository.cs:7` |
| User context | NO `IUserContext`. UserId passato come parametro in `command.UserId`/`query.UserId` | `AddRulebookCommandHandler.cs:70`, `AskQuestionQueryHandler.cs:101` |
| RAG composition | NO `IRagPipeline`. Compose: `SearchQueryHandler` + `IEmbeddingService.GenerateEmbeddingAsync` + `ISemanticResponseCache.TryGetAsync` + `IRagValidationPipelineService` + `ILlmService` | `BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionQueryHandler.cs:24` |
| Citation DTO | `CitationDto { DocumentId, PageNumber, Snippet, RelevanceScore }` | Same:157-161 |
| HttpClient | Named (`AddHttpClient("ai-embedding")`) o Typed (`AddHttpClient<IFoo,FooImpl>()`); NO Polly esplicito | `BoundedContexts/Administration/Infrastructure/DependencyInjection/AdministrationServiceExtensions.cs` |
| Authorization | Tier enforcement via `ITierEnforcementService.CanPerformAsync()` | `AddRulebookCommandHandler.cs:172` |
| Frontend HTTP | `HttpClient` class con `get<T>(path, schema?, options?)`, Zod validation, retry, circuit breaker, dedupe | `apps/web/src/lib/api/core/httpClient.ts:80` |
| Frontend auth | `getStoredApiKeySync()` + cookies `credentials: 'include'` | Same:10, 130 |
| Frontend errors | Custom `NetworkError`, `SchemaValidationError`, `ApiError` | Same:16 |
| Migrations naming | `{yyyyMMddHHmmss}_{Description}.cs` (es. `20260417193010_AddVisionSnapshots.cs`) | `apps/api/src/Api/Migrations/` |
| Migration FK pattern | `table.ForeignKey(name, column, principalTable, principalColumn, onDelete: ReferentialAction.Cascade)` | `Migrations/20260410113220_Initial.cs` |
| Schema organization | `knowledge_base`, `administration`, `session_tracking`, `entity_relationships`, `game_toolbox`, `game_toolkit` | Same |
| Testing | **xUnit + NSubstitute + FluentAssertions** (NOT Moq); InMemory ConfigurationBuilder per integration | `tests/Api.Tests/Application/Seed/SeedOrchestratorTests.cs:3-4` |

---

## Phase Overview & Risk-Driven Sequencing (revised)

| Phase | Calendar | Deliverable | Risk gate |
|-------|----------|-------------|-----------|
| **Phase 0** | Pre-sprint week -6..0 | Prerequisites + infra + observability + spike validation | OCR viability check |
| **Phase 1** | Sprint 1-3 (week 1-6) | G1 photo-first ingestion vertical slice | Confidence ≥ 85% / ≥ 95% pages |
| **Phase 2** | Sprint 4-6 (week 7-12) | G3 Q&A + TranslationService minimal + glossary | Hallucination ≤ 3% golden |
| **Phase 3** | Sprint 7-9 (week 13-18) | G4 translation full + UI screens + pricing engine | Payment flow + UX |
| **Phase 4** | Sprint 10-11 (week 19-24) | Integration, chaos, usability, GDPR/Privacy, launch | Production readiness |

**Realistic calendar: 22-25 weeks (5-6 mesi)**.

**Parallel work opportunities:**
- Phase 0: PR-1 legal // PR-2 OCR validation // PR-3 contractor brief // CAX31 provisioning // observability stack
- Phase 1 ML work + Phase 2 backend (mock data) overlap from week 4
- Phase 3 designer mockups during Phase 2 implementation
- Phase 4 chaos testing in parallel with usability (different teams)

---

## File Structure Map (corrected)

### Backend (`apps/api/src/Api/`)

**DocumentProcessing BC extension** (photo-first):
- Create: `BoundedContexts/DocumentProcessing/Domain/Entities/PhotoBatchUpload.cs`
- Create: `BoundedContexts/DocumentProcessing/Domain/Entities/PhotoBatchPage.cs`
- Create: `BoundedContexts/DocumentProcessing/Domain/Events/PhotoBatchCompletedEvent.cs`
- Create: `BoundedContexts/DocumentProcessing/Domain/ValueObjects/PageOrientation.cs`
- Create: `BoundedContexts/DocumentProcessing/Domain/ValueObjects/ConfidenceLevel.cs`
- Create: `BoundedContexts/DocumentProcessing/Domain/Repositories/IPhotoBatchUploadRepository.cs`
- Create: `BoundedContexts/DocumentProcessing/Application/Commands/UploadPhotoBatchCommand.cs`
- Create: `BoundedContexts/DocumentProcessing/Application/Commands/UploadPhotoBatchCommandHandler.cs`
- Create: `BoundedContexts/DocumentProcessing/Application/Validators/UploadPhotoBatchCommandValidator.cs`
- Create: `BoundedContexts/DocumentProcessing/Application/Services/IPhotoPreprocessor.cs`
- Create: `BoundedContexts/DocumentProcessing/Application/Services/IPhotoBatchProcessor.cs`
- Create: `BoundedContexts/DocumentProcessing/Application/Queries/GetPhotoBatchStatusQuery.cs`
- Create: `BoundedContexts/DocumentProcessing/Application/Queries/GetPhotoBatchStatusQueryHandler.cs`
- Create: `BoundedContexts/DocumentProcessing/Application/DTOs/PhotoBatchStatusDto.cs`
- Create: `BoundedContexts/DocumentProcessing/Infrastructure/Services/SmoldoclingPhotoPreprocessor.cs`
- Create: `BoundedContexts/DocumentProcessing/Infrastructure/Services/PhotoBatchProcessor.cs`
- Create: `BoundedContexts/DocumentProcessing/Infrastructure/Persistence/PhotoBatchUploadRepository.cs`

**TranslationService cross-cutting** (`Infrastructure/Translation/`):
- Create: `Infrastructure/Translation/INarrativeTranslationService.cs`
- Create: `Infrastructure/Translation/IGenericTranslationService.cs`
- Create: `Infrastructure/Translation/OpenRouterTranslationClient.cs`
- Create: `Infrastructure/Translation/Models/TranslationRequest.cs`
- Create: `Infrastructure/Translation/Models/TranslationResponse.cs`
- Create: `Infrastructure/Translation/Models/Glossary.cs`
- Create: `Infrastructure/Translation/Models/OpenRouterCompletionResponse.cs`
- Create: `Infrastructure/Translation/Cache/ITranslationCache.cs`
- Create: `Infrastructure/Translation/Cache/RedisTranslationCache.cs`
- Create: `Infrastructure/Translation/Routing/IModelRouter.cs`
- Create: `Infrastructure/Translation/Routing/OpenRouterModelRouter.cs`
- Create: `Infrastructure/Translation/Routing/OpenRouterConfig.cs`
- Create: `Infrastructure/Translation/Prompts/NarrativeTranslationPrompt.cs`
- Create: `Infrastructure/Translation/Resilience/CircuitBreakerPolicy.cs`
- Create: `Infrastructure/Translation/Cost/CostCalculator.cs`

**Glossary BC** (riuso AgentMemory):
- Create: `BoundedContexts/AgentMemory/Application/Commands/AddGlossaryEntryCommand.cs`
- Create: `BoundedContexts/AgentMemory/Application/Commands/AddGlossaryEntryCommandHandler.cs`
- Create: `BoundedContexts/AgentMemory/Application/Queries/GetGameGlossaryQuery.cs`
- Create: `BoundedContexts/AgentMemory/Application/Queries/GetGameGlossaryQueryHandler.cs`
- Create: `BoundedContexts/AgentMemory/Domain/Entities/GameGlossaryEntry.cs`
- Create: `BoundedContexts/AgentMemory/Domain/Repositories/IGameGlossaryRepository.cs`

**KnowledgeBase BC extension**:
- Modify: `BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionQuery.cs` (add `ResponseLanguage`, `SessionId`)
- Modify: `BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionQueryHandler.cs` (translate output, integrate house rules check, pricing consume)
- Create: `BoundedContexts/KnowledgeBase/Application/Queries/GetParagraphQuery.cs`
- Create: `BoundedContexts/KnowledgeBase/Application/Queries/GetParagraphQueryHandler.cs`
- Create: `BoundedContexts/KnowledgeBase/Application/Services/IQAComplexityClassifier.cs`
- Create: `BoundedContexts/KnowledgeBase/Application/Services/HeuristicQAComplexityClassifier.cs`

**House rules** (estensione AgentMemory):
- Create: `BoundedContexts/AgentMemory/Application/Commands/SaveHouseRuleCommand.cs`
- Create: `BoundedContexts/AgentMemory/Application/Commands/SaveHouseRuleCommandHandler.cs`
- Create: `BoundedContexts/AgentMemory/Application/Queries/GetHouseRulesForGameQuery.cs`
- Create: `BoundedContexts/AgentMemory/Application/Queries/GetHouseRulesForGameQueryHandler.cs`
- Create: `BoundedContexts/AgentMemory/Application/Services/IHouseRuleMatcher.cs`
- Create: `BoundedContexts/AgentMemory/Application/Services/SemanticHouseRuleMatcher.cs`
- Create: `BoundedContexts/AgentMemory/Domain/Entities/HouseRule.cs`
- Create: `BoundedContexts/AgentMemory/Domain/Repositories/IHouseRuleRepository.cs`

**Pricing capability** (`Infrastructure/Pricing/`):
- Create: `Infrastructure/Pricing/IPricingEngine.cs`
- Create: `Infrastructure/Pricing/CreditBasedPricingEngine.cs`
- Create: `Infrastructure/Pricing/Models/UserQuota.cs`
- Create: `Infrastructure/Pricing/Models/BillableOperation.cs`
- Create: `Infrastructure/Pricing/Models/QuotaCheckResult.cs`
- Create: `Infrastructure/Pricing/Stripe/StripeCheckoutService.cs`
- Create: `Infrastructure/Pricing/Stripe/StripeWebhookHandler.cs`
- Create: `Infrastructure/Pricing/Stripe/StripeConfig.cs`
- Create: `BoundedContexts/Administration/Infrastructure/Scheduling/MonthlyQuotaResetJob.cs` (Quartz)

**Rate limiting**:
- Create: `Infrastructure/RateLimiting/UserRateLimiter.cs`
- Create: `Infrastructure/RateLimiting/RateLimitMiddleware.cs`

**GDPR / Privacy**:
- Create: `BoundedContexts/Authentication/Application/Commands/DeleteUserDataCommand.cs`
- Create: `BoundedContexts/Authentication/Application/Commands/DeleteUserDataCommandHandler.cs`
- Create: `BoundedContexts/Authentication/Application/Queries/ExportUserDataQuery.cs`

**Routing**:
- Create: `Routing/PhotoIngestionEndpoints.cs`
- Create: `Routing/TranslationEndpoints.cs`
- Create: `Routing/HouseRulesEndpoints.cs`
- Create: `Routing/GlossaryEndpoints.cs`
- Create: `Routing/PricingEndpoints.cs`
- Create: `Routing/StripeWebhookEndpoints.cs`
- Create: `Routing/UserDataEndpoints.cs` (GDPR)
- Modify: `Routing/KnowledgeBaseEndpoints.cs` (add paragraph endpoint)

### Frontend (`apps/web/src/`)

**Routes**:
- Create: `app/(authenticated)/gamebook/page.tsx`
- Create: `app/(authenticated)/gamebook/upload/page.tsx`
- Create: `app/(authenticated)/gamebook/[gameId]/play/page.tsx`
- Create: `app/(public)/privacy/page.tsx` (GDPR)

**Components**:
- Create: `app/(authenticated)/gamebook/upload/_components/PhotoUploader.tsx`
- Create: `app/(authenticated)/gamebook/upload/_components/ConfidenceBadge.tsx`
- Create: `app/(authenticated)/gamebook/upload/_components/UploadProgress.tsx`
- Create: `app/(authenticated)/gamebook/[gameId]/play/_components/QAPanel.tsx`
- Create: `app/(authenticated)/gamebook/[gameId]/play/_components/TranslationViewer.tsx`
- Create: `app/(authenticated)/gamebook/[gameId]/play/_components/HouseRuleModal.tsx`
- Create: `app/(authenticated)/gamebook/[gameId]/play/_components/QuotaExceededModal.tsx`
- Create: `app/(authenticated)/gamebook/[gameId]/play/_components/CitationPreview.tsx`

**API client + hooks** (using existing HttpClient):
- Create: `lib/gamebook/schemas.ts` (Zod)
- Create: `lib/gamebook/api.ts`
- Create: `lib/gamebook/hooks/usePhotoBatchUpload.ts`
- Create: `lib/gamebook/hooks/usePhotoBatchStatus.ts`
- Create: `lib/gamebook/hooks/useAskQuestion.ts`
- Create: `lib/gamebook/hooks/useTranslateParagraph.ts`
- Create: `lib/gamebook/hooks/useHouseRules.ts`
- Create: `lib/gamebook/hooks/useQuota.ts`
- Create: `lib/gamebook/hooks/useStartCheckout.ts`

**Utils**:
- Create: `lib/gamebook/file-to-base64.ts`

**i18n**:
- Create: `i18n/locales/it/gamebook.json`

### Database migrations (timestamps placeholder, generate at apply time)
- Create: `apps/api/src/Api/Migrations/{yyyyMMddHHmmss}_AddPhotoIngestion.cs`
- Create: `apps/api/src/Api/Migrations/{yyyyMMddHHmmss}_AddCreditBalance.cs`
- Create: `apps/api/src/Api/Migrations/{yyyyMMddHHmmss}_AddHouseRules.cs`
- Create: `apps/api/src/Api/Migrations/{yyyyMMddHHmmss}_AddGameGlossary.cs`

### Tests
- Create: `tests/Api.Tests/DocumentProcessing/PhotoIngestion/UploadPhotoBatchCommandHandlerTests.cs`
- Create: `tests/Api.Tests/DocumentProcessing/PhotoIngestion/UploadPhotoBatchCommandValidatorTests.cs`
- Create: `tests/Api.Tests/DocumentProcessing/PhotoIngestion/PhotoBatchProcessorTests.cs`
- Create: `tests/Api.Tests/DocumentProcessing/PhotoIngestion/SmoldoclingPhotoPreprocessorTests.cs`
- Create: `tests/Api.Tests/DocumentProcessing/PhotoIngestion/GetPhotoBatchStatusQueryHandlerTests.cs`
- Create: `tests/Api.Tests/Translation/OpenRouterTranslationClientTests.cs`
- Create: `tests/Api.Tests/Translation/RedisTranslationCacheTests.cs`
- Create: `tests/Api.Tests/Translation/OpenRouterModelRouterTests.cs`
- Create: `tests/Api.Tests/Translation/CostCalculatorTests.cs`
- Create: `tests/Api.Tests/KnowledgeBase/AskQuestionQueryHandlerWithTranslationTests.cs`
- Create: `tests/Api.Tests/KnowledgeBase/GetParagraphQueryHandlerTests.cs`
- Create: `tests/Api.Tests/AgentMemory/HouseRuleMatcherTests.cs`
- Create: `tests/Api.Tests/AgentMemory/GameGlossaryRepositoryTests.cs`
- Create: `tests/Api.Tests/Pricing/CreditBasedPricingEngineTests.cs`
- Create: `tests/Api.Tests/Pricing/UserQuotaTests.cs`
- Create: `tests/Api.Tests/Pricing/StripeWebhookHandlerTests.cs`
- Create: `tests/Api.Tests/RateLimiting/UserRateLimiterTests.cs`
- Create: `tests/Api.Tests/Authentication/DeleteUserDataCommandHandlerTests.cs`
- Create: `tests/Api.Tests/Integration/GamebookE2ETests.cs`
- Create: `apps/web/__tests__/gamebook/upload/PhotoUploader.test.tsx`
- Create: `apps/web/__tests__/gamebook/play/QAPanel.test.tsx`
- Create: `apps/web/__tests__/gamebook/play/TranslationViewer.test.tsx`
- Create: `apps/web/__tests__/gamebook/play/QuotaExceededModal.test.tsx`
- Create: `apps/web/e2e/gamebook/upload-flow.spec.ts`
- Create: `apps/web/e2e/gamebook/qa-flow.spec.ts`
- Create: `apps/web/e2e/gamebook/translation-flow.spec.ts`
- Create: `apps/web/e2e/gamebook/quota-flow.spec.ts`
- Create: `apps/web/e2e/gamebook/full-session.spec.ts`

### LLM evaluation harness
- Create: `tests/llm-eval/golden-set/qa-questions.jsonl`
- Create: `tests/llm-eval/golden-set/translation-paragraphs.jsonl`
- Create: `tests/llm-eval/golden-set/README.md`
- Create: `tests/llm-eval/runners/qa_eval.py`
- Create: `tests/llm-eval/runners/translation_eval.py`
- Create: `tests/llm-eval/runners/judge.py` (LLM-as-judge module)
- Create: `tests/llm-eval/runners/requirements.txt`
- Create: `.github/workflows/llm-eval-gate.yml`
- Create: `infra/docker-compose.test.yml`

### OCR validation harness
- Create: `tests/llm-eval/ocr-validation/manuals/.gitkeep`
- Create: `tests/llm-eval/ocr-validation/run_validation.py`
- Create: `tests/llm-eval/ocr-validation/results.md`

### Infrastructure
- Create: `infra/hetzner/cax31-bootstrap.sh`
- Create: `infra/hetzner/disaster-recovery.md`
- Create: `infra/hetzner/backup.sh`
- Create: `infra/hetzner/backup.cron`
- Create: `infra/observability/prometheus.yml`
- Create: `infra/observability/grafana-datasources.yml`
- Create: `infra/observability/grafana-dashboards/cost-telemetry.json`
- Create: `infra/observability/grafana-dashboards/api-performance.json`
- Create: `infra/observability/grafana-dashboards/llm-quality.json`
- Create: `infra/observability/loki-config.yml`
- Create: `infra/observability/promtail-config.yml`
- Modify: `infra/docker-compose.production.yml`
- Create: `infra/secrets/openrouter.secret.example`
- Create: `infra/secrets/stripe.secret.example`

### Documentation
- Create: `docs/development/libro-game-architecture.md`
- Create: `docs/operations/openrouter-runbook.md`
- Create: `docs/operations/llm-cost-monitoring.md`
- Create: `docs/operations/disaster-recovery.md`
- Create: `docs/legal/copyright-position.md`
- Create: `docs/legal/tos-libro-game-addendum.md`
- Create: `docs/legal/privacy-policy-libro-game.md`
- Modify: `CLAUDE.md` (add libro game BC mapping)

---

## Phase 0 — Prerequisites & Infrastructure (Pre-sprint, 5-6 weeks)

**Goal:** Risolvere blockers PR-1/PR-2/PR-3 + setup infrastruttura (incluso observability + monitoring + backup automation).

### Task 0.1 — PR-2: OCR validation su 5 manuali gamebook reali

**Owner:** Computer Vision Engineer (NOT generic ML eng — OpenCV expertise required) + 1 contractor for image acquisition
**Effort:** 2 weeks
**Risk gate:** Decision matrix per condition × game

**Files:**
- Create: `tests/llm-eval/ocr-validation/manuals/.gitkeep` (manuals stored locally only)
- Create: `tests/llm-eval/ocr-validation/run_validation.py`
- Create: `tests/llm-eval/ocr-validation/results.md`

- [ ] **Step 1: Procurare 5 manuali gamebook reali**

Acquistare/scaricare versioni legali di:
1. Tainted Grail (English) — narrative-heavy, layout artistico
2. ISS Vanguard (English) — sci-fi, illustrazioni piene pagina
3. Stuffed Fables (English) — family, illustrations + text
4. Andor Chronicles (German) — chapter-based, no §
5. 7th Continent (French) — atypical layout

- [ ] **Step 2: Fotografare ogni manuale in 3 condizioni**

Per ogni manuale, scattare 10 pagine rappresentative in:
- Buona luce (riferimento)
- Luce salotto serale (scenario reale)
- Angolo 15° (mano umana)

Salvare in `tests/llm-eval/ocr-validation/manuals/<game>/<condition>/page_NN.jpg`.

Acceptance: 150 foto totali (5 × 3 × 10).

- [ ] **Step 3: Scrivere validation script con HTTP call diretto a smoldocling-service**

```python
# tests/llm-eval/ocr-validation/run_validation.py
import json
import os
import requests
from pathlib import Path

SMOLDOCLING_URL = os.environ.get("SMOLDOCLING_URL", "http://localhost:8500")
RESULTS_PATH = Path(__file__).parent / "results.json"
MANUALS_DIR = Path(__file__).parent / "manuals"

def validate_page(img_path: Path) -> dict:
    """POST a smoldocling-service /preprocess endpoint (Task 1.4 dipendency)."""
    with open(img_path, "rb") as f:
        files = {"image": (img_path.name, f, "image/jpeg")}
        data = {"preprocessing_mode": "photo-camera"}
        try:
            response = requests.post(f"{SMOLDOCLING_URL}/preprocess", files=files, data=data, timeout=30)
            response.raise_for_status()
        except requests.RequestException as e:
            return {"page": img_path.stem, "confidence": 0.0, "error": str(e)}

    payload = response.json()
    return {
        "page": img_path.stem,
        "confidence": payload.get("confidence", 0.0),
        "char_count": len(payload.get("extracted_text", "")),
        "warnings": payload.get("warnings", []),
        "is_blank": payload.get("is_blank", False)
    }

def validate_manual_condition(game_name: str, condition: str) -> dict:
    condition_dir = MANUALS_DIR / game_name / condition
    if not condition_dir.exists():
        return {"game": game_name, "condition": condition, "error": "directory missing"}

    pages = sorted(condition_dir.glob("*.jpg"))
    if not pages:
        return {"game": game_name, "condition": condition, "error": "no pages found"}

    results = [validate_page(p) for p in pages]
    valid_results = [r for r in results if "error" not in r]

    if not valid_results:
        return {"game": game_name, "condition": condition, "all_failed": True, "results": results}

    return {
        "game": game_name,
        "condition": condition,
        "page_count": len(valid_results),
        "avg_confidence": sum(r["confidence"] for r in valid_results) / len(valid_results),
        "min_confidence": min(r["confidence"] for r in valid_results),
        "max_confidence": max(r["confidence"] for r in valid_results),
        "high_conf_pct": sum(1 for r in valid_results if r["confidence"] >= 0.85) / len(valid_results) * 100,
        "low_conf_pct": sum(1 for r in valid_results if r["confidence"] < 0.7) / len(valid_results) * 100,
        "results": results
    }

if __name__ == "__main__":
    games = ["tainted-grail", "iss-vanguard", "stuffed-fables", "andor", "7th-continent"]
    conditions = ["good-light", "evening-light", "angled"]
    all_results = []
    for game in games:
        for cond in conditions:
            result = validate_manual_condition(game, cond)
            all_results.append(result)
            print(f"{game}/{cond}: avg={result.get('avg_confidence', 'N/A'):.2f}, low_pct={result.get('low_conf_pct', 'N/A')}%")

    RESULTS_PATH.write_text(json.dumps(all_results, indent=2))
    print(f"\nResults written to {RESULTS_PATH}")
```

- [ ] **Step 4: Pre-requisite: Task 1.4 (preprocessor /preprocess endpoint) deve essere deployato in dev**

Prerequisito: Task 1.4 step "Add /preprocess endpoint to smoldocling-service" deve esistere in dev environment.

Workaround se Task 1.4 non è ancora pronto: usare existing smoldocling endpoint `/extract` modificandolo temporaneamente per accettare `preprocessing_mode` flag.

- [ ] **Step 5: Run validation**

```bash
cd tests/llm-eval/ocr-validation
SMOLDOCLING_URL=http://localhost:8500 python run_validation.py
```

Expected: `results.json` con confidence scores per (game, condition, page).

- [ ] **Step 6: Decision gate matrix normalizzato per condition**

Crea `results.md` con tabelle:

| Game | good-light avg | evening avg | angled avg | high_conf_pct (good) | low_conf_pct (any) |
|------|----------------|-------------|------------|----------------------|--------------------|
| tainted-grail | X | Y | Z | A% | B% |
| ... | ... | ... | ... | ... | ... |

**Gate decision matrix** (per game):
- ✅ **PASS**: avg good-light ≥ 0.85 AND avg angled ≥ 0.7 AND high_conf_pct(good) ≥ 90% → proceed
- ⚠️ **MARGINAL**: avg good-light 0.7-0.85 → proceed but invest in stronger UI confidence indicators (Task 1.6)
- 🔴 **FAIL**: avg good-light < 0.7 OR > 30% pages low_conf in good-light → SCOPE REVIEW required

**Aggregate decision**:
- ✅ All 5 PASS → green light
- ⚠️ 3-4 PASS, 1-2 MARGINAL → green with mitigations
- 🔴 1+ FAIL OR 3+ MARGINAL → STOP — investigate Mistral OCR / Google Document AI / scope reduction

- [ ] **Step 7: Document findings + commit**

```bash
git add tests/llm-eval/ocr-validation/run_validation.py tests/llm-eval/ocr-validation/results.md
git commit -m "feat(eval): PR-2 OCR validation results on 5 gamebook manuals"
```

### Task 0.2 — PR-3: Test set golden creation

**Owner:** ML engineer + IT native speaker contractor (gamebook expertise)
**Effort:** 4 weeks lead time, 2 weeks effective work
**Budget:** ~$3000 contractor

**Files:**
- Create: `tests/llm-eval/golden-set/qa-questions.jsonl`
- Create: `tests/llm-eval/golden-set/translation-paragraphs.jsonl`
- Create: `tests/llm-eval/golden-set/README.md`

- [ ] **Step 1: Identify contractor**

Cercare IT native speaker con:
- Background board game (BGG profile, gamebook player attestato)
- Esperienza traduzione/copyediting (literary)
- Disponibile 80h totali (~$30-40/h)

Pubblicare brief su Upwork / Fiverr Pro / italian gaming communities.

- [ ] **Step 2: Definire schema JSONL Q&A con citation match policy**

```jsonl
{
  "id": "qa-001",
  "game": "tainted-grail",
  "game_id": "00000000-0000-0000-0000-000000000001",
  "question_it": "Quanti dadi tira il mago per il fireball?",
  "expected_answer_it": "Il mago tira 3 dadi base, +1 per ogni livello di Mastery Fuoco oltre 1.",
  "expected_citations": {
    "primary_pages": [22, 23],
    "match_policy": "overlap_at_least_one"
  },
  "category": "combat",
  "difficulty": "easy",
  "expected_confidence": "high"
}
```

`match_policy` enum:
- `"exact"`: actual citations == expected exactly
- `"overlap_at_least_one"`: intersection non vuota
- `"subset"`: actual ⊆ expected
- `"superset"`: actual ⊇ expected

100 questions distribuite:
- 60 easy (well-documented in manual, expected_confidence: high)
- 30 medium (require multi-page synthesis, medium)
- 10 hard (edge cases, ambiguous, no-answer expected — used to test "I don't know" path)

Coperture: 5 manuali × 20 questions/manuale.
Categorie: setup (4), combat (5), narrative (4), character (3), items (2), edge-cases (2) per gioco.

- [ ] **Step 3: Definire schema JSONL translation con tone validation**

```jsonl
{
  "id": "tr-001",
  "game": "tainted-grail",
  "game_id": "00000000-0000-0000-0000-000000000001",
  "source_lang": "en",
  "paragraph_id": "147",
  "source_text": "Niamh raises the Sword of Avalon and the Wraithstone glows with eldritch fire.",
  "expected_translation_it": "Niamh solleva la Spada di Avalon e la Pietra Spettrale brilla di fuoco arcano.",
  "tone": "fantasy-dramatic",
  "glossary": {
    "Sword of Avalon": "Spada di Avalon",
    "Wraithstone": "Pietra Spettrale",
    "Niamh": "Niamh"
  },
  "evaluation_criteria": {
    "preserve_glossary": true,
    "preserve_tone": "fantasy-dramatic",
    "max_bleu_delta": 0.3,
    "min_human_score": 4
  }
}
```

50 paragraphs con varietà:
- 20 narrative descriptive
- 15 dialogue
- 10 climactic/dramatic
- 5 technical (rules embedded in narrative)

- [ ] **Step 4: Brief contractor + spreadsheet workflow**

Brief documento con:
- Goal: ground truth per LLM eval
- Quality bar: traduzione "publication-ready" italiana
- Process: leggere paragrafo → tradurre → review per consistency glossario
- Tools: Google Sheets (1 sheet per game) → export JSONL via script

Provide CSV template con columns: id, source_text, glossary_pairs, tone, expected_translation_it, notes.

- [ ] **Step 5: Internal review process (100% review NOT 10%)**

ML engineer review TUTTI i golden entries:
- Linguaggio italiano corretto?
- Glossario consistent?
- Tono preservato?
- Citation accuracy verificata vs manuale fisico

25% double-blind validation: secondo native speaker (può essere community volunteer) verifica 25 random entries.

Inter-rater agreement target: ≥ 90% concordance.

- [ ] **Step 6: Convert spreadsheet to JSONL + validate schema**

```python
# tests/llm-eval/golden-set/csv_to_jsonl.py (utility script)
import csv, json, sys
from pathlib import Path

def convert(csv_path: Path, jsonl_path: Path, schema_validator):
    with csv_path.open() as f, jsonl_path.open("w") as out:
        reader = csv.DictReader(f)
        for row in reader:
            entry = parse_row(row)
            schema_validator.validate(entry)  # raises if invalid
            out.write(json.dumps(entry, ensure_ascii=False) + "\n")
    print(f"Converted {csv_path} → {jsonl_path}")
```

- [ ] **Step 7: Test runner con sample 5 questions**

Prima di firmare contractor, validate schema runs:

```bash
cd tests/llm-eval
head -5 golden-set/qa-questions.jsonl > /tmp/sample.jsonl
python runners/qa_eval.py --golden-set /tmp/sample.jsonl --dry-run
```

Expected: parses 5 questions without error, prints would-execute summary.

- [ ] **Step 8: Commit**

```bash
git add tests/llm-eval/golden-set/
git commit -m "feat(eval): PR-3 golden test set 100 Q&A + 50 translations with citation match policy"
```

### Task 0.3 — PR-1: Legal review copyright

**Owner:** Aaron (identifies legal advisor) + Legal advisor specializzato IP/copyright
**Effort:** 2-3 weeks calendar
**Budget:** €3000-€8000

**Files:**
- Create: `docs/legal/copyright-position.md`
- Create: `docs/legal/tos-libro-game-addendum.md`
- Create: `docs/legal/privacy-policy-libro-game.md`

- [ ] **Step 1: Identify legal advisor**

Criteri:
- Esperienza prodotti SaaS user-generated content
- Familiarità con UE Copyright Directive 2019/790 + DMCA US
- Idealmente background gaming/entertainment

Sources: studi legali italiani con divisione IP (es. BonelliErede, DLA Piper Italy), o freelance specialists su Linkedin.

- [ ] **Step 2: Brief advisor**

Documento brief con:
- Use case: utenti caricano foto manuali board game posseduti fisicamente
- Scope: indicizzazione + Q&A su contenuto + traduzione frammenti
- NO redistribuzione pubblica
- Free tier limitato (50 pag/mese)
- Architettura: foto storate server-side, deletable on demand
- Geographies: IT primary, EU expansion roadmap, no US per now

- [ ] **Step 3: Advisor produce 4 deliverable**

(a) Position paper su rischi copyright:
- Fair use applicabile in UE? (più ristretto di US)
- Differenza traduzione completa vs frammenti
- Liability shield disponibili (DMCA safe harbor non UE)
- Direttiva UE 2019/790 art. 17 (online content sharing)

(b) TOS addendum specifico libro game:
- User dichiara di possedere copia legale (warranty clause)
- User indemnify MeepleAI per copyright infringement claims
- Right to terminate account su takedown notice valido
- Limitazione responsabilità

(c) Privacy policy GDPR-compliant:
- Storage manuali user-uploaded (location, retention)
- Right to deletion (Article 17) implementation
- Right to data portability (Article 20)
- **Data residency disclosure**: OpenRouter routing US/EU specifics
- Sub-processors disclosure (Anthropic, OpenAI, DeepSeek, Cloudflare, Stripe)
- Cookie policy + consent

(d) Contingency matrix:
- Se advisor dice "no-go" su feature X → quali alternatives?
- Decision tree per scope changes

- [ ] **Step 4: Internal review + commit**

```bash
git add docs/legal/
git commit -m "feat(legal): PR-1 copyright position + TOS + privacy policy + contingency matrix"
```

### Task 0.4 — Hetzner CAX31 provisioning + observability

**Owner:** DevOps (può essere fullstack lead)
**Effort:** 1 week (5 days)

**Files:**
- Create: `infra/hetzner/cax31-bootstrap.sh`
- Create: `infra/hetzner/disaster-recovery.md`
- Create: `infra/hetzner/backup.sh`
- Create: `infra/hetzner/backup.cron`
- Create: `infra/observability/prometheus.yml`
- Create: `infra/observability/grafana-datasources.yml`
- Create: `infra/observability/loki-config.yml`
- Create: `infra/observability/promtail-config.yml`
- Create: `infra/observability/grafana-dashboards/api-performance.json`
- Modify: `infra/docker-compose.production.yml`

- [ ] **Step 1: Provision CAX31 in Hetzner Cloud**

```bash
# Verify Hetzner CLI installed
hcloud version

# List available types in fsn1 location
hcloud server-type list --location fsn1 | grep cax

# Create server
hcloud server create \
  --name meepleai-prod-1 \
  --type cax31 \
  --image ubuntu-24.04 \
  --location fsn1 \
  --ssh-key meepleai-prod \
  --label env=production
```

Expected: Server reachable on assigned IPv4. Note IP for DNS.

- [ ] **Step 2: Bootstrap script with all required setup**

```bash
# infra/hetzner/cax31-bootstrap.sh
#!/usr/bin/env bash
set -euo pipefail

echo "==> System update"
apt-get update && apt-get upgrade -y

echo "==> Install essential tools"
apt-get install -y \
  curl wget git ca-certificates gnupg lsb-release \
  cifs-utils ufw fail2ban htop ncdu jq \
  postgresql-client-common

echo "==> Install Docker (ARM64)"
curl -fsSL https://get.docker.com | sh
systemctl enable --now docker

echo "==> Install Docker Compose plugin"
mkdir -p ~/.docker/cli-plugins
curl -SL https://github.com/docker/compose/releases/latest/download/docker-compose-linux-aarch64 \
  -o ~/.docker/cli-plugins/docker-compose
chmod +x ~/.docker/cli-plugins/docker-compose

echo "==> Firewall (only 22, 80, 443 from internet)"
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable

echo "==> Fail2ban for SSH brute-force protection"
systemctl enable --now fail2ban

echo "==> Swap (8 GB safety net)"
if [ ! -f /swapfile ]; then
  fallocate -l 8G /swapfile
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
  echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

echo "==> Storage Box mount preparation"
mkdir -p /mnt/storagebox
# Credentials file MUST be created manually before first mount (security)
if [ ! -f /etc/cifs-creds-storagebox ]; then
  echo "ATTENTION: Create /etc/cifs-creds-storagebox manually with:"
  echo "  username=u<box-id>"
  echo "  password=<box-password>"
  echo "  Then chmod 600 /etc/cifs-creds-storagebox"
fi

echo "==> Bootstrap complete. Reboot recommended."
echo "==> Next steps:"
echo "  1. Create /etc/cifs-creds-storagebox"
echo "  2. Add Storage Box mount to /etc/fstab"
echo "  3. Deploy docker-compose stack"
echo "  4. Configure DNS"
```

- [ ] **Step 3: Run bootstrap**

```bash
ssh root@<cax31-ip> 'bash -s' < infra/hetzner/cax31-bootstrap.sh
ssh root@<cax31-ip> 'reboot'
```

- [ ] **Step 4: Provision Hetzner Storage Box 1 TB**

Via Hetzner console: order Storage Box BX11 (~€4/mese, 1 TB SMB/SFTP).

Receive credentials. Create on CAX31:

```bash
ssh root@<cax31-ip>
cat > /etc/cifs-creds-storagebox <<EOF
username=u123456
password=<storage-box-password>
EOF
chmod 600 /etc/cifs-creds-storagebox

# Add mount to fstab
echo '//u123456.your-storagebox.de/backup /mnt/storagebox cifs credentials=/etc/cifs-creds-storagebox,vers=3.0,iocharset=utf8,uid=1000,gid=1000 0 0' >> /etc/fstab

# Test mount
mount /mnt/storagebox
touch /mnt/storagebox/test_write && rm /mnt/storagebox/test_write
echo "Storage Box OK"
```

- [ ] **Step 5: Backup automation script**

```bash
# infra/hetzner/backup.sh
#!/usr/bin/env bash
set -euo pipefail

BACKUP_DIR="/mnt/storagebox/backups"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
RETENTION_DAYS=30

mkdir -p "$BACKUP_DIR/postgres" "$BACKUP_DIR/redis" "$BACKUP_DIR/blob"

# PostgreSQL backup (compressed)
echo "==> Backing up PostgreSQL"
docker exec meepleai-postgres pg_dump -U meepleai meepleai_db | \
  gzip > "$BACKUP_DIR/postgres/postgres_${TIMESTAMP}.sql.gz"

# Redis backup (RDB snapshot)
echo "==> Backing up Redis"
docker exec meepleai-redis redis-cli --rdb /tmp/dump.rdb
docker cp meepleai-redis:/tmp/dump.rdb "$BACKUP_DIR/redis/redis_${TIMESTAMP}.rdb"

# Blob storage backup (rsync to box)
echo "==> Backing up blob storage"
rsync -av --delete /var/lib/meepleai/blob/ "$BACKUP_DIR/blob/"

# Encrypt with age (recipient public key in /etc/age.pub)
echo "==> Encrypting backups"
for f in "$BACKUP_DIR/postgres/postgres_${TIMESTAMP}.sql.gz" "$BACKUP_DIR/redis/redis_${TIMESTAMP}.rdb"; do
  age -R /etc/age.pub -o "${f}.age" "$f" && rm "$f"
done

# Retention: delete backups older than RETENTION_DAYS
find "$BACKUP_DIR/postgres" -name "*.age" -mtime +$RETENTION_DAYS -delete
find "$BACKUP_DIR/redis" -name "*.age" -mtime +$RETENTION_DAYS -delete

echo "==> Backup complete: $TIMESTAMP"
```

- [ ] **Step 6: Backup cron**

```bash
# infra/hetzner/backup.cron
# Daily at 03:00 UTC
0 3 * * * /usr/local/bin/backup.sh >> /var/log/meepleai-backup.log 2>&1
# Weekly full to Cloudflare R2 (Sunday 04:00)
0 4 * * 0 /usr/local/bin/backup-to-r2.sh >> /var/log/meepleai-backup-r2.log 2>&1
```

Install:
```bash
scp infra/hetzner/backup.sh root@<cax31-ip>:/usr/local/bin/backup.sh
ssh root@<cax31-ip> 'chmod +x /usr/local/bin/backup.sh && cp infra/hetzner/backup.cron /etc/cron.d/meepleai-backup'
```

- [ ] **Step 7: Observability stack — Prometheus + Grafana + Loki + Promtail**

Add to `infra/docker-compose.production.yml`:

```yaml
services:
  prometheus:
    image: prom/prometheus:latest
    platform: linux/arm64
    volumes:
      - ./observability/prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus_data:/prometheus
    ports:
      - "127.0.0.1:9090:9090"
    mem_limit: 512m

  grafana:
    image: grafana/grafana:latest
    platform: linux/arm64
    volumes:
      - ./observability/grafana-datasources.yml:/etc/grafana/provisioning/datasources/datasources.yml
      - ./observability/grafana-dashboards:/var/lib/grafana/dashboards
      - grafana_data:/var/lib/grafana
    environment:
      - GF_AUTH_ANONYMOUS_ENABLED=false
      - GF_SECURITY_ADMIN_PASSWORD__FILE=/run/secrets/grafana_admin
    ports:
      - "127.0.0.1:3001:3000"
    secrets: [grafana_admin]
    mem_limit: 256m

  loki:
    image: grafana/loki:latest
    platform: linux/arm64
    volumes:
      - ./observability/loki-config.yml:/etc/loki/local-config.yaml
      - loki_data:/loki
    ports:
      - "127.0.0.1:3100:3100"
    mem_limit: 512m

  promtail:
    image: grafana/promtail:latest
    platform: linux/arm64
    volumes:
      - ./observability/promtail-config.yml:/etc/promtail/config.yml
      - /var/log:/var/log:ro
      - /var/lib/docker/containers:/var/lib/docker/containers:ro
    mem_limit: 128m

volumes:
  prometheus_data:
  grafana_data:
  loki_data:
```

- [ ] **Step 8: Prometheus scrape config**

```yaml
# infra/observability/prometheus.yml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'meepleai-api'
    static_configs:
      - targets: ['meepleai-api:8080']
    metrics_path: /metrics

  - job_name: 'postgres'
    static_configs:
      - targets: ['postgres-exporter:9187']

  - job_name: 'redis'
    static_configs:
      - targets: ['redis-exporter:9121']

  - job_name: 'node'
    static_configs:
      - targets: ['node-exporter:9100']
```

- [ ] **Step 9: Reverse proxy (Caddy) + DNS + TLS**

```yaml
# Add to docker-compose.production.yml
services:
  caddy:
    image: caddy:latest
    platform: linux/arm64
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile
      - caddy_data:/data
      - caddy_config:/config
    mem_limit: 128m

volumes:
  caddy_data:
  caddy_config:
```

```caddy
# infra/Caddyfile
meepleai.com {
  reverse_proxy meepleai-web:3000
}

api.meepleai.com {
  reverse_proxy meepleai-api:8080
}

grafana.meepleai.com {
  reverse_proxy grafana:3000
  basicauth {
    admin {env.GRAFANA_BASIC_AUTH_HASH}
  }
}
```

- DNS Cloudflare: `meepleai.com`, `api.meepleai.com`, `grafana.meepleai.com` → CAX31 IPv4
- Test: `curl https://meepleai.com/health` returns 200, `curl https://api.meepleai.com/health` returns 200
- TLS: automatic via Caddy + Let's Encrypt

- [ ] **Step 10: DR runbook + commit**

```markdown
# infra/hetzner/disaster-recovery.md

## Scenario 1: CAX31 down/lost
**RTO target: 2 hours**

1. Provision new CAX31: `hcloud server create ...` (Step 1 above)
2. Run bootstrap script (Step 2-3)
3. Mount Storage Box (Step 4)
4. Restore PostgreSQL: `gunzip < $LATEST_BACKUP | docker exec -i meepleai-postgres psql -U meepleai meepleai_db`
5. Restore Redis: `docker cp $LATEST_RDB meepleai-redis:/data/dump.rdb && docker restart meepleai-redis`
6. Restore blob: `rsync -av /mnt/storagebox/backups/blob/ /var/lib/meepleai/blob/`
7. Update DNS Cloudflare to new IP
8. Verify health endpoints

## Scenario 2: Region down (Falkenstein)
**RTO target: 30 min via hot standby Helsinki**
... (detailed steps)

## Scenario 3: Data corruption
**RTO target: 4 hours**
... (detailed steps)
```

```bash
git add infra/hetzner/ infra/observability/ infra/Caddyfile infra/docker-compose.production.yml
git commit -m "feat(infra): provision Hetzner CAX31 + Storage Box + observability + DR runbook"
```

### Task 0.5 — OpenRouter setup + cost alerting

**Owner:** Backend lead
**Effort:** 1 day

**Files:**
- Create: `infra/secrets/openrouter.secret.example`
- Create: `infra/secrets/openrouter.secret` (NOT committed)
- Create: `infra/observability/grafana-dashboards/cost-telemetry.json`

- [ ] **Step 1: Create OpenRouter account + initial credit**

- Register at openrouter.ai
- Add credit balance ($50 starter for development)
- Generate API key with scoped permissions (rate limit settings)

- [ ] **Step 2: Secret template**

```bash
# infra/secrets/openrouter.secret.example
OPENROUTER_API_KEY=<your_openrouter_api_key>
OPENROUTER_BASE_URL=https://openrouter.ai/api/v1
OPENROUTER_HTTP_REFERER=https://meepleai.com
OPENROUTER_X_TITLE=MeepleAI
OPENROUTER_DEFAULT_MODEL=anthropic/claude-haiku-4.5
OPENROUTER_TRANSLATION_MODEL=anthropic/claude-sonnet-4.5
OPENROUTER_QA_BULK_MODEL=deepseek/deepseek-v3
OPENROUTER_FALLBACK_MODELS=openai/gpt-4o-mini,meta-llama/llama-3.1-70b
OPENROUTER_MAX_RETRIES=3
OPENROUTER_TIMEOUT_SECONDS=30
OPENROUTER_DAILY_COST_SOFT_ALERT_USD=100
OPENROUTER_DAILY_COST_HARD_LIMIT_USD=200
```

- [ ] **Step 3: Sync secret to staging**

```bash
cd infra
make secrets-setup
# Manually populate openrouter.secret with real values via secure channel
make secrets-sync
```

- [ ] **Step 4: Validate via curl**

```bash
source infra/secrets/openrouter.secret
curl -X POST $OPENROUTER_BASE_URL/chat/completions \
  -H "Authorization: Bearer $OPENROUTER_API_KEY" \
  -H "Content-Type: application/json" \
  -H "HTTP-Referer: $OPENROUTER_HTTP_REFERER" \
  -H "X-Title: $OPENROUTER_X_TITLE" \
  -d '{
    "model": "anthropic/claude-haiku-4.5",
    "messages": [{"role": "user", "content": "Hello"}]
  }'
```

Expected: 200 response with completion.

- [ ] **Step 5: Cost telemetry dashboard Grafana**

Provisioned dashboard JSON (`infra/observability/grafana-dashboards/cost-telemetry.json`) con:
- Total cost / day (24h window)
- Cost per operation type (translation, Q&A, setup)
- Cost per model
- P95 latency per model
- Error rate per model
- Alert lines: soft $100/day, hard $200/day

- [ ] **Step 6: Prometheus alerting rules**

```yaml
# infra/observability/prometheus-alerts.yml
groups:
  - name: openrouter-cost
    rules:
      - alert: OpenRouterDailyCostSoftAlert
        expr: sum(increase(openrouter_cost_usd_total[24h])) > 100
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "OpenRouter daily cost > $100"
      - alert: OpenRouterDailyCostHardLimit
        expr: sum(increase(openrouter_cost_usd_total[24h])) > 200
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "OpenRouter daily cost > $200 — circuit breaker should engage"
```

- [ ] **Step 7: Commit**

```bash
git add infra/secrets/openrouter.secret.example infra/observability/grafana-dashboards/cost-telemetry.json infra/observability/prometheus-alerts.yml
git commit -m "feat(infra): OpenRouter setup + cost telemetry dashboard + alerting"
```

### Task 0.6 — Stripe account + secret management

**Owner:** Backend lead
**Effort:** 1 day

**Files:**
- Create: `infra/secrets/stripe.secret.example`

- [ ] **Step 1: Create Stripe account (test mode)**

Register at stripe.com. Note publishable + secret keys.
Create webhook endpoint URL placeholder: `https://api.meepleai.com/api/v1/webhooks/stripe`.
Note webhook signing secret.

- [ ] **Step 2: Secret template**

```bash
# infra/secrets/stripe.secret.example
STRIPE_PUBLISHABLE_KEY=<your_stripe_publishable_key>
STRIPE_SECRET_KEY=<your_stripe_secret_key>
STRIPE_WEBHOOK_SECRET=<your_stripe_webhook_secret>
STRIPE_PRICE_100_CREDITS=<your_stripe_price_id>  # €5 = 100 credits
```

- [ ] **Step 3: Create product + price in Stripe Dashboard**

- Product: "MeepleAI Credits"
- Price: €5.00 EUR, one-time, payment mode
- Note `price_id`, populate `STRIPE_PRICE_100_CREDITS`

- [ ] **Step 4: Sync secret + commit template**

```bash
cd infra && make secrets-sync
git add infra/secrets/stripe.secret.example
git commit -m "feat(infra): Stripe account + secret template for credits product"
```

### Task 0.7 — Shared Game Catalog integration verification

**Owner:** Backend lead
**Effort:** 2 days

- [ ] **Step 1: Verify SharedGameCatalog API supports gamebook lookup**

Read existing `BoundedContexts/SharedGameCatalog/` BC. Verify queries available:
- `FindByNameQuery` (search Tainted Grail by name)
- `FindByBggIdQuery` (BGG integration)

- [ ] **Step 2: Test deduplication via content hash**

Verify `IPdfDocumentRepository.FindByContentHashAsync` works for photo-derived content. If not, extend.

- [ ] **Step 3: Document in plan v2 architecture**

```markdown
# docs/development/libro-game-architecture.md (excerpt)

## SharedGameCatalog integration
- Photo upload triggers `SharedGameCatalog.FindByNameQuery` for game match
- Content hash dedup: SHA-256 of stitched manual content → `IPdfDocumentRepository.FindByContentHashAsync`
- New game flow: if not found, prompt user to create entry via existing `CreateGameCommand`
```

- [ ] **Step 4: Commit**

```bash
git add docs/development/libro-game-architecture.md
git commit -m "docs(architecture): SharedGameCatalog integration for libro game flow"
```

---

## Phase 1 — G1 Photo-First Ingestion (Sprint 1-3, weeks 1-6)

**Goal:** Sara può fotografare 50 pagine di un manuale e averle indicizzate in < 5 min con confidence visibile.

**Acceptance gate:** Run end-to-end test su 1 manuale Tainted Grail completo + verificare:
- Throughput ≥ 10 pag/min batch parallel
- Confidence ≥ 0.7 su ≥ 95% pagine
- UI mostra preview confidence + opzione "rifotografa"
- KB query funzionante post-indexing

### Task 1.1 — PhotoBatchUpload aggregate root

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/PhotoBatchUpload.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/PhotoBatchPage.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/ValueObjects/PageOrientation.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/ValueObjects/ConfidenceLevel.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Events/PhotoBatchCompletedEvent.cs`
- Test: `tests/Api.Tests/DocumentProcessing/PhotoIngestion/PhotoBatchUploadTests.cs`

- [ ] **Step 1: Write failing entity test (TDD)**

```csharp
// tests/Api.Tests/DocumentProcessing/PhotoIngestion/PhotoBatchUploadTests.cs
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using FluentAssertions;
using Xunit;

public class PhotoBatchUploadTests
{
    [Fact]
    public void Create_WithValidParams_InitializesPending()
    {
        var batch = PhotoBatchUpload.Create(
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            sourceLanguage: "en",
            totalPages: 50);

        batch.Status.Should().Be(PhotoBatchStatus.Pending);
        batch.IndexedPages.Should().Be(0);
        batch.TotalPages.Should().Be(50);
    }

    [Fact]
    public void Create_WithZeroPages_ThrowsArgumentException()
    {
        Action act = () => PhotoBatchUpload.Create(Guid.NewGuid(), Guid.NewGuid(), "en", 0);
        act.Should().Throw<ArgumentException>().WithMessage("*positive*");
    }

    [Fact]
    public void RecordPageIndexed_WhenAllIndexed_RaisesCompletedEventAndSetsCompleted()
    {
        var batch = PhotoBatchUpload.Create(Guid.NewGuid(), Guid.NewGuid(), "en", 2);
        batch.StartProcessing();

        batch.RecordPageIndexed(pageNumber: 1, confidence: 0.9, warnings: Array.Empty<string>());
        batch.Status.Should().Be(PhotoBatchStatus.Processing);
        batch.DomainEvents.Should().BeEmpty();

        batch.RecordPageIndexed(pageNumber: 2, confidence: 0.85, warnings: Array.Empty<string>());
        batch.Status.Should().Be(PhotoBatchStatus.Completed);
        batch.DomainEvents.Should().ContainSingle(e => e is PhotoBatchCompletedEvent);
    }
}
```

- [ ] **Step 2: Run test (BUILD FAIL — types not defined)**

```bash
cd tests/Api.Tests
dotnet test --filter "FullyQualifiedName~PhotoBatchUploadTests"
```

Expected: BUILD FAIL.

- [ ] **Step 3: Define value objects**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/ValueObjects/PageOrientation.cs
namespace Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

public enum PageOrientation { Portrait, Landscape, Rotated, Unknown }
```

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/ValueObjects/ConfidenceLevel.cs
namespace Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

public enum ConfidenceLevel { High, Medium, Low }

public static class ConfidenceLevelExtensions
{
    public static ConfidenceLevel FromScore(double score) => score switch
    {
        >= 0.85 => ConfidenceLevel.High,
        >= 0.7 => ConfidenceLevel.Medium,
        _ => ConfidenceLevel.Low
    };
}
```

- [ ] **Step 4: Define domain event**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Events/PhotoBatchCompletedEvent.cs
namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

public sealed class PhotoBatchCompletedEvent : IDomainEvent
{
    public Guid BatchId { get; }
    public Guid UserId { get; }
    public Guid GameId { get; }
    public int TotalPages { get; }
    public int LowConfidencePages { get; }
    public DateTime OccurredAt { get; }

    public PhotoBatchCompletedEvent(Guid batchId, Guid userId, Guid gameId, int totalPages, int lowConfidencePages)
    {
        BatchId = batchId;
        UserId = userId;
        GameId = gameId;
        TotalPages = totalPages;
        LowConfidencePages = lowConfidencePages;
        OccurredAt = DateTime.UtcNow;
    }
}
```

(Assumes `IDomainEvent` interface exists in `Api.SharedKernel.Domain` — verify in spike if not, create.)

- [ ] **Step 5: Define entities with factory + private setters**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/PhotoBatchPage.cs
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Entities;

public class PhotoBatchPage
{
    public Guid Id { get; private set; }
    public Guid PhotoBatchUploadId { get; private set; }
    public int PageNumber { get; private set; }
    public string BlobKey { get; private set; } = null!;
    public double Confidence { get; private set; }
    public ConfidenceLevel ConfidenceLevel { get; private set; }
    public PageOrientation Orientation { get; private set; }
    public bool IsBlank { get; private set; }
    public string[] Warnings { get; private set; } = Array.Empty<string>();
    public string? ExtractedText { get; private set; }
    public DateTime IndexedAt { get; private set; }

    private PhotoBatchPage() { } // EF

    public static PhotoBatchPage Create(
        Guid batchId,
        int pageNumber,
        string blobKey,
        double confidence,
        PageOrientation orientation,
        bool isBlank,
        string[] warnings,
        string extractedText)
    {
        return new PhotoBatchPage
        {
            Id = Guid.NewGuid(),
            PhotoBatchUploadId = batchId,
            PageNumber = pageNumber,
            BlobKey = blobKey,
            Confidence = confidence,
            ConfidenceLevel = ConfidenceLevelExtensions.FromScore(confidence),
            Orientation = orientation,
            IsBlank = isBlank,
            Warnings = warnings ?? Array.Empty<string>(),
            ExtractedText = extractedText,
            IndexedAt = DateTime.UtcNow
        };
    }
}
```

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/PhotoBatchUpload.cs
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.SharedKernel.Domain;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Entities;

public class PhotoBatchUpload : AggregateRoot
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid GameId { get; private set; }
    public string SourceLanguage { get; private set; } = null!;
    public PhotoBatchStatus Status { get; private set; }
    public int TotalPages { get; private set; }
    public int IndexedPages { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    [Timestamp]
    public byte[] RowVersion { get; private set; } = null!;

    private readonly List<PhotoBatchPage> _pages = new();
    public IReadOnlyCollection<PhotoBatchPage> Pages => _pages.AsReadOnly();

    private PhotoBatchUpload() { } // EF

    public static PhotoBatchUpload Create(Guid userId, Guid gameId, string sourceLanguage, int totalPages)
    {
        if (totalPages <= 0) throw new ArgumentException("Total pages must be positive", nameof(totalPages));
        if (string.IsNullOrWhiteSpace(sourceLanguage)) throw new ArgumentException("Source language required", nameof(sourceLanguage));

        return new PhotoBatchUpload
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            SourceLanguage = sourceLanguage,
            Status = PhotoBatchStatus.Pending,
            TotalPages = totalPages,
            IndexedPages = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void StartProcessing()
    {
        if (Status != PhotoBatchStatus.Pending)
            throw new InvalidOperationException($"Cannot start processing from status {Status}");
        Status = PhotoBatchStatus.Processing;
    }

    public void RecordPageIndexed(int pageNumber, double confidence, string[] warnings)
    {
        IndexedPages++;
        if (IndexedPages >= TotalPages)
        {
            Status = PhotoBatchStatus.Completed;
            CompletedAt = DateTime.UtcNow;
            var lowConfPages = _pages.Count(p => p.Confidence < 0.7);
            RaiseDomainEvent(new PhotoBatchCompletedEvent(Id, UserId, GameId, TotalPages, lowConfPages));
        }
    }

    public void AttachPage(PhotoBatchPage page)
    {
        if (page.PhotoBatchUploadId != Id)
            throw new InvalidOperationException("Page batch ID mismatch");
        _pages.Add(page);
    }

    public void Fail(string reason)
    {
        Status = PhotoBatchStatus.Failed;
        CompletedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }
}

public enum PhotoBatchStatus { Pending, Processing, Completed, Failed }
```

(Assumes `AggregateRoot` base class with `RaiseDomainEvent` exists in `Api.SharedKernel.Domain`. Verify in audit; create if not.)

- [ ] **Step 6: Run tests**

```bash
dotnet test --filter "FullyQualifiedName~PhotoBatchUploadTests"
```

Expected: 3 PASS.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/
git add tests/Api.Tests/DocumentProcessing/PhotoIngestion/PhotoBatchUploadTests.cs
git commit -m "feat(document-processing): PhotoBatchUpload aggregate + PhotoBatchPage + domain events"
```

### Task 1.2 — PhotoBatchUpload repository + EF migration

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Repositories/IPhotoBatchUploadRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Persistence/PhotoBatchUploadRepository.cs`
- Create: `apps/api/src/Api/Migrations/{yyyyMMddHHmmss}_AddPhotoIngestion.cs`

- [ ] **Step 1: Define repository interface**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Repositories/IPhotoBatchUploadRepository.cs
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Repositories;

public interface IPhotoBatchUploadRepository : IRepository<PhotoBatchUpload, Guid>
{
    Task<IReadOnlyList<PhotoBatchUpload>> FindByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<PhotoBatchUpload?> FindByIdWithPagesAsync(Guid id, CancellationToken ct = default);
}
```

- [ ] **Step 2: Implementation**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Persistence/PhotoBatchUploadRepository.cs
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Infrastructure.Persistence;  // adjust to actual DbContext namespace verified in spike
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;

public sealed class PhotoBatchUploadRepository : IPhotoBatchUploadRepository
{
    private readonly AppDbContext _db;

    public PhotoBatchUploadRepository(AppDbContext db) => _db = db;

    public async Task<PhotoBatchUpload?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.PhotoBatchUploads.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<PhotoBatchUpload?> FindByIdWithPagesAsync(Guid id, CancellationToken ct = default) =>
        await _db.PhotoBatchUploads
            .Include(b => b.Pages)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<IReadOnlyList<PhotoBatchUpload>> FindByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await _db.PhotoBatchUploads
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

    public Task<PhotoBatchUpload> AddAsync(PhotoBatchUpload entity, CancellationToken ct = default)
    {
        _db.PhotoBatchUploads.Add(entity);
        return Task.FromResult(entity);
    }

    public Task UpdateAsync(PhotoBatchUpload entity, CancellationToken ct = default)
    {
        _db.PhotoBatchUploads.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(PhotoBatchUpload entity, CancellationToken ct = default)
    {
        _db.PhotoBatchUploads.Remove(entity);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 3: EF entity configuration**

Add to `AppDbContext` (or appropriate context):

```csharp
modelBuilder.Entity<PhotoBatchUpload>(b =>
{
    b.ToTable("photo_batch_uploads");
    b.HasKey(p => p.Id);
    b.Property(p => p.SourceLanguage).HasMaxLength(10).IsRequired();
    b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
    b.HasQueryFilter(p => !p.IsDeleted);
    b.HasIndex(p => p.UserId);
    b.HasIndex(p => new { p.GameId, p.Status });
    b.Property(p => p.RowVersion).IsRowVersion();
    b.Ignore(p => p.DomainEvents);  // domain events not persisted

    b.HasMany(p => p.Pages)
        .WithOne()
        .HasForeignKey(pg => pg.PhotoBatchUploadId)
        .OnDelete(DeleteBehavior.Cascade);
});

modelBuilder.Entity<PhotoBatchPage>(b =>
{
    b.ToTable("photo_batch_pages");
    b.HasKey(p => p.Id);
    b.Property(p => p.BlobKey).HasMaxLength(500).IsRequired();
    b.Property(p => p.Orientation).HasConversion<string>().HasMaxLength(20);
    b.Property(p => p.ConfidenceLevel).HasConversion<string>().HasMaxLength(10);
    b.Property(p => p.ExtractedText).HasColumnType("text");
    b.Property(p => p.Warnings).HasColumnType("jsonb");
    b.HasIndex(p => p.PhotoBatchUploadId);
    b.HasIndex(p => new { p.PhotoBatchUploadId, p.PageNumber }).IsUnique();
});
```

- [ ] **Step 4: Generate migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddPhotoIngestion
```

Verify generated SQL:
- `photo_batch_uploads` table with FK to `users(Id)` ON DELETE CASCADE
- `photo_batch_pages` table with FK to `photo_batch_uploads(Id)` ON DELETE CASCADE
- Indices: `IX_photo_batch_uploads_UserId`, `IX_photo_batch_uploads_GameId_Status`, `IX_photo_batch_pages_PhotoBatchUploadId`, unique `IX_photo_batch_pages_PhotoBatchUploadId_PageNumber`

- [ ] **Step 5: DI registration**

In `BoundedContexts/DocumentProcessing/Infrastructure/DependencyInjection/DocumentProcessingServiceExtensions.cs`:

```csharp
services.AddScoped<IPhotoBatchUploadRepository, PhotoBatchUploadRepository>();
```

- [ ] **Step 6: Apply migration locally**

```bash
dotnet ef database update
```

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Repositories/
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Persistence/
git add apps/api/src/Api/Migrations/*_AddPhotoIngestion.cs
git commit -m "feat(document-processing): PhotoBatchUpload repository + EF migration"
```

### Task 1.3 — UploadPhotoBatchCommand + Validator

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPhotoBatchCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Validators/UploadPhotoBatchCommandValidator.cs`
- Test: `tests/Api.Tests/DocumentProcessing/PhotoIngestion/UploadPhotoBatchCommandValidatorTests.cs`

- [ ] **Step 1: Write failing validator test**

```csharp
// tests/Api.Tests/DocumentProcessing/PhotoIngestion/UploadPhotoBatchCommandValidatorTests.cs
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

public class UploadPhotoBatchCommandValidatorTests
{
    private readonly UploadPhotoBatchCommandValidator _validator = new();

    private static string MakeBase64(int sizeBytes) =>
        Convert.ToBase64String(new byte[sizeBytes]);

    [Fact]
    public void Validate_EmptyPhotoList_ShouldHaveError()
    {
        var cmd = new UploadPhotoBatchCommand(
            UserId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            SourceLanguage: "en",
            Photos: Array.Empty<PhotoUploadDto>());

        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.Photos);
    }

    [Fact]
    public void Validate_TooManyPhotos_ShouldHaveError()
    {
        var photos = Enumerable.Range(0, 201)
            .Select(i => new PhotoUploadDto($"p{i}.jpg", MakeBase64(1024)))
            .ToArray();
        var cmd = new UploadPhotoBatchCommand(Guid.NewGuid(), Guid.NewGuid(), "en", photos);

        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.Photos);
    }

    [Fact]
    public void Validate_PhotoExceeds10MB_ShouldHaveError()
    {
        var bigPhoto = new PhotoUploadDto("p1.jpg", MakeBase64(11 * 1024 * 1024));
        var cmd = new UploadPhotoBatchCommand(Guid.NewGuid(), Guid.NewGuid(), "en", new[] { bigPhoto });

        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor("Photos[0].Base64Content");
    }

    [Fact]
    public void Validate_UnsupportedLanguage_ShouldHaveError()
    {
        var cmd = new UploadPhotoBatchCommand(
            Guid.NewGuid(), Guid.NewGuid(), "ja",
            new[] { new PhotoUploadDto("p1.jpg", MakeBase64(1024)) });

        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.SourceLanguage);
    }

    [Fact]
    public void Validate_ItalianAsSourceLang_ShouldWarnNoTranslationNeeded()
    {
        var cmd = new UploadPhotoBatchCommand(
            Guid.NewGuid(), Guid.NewGuid(), "it",
            new[] { new PhotoUploadDto("p1.jpg", MakeBase64(1024)) });

        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(c => c.SourceLanguage);
        // Italian is valid as source — translation will be no-op for narrative paragraphs
    }

    [Fact]
    public void Validate_InvalidBase64_ShouldHaveError()
    {
        var cmd = new UploadPhotoBatchCommand(
            Guid.NewGuid(), Guid.NewGuid(), "en",
            new[] { new PhotoUploadDto("p1.jpg", "not!valid!base64!") });

        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor("Photos[0].Base64Content");
    }

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveErrors()
    {
        var cmd = new UploadPhotoBatchCommand(
            Guid.NewGuid(), Guid.NewGuid(), "en",
            new[] { new PhotoUploadDto("p1.jpg", MakeBase64(50_000)) });

        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }
}
```

- [ ] **Step 2: Run test (BUILD FAIL)**

```bash
dotnet test --filter "FullyQualifiedName~UploadPhotoBatchCommandValidatorTests"
```

Expected: BUILD FAIL.

- [ ] **Step 3: Define command (record) + DTO**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPhotoBatchCommand.cs
using Api.SharedKernel.Application.Mediator;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

public sealed record UploadPhotoBatchCommand(
    Guid UserId,
    Guid GameId,
    string SourceLanguage,
    PhotoUploadDto[] Photos
) : ICommand<UploadPhotoBatchResult>;

public sealed record PhotoUploadDto(string Filename, string Base64Content);

public sealed record UploadPhotoBatchResult(Guid BatchId, int AcceptedCount);
```

(Assumes `ICommand<TResult>` exists in `Api.SharedKernel.Application.Mediator`. Verify in audit.)

- [ ] **Step 4: Implement validator**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Validators/UploadPhotoBatchCommandValidator.cs
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

public sealed class UploadPhotoBatchCommandValidator : AbstractValidator<UploadPhotoBatchCommand>
{
    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "en", "it", "de", "fr", "es", "pt", "nl"
    };

    private const int MaxPhotosPerBatch = 200;
    private const long MaxBase64SizeBytes = 14_000_000; // ~10 MB binary = ~14 MB base64

    public UploadPhotoBatchCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.GameId).NotEmpty();

        RuleFor(c => c.SourceLanguage)
            .NotEmpty()
            .Must(lang => SupportedLanguages.Contains(lang))
            .WithMessage("Source language must be one of: en, it, de, fr, es, pt, nl");

        RuleFor(c => c.Photos)
            .NotEmpty().WithMessage("At least one photo required")
            .Must(photos => photos.Length <= MaxPhotosPerBatch)
            .WithMessage($"Maximum {MaxPhotosPerBatch} photos per batch");

        RuleForEach(c => c.Photos).ChildRules(photo =>
        {
            photo.RuleFor(p => p.Filename).NotEmpty();
            photo.RuleFor(p => p.Base64Content)
                .NotEmpty()
                .Must(BeValidBase64).WithMessage("Invalid base64 content")
                .Must(s => s.Length <= MaxBase64SizeBytes)
                .WithMessage($"Photo exceeds maximum size of 10 MB");
        });
    }

    private static bool BeValidBase64(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        try
        {
            Convert.FromBase64String(s);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
```

- [ ] **Step 5: Run tests**

```bash
dotnet test --filter "FullyQualifiedName~UploadPhotoBatchCommandValidatorTests"
```

Expected: 7 PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPhotoBatchCommand.cs
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Validators/UploadPhotoBatchCommandValidator.cs
git add tests/Api.Tests/DocumentProcessing/PhotoIngestion/UploadPhotoBatchCommandValidatorTests.cs
git commit -m "feat(document-processing): UploadPhotoBatchCommand + FluentValidation with size limits"
```

### Task 1.4a — IPhotoPreprocessor interface + smoldocling endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IPhotoPreprocessor.cs`
- Modify: `apps/smoldocling-service/main.py` (add `/preprocess` endpoint)
- Modify: `apps/smoldocling-service/requirements.txt` (add `imutils`, `numpy`, `opencv-python-headless`)

- [ ] **Step 1: Define preprocessor interface (with ExtractedText FIELD)**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IPhotoPreprocessor.cs
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

public interface IPhotoPreprocessor
{
    Task<PhotoPreprocessResult> PreprocessAsync(byte[] imageData, CancellationToken ct = default);
}

public sealed record PhotoPreprocessResult(
    byte[] ProcessedImage,
    string ExtractedText,           // ← BUG fix: was missing in v1
    double ConfidenceScore,
    PageOrientation DetectedOrientation,
    bool IsBlankPage,
    string[] Warnings
);
```

- [ ] **Step 2: Add Python dependencies**

```text
# apps/smoldocling-service/requirements.txt (additions)
imutils==0.5.4
numpy==1.26.4
opencv-python-headless==4.10.0.84
Pillow==10.4.0
```

- [ ] **Step 3: Add `/preprocess` endpoint**

```python
# apps/smoldocling-service/main.py (additions)
import base64
import io
from typing import Optional

import cv2
import numpy as np
from fastapi import FastAPI, File, Form, UploadFile
from imutils.perspective import four_point_transform
from PIL import Image
from pydantic import BaseModel

# Existing app initialization assumed
# ...

class PreprocessResponse(BaseModel):
    processed_image_base64: str
    extracted_text: str
    confidence: float
    orientation: str
    is_blank: bool
    warnings: list[str]


def dewarp_image(img: Image.Image) -> Image.Image:
    """Detect page edges + apply perspective transform to rectify."""
    cv_img = cv2.cvtColor(np.array(img), cv2.COLOR_RGB2BGR)
    gray = cv2.cvtColor(cv_img, cv2.COLOR_BGR2GRAY)
    blurred = cv2.GaussianBlur(gray, (5, 5), 0)
    edges = cv2.Canny(blurred, 75, 200)

    contours, _ = cv2.findContours(edges, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    if not contours:
        return img

    page_contour = max(contours, key=cv2.contourArea)
    peri = cv2.arcLength(page_contour, True)
    approx = cv2.approxPolyDP(page_contour, 0.02 * peri, True)

    if len(approx) == 4:
        try:
            warped = four_point_transform(cv_img, approx.reshape(4, 2))
            return Image.fromarray(cv2.cvtColor(warped, cv2.COLOR_BGR2RGB))
        except Exception:
            return img

    return img


def detect_orientation(img: Image.Image) -> str:
    """Detect page orientation using image dimensions + EXIF metadata."""
    width, height = img.size
    if hasattr(img, "_getexif") and img._getexif():
        exif = img._getexif()
        # EXIF orientation tag = 274
        ori_tag = exif.get(274) if exif else None
        if ori_tag in (3, 4):
            return "rotated"
        if ori_tag in (5, 6, 7, 8):
            return "landscape" if width > height else "portrait"

    if width > height * 1.2:
        return "landscape"
    if height > width * 1.2:
        return "portrait"
    return "unknown"


def detect_blank_page(img: Image.Image, std_threshold: float = 5.0) -> bool:
    """Detect mostly-blank pages via pixel intensity standard deviation."""
    cv_img = np.array(img.convert("L"))  # grayscale
    return float(cv_img.std()) < std_threshold


def compute_ocr_confidence(img: Image.Image, ocr_engine) -> tuple[float, str]:
    """Run OCR and compute average word-level confidence."""
    # ocr_engine is the existing smoldocling instance — adjust to actual API
    result = ocr_engine.extract_text_with_confidence(img)
    return result.avg_confidence, result.text


@app.post("/preprocess", response_model=PreprocessResponse)
async def preprocess_photo(
    image: UploadFile = File(...),
    preprocessing_mode: str = Form("photo-camera"),
):
    img_bytes = await image.read()
    img = Image.open(io.BytesIO(img_bytes))

    warnings: list[str] = []

    if preprocessing_mode == "photo-camera":
        img = dewarp_image(img)
        orientation = detect_orientation(img)
        is_blank = detect_blank_page(img)
        try:
            confidence, extracted_text = compute_ocr_confidence(img, ocr_engine)
        except Exception as e:
            confidence = 0.0
            extracted_text = ""
            warnings.append(f"OCR error: {e}")

        if confidence < 0.5:
            warnings.append("Low OCR confidence - possibly blurry or low light")
        if is_blank:
            warnings.append("Page appears blank")

        buf = io.BytesIO()
        img.save(buf, format="JPEG", quality=92)
        processed_b64 = base64.b64encode(buf.getvalue()).decode()

        return PreprocessResponse(
            processed_image_base64=processed_b64,
            extracted_text=extracted_text,
            confidence=confidence,
            orientation=orientation,
            is_blank=is_blank,
            warnings=warnings,
        )

    # Default mode (no photo-camera preprocessing): pass-through
    extracted_text = ocr_engine.extract_text(img)
    confidence = 0.95  # baseline for clean PDFs
    return PreprocessResponse(
        processed_image_base64=base64.b64encode(img_bytes).decode(),
        extracted_text=extracted_text,
        confidence=confidence,
        orientation="portrait",
        is_blank=False,
        warnings=[],
    )
```

(Note: `ocr_engine` actual API depends on existing smoldocling-service implementation. Adjust calls to match.)

- [ ] **Step 4: Update Dockerfile to install OpenCV system deps**

```dockerfile
# apps/smoldocling-service/Dockerfile (additions)
RUN apt-get update && apt-get install -y \
    libgl1-mesa-glx libglib2.0-0 libsm6 libxext6 libxrender-dev \
    && rm -rf /var/lib/apt/lists/*
```

- [ ] **Step 5: Pytest for /preprocess endpoint**

```python
# apps/smoldocling-service/tests/test_preprocess.py
import io
from pathlib import Path

import pytest
from fastapi.testclient import TestClient
from PIL import Image

from main import app

client = TestClient(app)
FIXTURES = Path(__file__).parent / "fixtures"


def test_preprocess_clear_page_returns_high_confidence():
    img_path = FIXTURES / "clear-page.jpg"
    with img_path.open("rb") as f:
        response = client.post(
            "/preprocess",
            files={"image": ("page.jpg", f, "image/jpeg")},
            data={"preprocessing_mode": "photo-camera"},
        )
    assert response.status_code == 200
    payload = response.json()
    assert payload["confidence"] >= 0.85
    assert not payload["is_blank"]


def test_preprocess_blank_page_detected():
    blank = Image.new("RGB", (800, 1000), color="white")
    buf = io.BytesIO()
    blank.save(buf, format="JPEG")
    buf.seek(0)
    response = client.post(
        "/preprocess",
        files={"image": ("blank.jpg", buf.read(), "image/jpeg")},
        data={"preprocessing_mode": "photo-camera"},
    )
    assert response.status_code == 200
    payload = response.json()
    assert payload["is_blank"] is True
```

- [ ] **Step 6: Run pytest**

```bash
cd apps/smoldocling-service
pip install -r requirements.txt
pytest tests/test_preprocess.py -v
```

Expected: 2 PASS (provide actual `clear-page.jpg` fixture).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IPhotoPreprocessor.cs
git add apps/smoldocling-service/main.py apps/smoldocling-service/requirements.txt apps/smoldocling-service/Dockerfile
git add apps/smoldocling-service/tests/test_preprocess.py
git commit -m "feat(smoldocling): /preprocess endpoint with dewarping + confidence + IPhotoPreprocessor interface"
```

### Task 1.4b — SmoldoclingPhotoPreprocessor (HTTP client implementation)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/SmoldoclingPhotoPreprocessor.cs`
- Test: `tests/Api.Tests/DocumentProcessing/PhotoIngestion/SmoldoclingPhotoPreprocessorTests.cs`

- [ ] **Step 1: Write failing integration test with InMemory ConfigurationBuilder**

```csharp
// tests/Api.Tests/DocumentProcessing/PhotoIngestion/SmoldoclingPhotoPreprocessorTests.cs
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

public class SmoldoclingPhotoPreprocessorTests
{
    [Fact]
    public async Task PreprocessAsync_ServiceReturnsHighConfidence_ParsesCorrectly()
    {
        var stubResponse = """
        {
          "processed_image_base64": "dGVzdA==",
          "extracted_text": "Hello world",
          "confidence": 0.92,
          "orientation": "portrait",
          "is_blank": false,
          "warnings": []
        }
        """;
        var handler = new StubHttpMessageHandler(System.Net.HttpStatusCode.OK, stubResponse);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("smoldocling-service").Returns(httpClient);

        var preprocessor = new SmoldoclingPhotoPreprocessor(factory, NullLogger<SmoldoclingPhotoPreprocessor>.Instance);

        var result = await preprocessor.PreprocessAsync(new byte[] { 0x01, 0x02 });

        result.ConfidenceScore.Should().Be(0.92);
        result.ExtractedText.Should().Be("Hello world");
        result.IsBlankPage.Should().BeFalse();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task PreprocessAsync_ServiceReturns500_ThrowsHttpRequestException()
    {
        var handler = new StubHttpMessageHandler(System.Net.HttpStatusCode.InternalServerError, "{}");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("smoldocling-service").Returns(httpClient);

        var preprocessor = new SmoldoclingPhotoPreprocessor(factory, NullLogger<SmoldoclingPhotoPreprocessor>.Instance);

        Func<Task> act = () => preprocessor.PreprocessAsync(new byte[] { 0x01 });
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly System.Net.HttpStatusCode _status;
        private readonly string _content;

        public StubHttpMessageHandler(System.Net.HttpStatusCode status, string content)
        {
            _status = status;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_content, System.Text.Encoding.UTF8, "application/json")
            });
    }
}
```

- [ ] **Step 2: Implement preprocessor**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/SmoldoclingPhotoPreprocessor.cs
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

public sealed class SmoldoclingPhotoPreprocessor : IPhotoPreprocessor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SmoldoclingPhotoPreprocessor> _logger;

    public SmoldoclingPhotoPreprocessor(IHttpClientFactory factory, ILogger<SmoldoclingPhotoPreprocessor> logger)
    {
        _httpClient = factory.CreateClient("smoldocling-service");
        _logger = logger;
    }

    public async Task<PhotoPreprocessResult> PreprocessAsync(byte[] imageData, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(imageData);
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(imageContent, "image", "page.jpg");
        content.Add(new StringContent("photo-camera"), "preprocessing_mode");

        try
        {
            var response = await _httpClient.PostAsync("/preprocess", content, ct);
            response.EnsureSuccessStatusCode();

            var dto = await response.Content.ReadFromJsonAsync<SmoldoclingPreprocessDto>(cancellationToken: ct)
                ?? throw new InvalidOperationException("Empty response from smoldocling-service");

            return new PhotoPreprocessResult(
                ProcessedImage: Convert.FromBase64String(dto.ProcessedImageBase64),
                ExtractedText: dto.ExtractedText,
                ConfidenceScore: dto.Confidence,
                DetectedOrientation: ParseOrientation(dto.Orientation),
                IsBlankPage: dto.IsBlank,
                Warnings: dto.Warnings ?? Array.Empty<string>()
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Smoldocling preprocess failed");
            throw;
        }
    }

    private static PageOrientation ParseOrientation(string s) => s.ToLowerInvariant() switch
    {
        "portrait" => PageOrientation.Portrait,
        "landscape" => PageOrientation.Landscape,
        "rotated" => PageOrientation.Rotated,
        _ => PageOrientation.Unknown
    };

    private sealed record SmoldoclingPreprocessDto(
        string ProcessedImageBase64,
        string ExtractedText,
        double Confidence,
        string Orientation,
        bool IsBlank,
        string[]? Warnings);
}
```

- [ ] **Step 3: HttpClient registration in DI**

In `Infrastructure/DependencyInjection/DocumentProcessingServiceExtensions.cs`:

```csharp
services.AddHttpClient("smoldocling-service", client =>
{
    var baseUrl = configuration["SMOLDOCLING_SERVICE_URL"] ?? "http://smoldocling-service:8500";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3, retry => TimeSpan.FromSeconds(Math.Pow(2, retry))));
// (If Polly is not currently used in MeepleAI, add Polly.Extensions.Http NuGet package)

services.AddScoped<IPhotoPreprocessor, SmoldoclingPhotoPreprocessor>();
```

- [ ] **Step 4: Run tests**

```bash
dotnet test --filter "FullyQualifiedName~SmoldoclingPhotoPreprocessorTests"
```

Expected: 2 PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/SmoldoclingPhotoPreprocessor.cs
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/DependencyInjection/
git add tests/Api.Tests/DocumentProcessing/PhotoIngestion/SmoldoclingPhotoPreprocessorTests.cs
git commit -m "feat(document-processing): SmoldoclingPhotoPreprocessor HTTP client + DI + retry policy"
```

### Task 1.5 — UploadPhotoBatchCommandHandler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPhotoBatchCommandHandler.cs`
- Test: `tests/Api.Tests/DocumentProcessing/PhotoIngestion/UploadPhotoBatchCommandHandlerTests.cs`

- [ ] **Step 1: Write failing handler test with NSubstitute**

```csharp
// tests/Api.Tests/DocumentProcessing/PhotoIngestion/UploadPhotoBatchCommandHandlerTests.cs
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Services.Pdf;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

public class UploadPhotoBatchCommandHandlerTests
{
    private readonly IPhotoBatchUploadRepository _repo = Substitute.For<IPhotoBatchUploadRepository>();
    private readonly IBlobStorageService _blob = Substitute.For<IBlobStorageService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly UploadPhotoBatchCommandHandler _sut;

    public UploadPhotoBatchCommandHandlerTests()
    {
        _sut = new UploadPhotoBatchCommandHandler(_repo, _blob, _uow, _mediator, NullLogger<UploadPhotoBatchCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ValidBatch_StoresPhotosAndQueuesProcessJob()
    {
        var photoBytes = new byte[] { 0x01, 0x02 };
        var base64 = Convert.ToBase64String(photoBytes);
        var cmd = new UploadPhotoBatchCommand(
            UserId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            SourceLanguage: "en",
            Photos: new[] { new PhotoUploadDto("p1.jpg", base64) });

        _blob.StoreAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new BlobStorageResult { FileId = Guid.NewGuid().ToString(), ContentHash = "abc" });

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.BatchId.Should().NotBeEmpty();
        result.AcceptedCount.Should().Be(1);

        await _repo.Received(1).AddAsync(Arg.Any<PhotoBatchUpload>(), Arg.Any<CancellationToken>());
        await _blob.Received(1).StoreAsync(Arg.Any<Stream>(), Arg.Any<string>(), cmd.GameId, Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(Arg.Is<EnqueuePhotoBatchProcessingCommand>(c => c.BatchId == result.BatchId), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Implement handler using verified MeepleAI patterns**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPhotoBatchCommandHandler.cs
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Application.Mediator;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Services.Pdf;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

public sealed class UploadPhotoBatchCommandHandler : ICommandHandler<UploadPhotoBatchCommand, UploadPhotoBatchResult>
{
    private readonly IPhotoBatchUploadRepository _repo;
    private readonly IBlobStorageService _blob;
    private readonly IUnitOfWork _uow;
    private readonly IMediator _mediator;
    private readonly ILogger<UploadPhotoBatchCommandHandler> _logger;

    public UploadPhotoBatchCommandHandler(
        IPhotoBatchUploadRepository repo,
        IBlobStorageService blob,
        IUnitOfWork uow,
        IMediator mediator,
        ILogger<UploadPhotoBatchCommandHandler> logger)
    {
        _repo = repo;
        _blob = blob;
        _uow = uow;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<UploadPhotoBatchResult> Handle(UploadPhotoBatchCommand cmd, CancellationToken ct)
    {
        var batch = PhotoBatchUpload.Create(cmd.UserId, cmd.GameId, cmd.SourceLanguage, cmd.Photos.Length);
        await _repo.AddAsync(batch, ct);

        // Save batch first to obtain ID for blob keys
        await _uow.SaveChangesAsync(ct);

        for (var i = 0; i < cmd.Photos.Length; i++)
        {
            var photo = cmd.Photos[i];
            var photoBytes = Convert.FromBase64String(photo.Base64Content);
            var fileName = $"photo-batch-{batch.Id}-page-{i:D3}.jpg";

            using var stream = new MemoryStream(photoBytes);
            await _blob.StoreAsync(stream, fileName, batch.GameId, ct);
        }

        // Queue background processing via MediatR enqueue command (existing pattern)
        await _mediator.Send(new EnqueuePhotoBatchProcessingCommand(batch.Id), ct);

        _logger.LogInformation("Photo batch {BatchId} created with {PhotoCount} photos for game {GameId}",
            batch.Id, cmd.Photos.Length, cmd.GameId);

        return new UploadPhotoBatchResult(batch.Id, cmd.Photos.Length);
    }
}

public sealed record EnqueuePhotoBatchProcessingCommand(Guid BatchId) : ICommand<Unit>;
```

- [ ] **Step 3: Implement enqueue handler (uses IBackgroundTaskService pattern)**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/EnqueuePhotoBatchProcessingCommandHandler.cs
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.SharedKernel.Application.Mediator;
using Api.Services;  // IBackgroundTaskService
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

public sealed class EnqueuePhotoBatchProcessingCommandHandler : ICommandHandler<EnqueuePhotoBatchProcessingCommand, Unit>
{
    private readonly IBackgroundTaskService _backgroundTasks;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EnqueuePhotoBatchProcessingCommandHandler> _logger;

    public EnqueuePhotoBatchProcessingCommandHandler(
        IBackgroundTaskService backgroundTasks,
        IServiceScopeFactory scopeFactory,
        ILogger<EnqueuePhotoBatchProcessingCommandHandler> logger)
    {
        _backgroundTasks = backgroundTasks;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task<Unit> Handle(EnqueuePhotoBatchProcessingCommand cmd, CancellationToken ct)
    {
        var taskId = $"photo-batch-{cmd.BatchId}";
        _backgroundTasks.ExecuteWithCancellation(taskId, async (scopedCt) =>
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IPhotoBatchProcessor>();
            try
            {
                await processor.ProcessAsync(cmd.BatchId, scopedCt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Photo batch {BatchId} processing failed", cmd.BatchId);
            }
        });

        return Task.FromResult(Unit.Value);
    }
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test --filter "FullyQualifiedName~UploadPhotoBatchCommandHandlerTests"
```

Expected: 1 PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/
git add tests/Api.Tests/DocumentProcessing/PhotoIngestion/UploadPhotoBatchCommandHandlerTests.cs
git commit -m "feat(document-processing): UploadPhotoBatchCommandHandler + EnqueuePhotoBatchProcessing using IBackgroundTaskService"
```

### Task 1.6 — IPhotoBatchProcessor (parallel processing service)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IPhotoBatchProcessor.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/PhotoBatchProcessor.cs`
- Test: `tests/Api.Tests/DocumentProcessing/PhotoIngestion/PhotoBatchProcessorTests.cs`

- [ ] **Step 1: Define service interface**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IPhotoBatchProcessor.cs
namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

public interface IPhotoBatchProcessor
{
    Task ProcessAsync(Guid batchId, CancellationToken ct);
}
```

- [ ] **Step 2: Implementation with bounded parallelism**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/PhotoBatchProcessor.cs
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Services;  // IDocumentChunker, IEmbeddingService — verify in audit
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Services.Pdf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

public sealed class PhotoBatchProcessor : IPhotoBatchProcessor
{
    private readonly IPhotoBatchUploadRepository _repo;
    private readonly IBlobStorageService _blob;
    private readonly IPhotoPreprocessor _preprocessor;
    private readonly IDocumentChunker _chunker;
    private readonly IEmbeddingService _embeddings;
    private readonly IKnowledgeBaseIndexer _kbIndexer;  // wraps pgvector insert
    private readonly IUnitOfWork _uow;
    private readonly int _maxParallelism;
    private readonly ILogger<PhotoBatchProcessor> _logger;

    public PhotoBatchProcessor(
        IPhotoBatchUploadRepository repo,
        IBlobStorageService blob,
        IPhotoPreprocessor preprocessor,
        IDocumentChunker chunker,
        IEmbeddingService embeddings,
        IKnowledgeBaseIndexer kbIndexer,
        IUnitOfWork uow,
        IConfiguration config,
        ILogger<PhotoBatchProcessor> logger)
    {
        _repo = repo;
        _blob = blob;
        _preprocessor = preprocessor;
        _chunker = chunker;
        _embeddings = embeddings;
        _kbIndexer = kbIndexer;
        _uow = uow;
        _maxParallelism = config.GetValue<int?>("PhotoBatch:MaxParallelism") ?? 4;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid batchId, CancellationToken ct)
    {
        var batch = await _repo.FindByIdWithPagesAsync(batchId, ct)
            ?? throw new InvalidOperationException($"Batch {batchId} not found");

        batch.StartProcessing();
        await _uow.SaveChangesAsync(ct);

        // List blob keys for this batch (assume STORE convention)
        var blobKeys = Enumerable.Range(0, batch.TotalPages)
            .Select(i => $"photo-batch-{batchId}-page-{i:D3}.jpg")
            .ToList();

        var semaphore = new SemaphoreSlim(_maxParallelism);
        var tasks = blobKeys.Select(async (blobKey, index) =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                await ProcessSinglePageAsync(batch, blobKey, index, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Page {PageIndex} of batch {BatchId} failed", index, batchId);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Batch {BatchId} processing complete: {Indexed}/{Total}",
            batchId, batch.IndexedPages, batch.TotalPages);
    }

    private async Task ProcessSinglePageAsync(PhotoBatchUpload batch, string blobKey, int pageIndex, CancellationToken ct)
    {
        var stream = await _blob.RetrieveAsync(blobKey, batch.GameId, ct);
        if (stream is null)
        {
            _logger.LogWarning("Blob {BlobKey} not found", blobKey);
            return;
        }

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var imageData = ms.ToArray();

        var preprocessed = await _preprocessor.PreprocessAsync(imageData, ct);

        var page = PhotoBatchPage.Create(
            batchId: batch.Id,
            pageNumber: pageIndex + 1,
            blobKey: blobKey,
            confidence: preprocessed.ConfidenceScore,
            orientation: preprocessed.DetectedOrientation,
            isBlank: preprocessed.IsBlankPage,
            warnings: preprocessed.Warnings,
            extractedText: preprocessed.ExtractedText);

        batch.AttachPage(page);

        // Chunk + embed + index in KB
        if (!preprocessed.IsBlankPage && !string.IsNullOrWhiteSpace(preprocessed.ExtractedText))
        {
            var chunks = _chunker.ChunkText(preprocessed.ExtractedText, chunkSize: 512, overlap: 50);
            foreach (var chunk in chunks)
            {
                var embedding = await _embeddings.GenerateEmbeddingAsync(chunk, ct);
                await _kbIndexer.IndexChunkAsync(new KnowledgeChunk
                {
                    GameId = batch.GameId,
                    Source = "phone_camera",
                    PhotoBatchId = batch.Id,
                    PageNumber = page.PageNumber,
                    Text = chunk,
                    Embedding = embedding,
                    Confidence = preprocessed.ConfidenceScore,
                    SourceLanguage = batch.SourceLanguage
                }, ct);
            }
        }

        batch.RecordPageIndexed(page.PageNumber, preprocessed.ConfidenceScore, preprocessed.Warnings);
    }
}
```

(Note: `IDocumentChunker`, `IEmbeddingService`, `IKnowledgeBaseIndexer` reference existing or to-be-created services from KnowledgeBase BC. Verify in spike + create if missing. `KnowledgeChunk` DTO TBD in KB BC.)

- [ ] **Step 3: Test with NSubstitute mocks**

```csharp
// tests/Api.Tests/DocumentProcessing/PhotoIngestion/PhotoBatchProcessorTests.cs
public class PhotoBatchProcessorTests
{
    [Fact]
    public async Task ProcessAsync_BatchOf3Pages_IndexesAllPages()
    {
        var batchId = Guid.NewGuid();
        var batch = PhotoBatchUpload.Create(Guid.NewGuid(), Guid.NewGuid(), "en", 3);
        SetPrivateField(batch, "Id", batchId);

        var repo = Substitute.For<IPhotoBatchUploadRepository>();
        repo.FindByIdWithPagesAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);

        var blob = Substitute.For<IBlobStorageService>();
        blob.RetrieveAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(new byte[] { 1, 2, 3 }));

        var preprocessor = Substitute.For<IPhotoPreprocessor>();
        preprocessor.PreprocessAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new PhotoPreprocessResult(
                ProcessedImage: new byte[] { 1 },
                ExtractedText: "Sample page text",
                ConfidenceScore: 0.9,
                DetectedOrientation: PageOrientation.Portrait,
                IsBlankPage: false,
                Warnings: Array.Empty<string>()));

        var chunker = Substitute.For<IDocumentChunker>();
        chunker.ChunkText("Sample page text", 512, 50).Returns(new[] { "Sample page text" });

        var embeddings = Substitute.For<IEmbeddingService>();
        embeddings.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new float[] { 0.1f, 0.2f });

        var indexer = Substitute.For<IKnowledgeBaseIndexer>();
        var uow = Substitute.For<IUnitOfWork>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PhotoBatch:MaxParallelism"] = "4"
        }).Build();

        var sut = new PhotoBatchProcessor(repo, blob, preprocessor, chunker, embeddings, indexer, uow, config,
            NullLogger<PhotoBatchProcessor>.Instance);

        await sut.ProcessAsync(batchId, CancellationToken.None);

        await indexer.Received(3).IndexChunkAsync(Arg.Any<KnowledgeChunk>(), Arg.Any<CancellationToken>());
        batch.IndexedPages.Should().Be(3);
        batch.Status.Should().Be(PhotoBatchStatus.Completed);
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IPhotoBatchProcessor.cs
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/PhotoBatchProcessor.cs
git add tests/Api.Tests/DocumentProcessing/PhotoIngestion/PhotoBatchProcessorTests.cs
git commit -m "feat(document-processing): PhotoBatchProcessor with bounded parallelism + KB indexing"
```

### Task 1.7 — GetPhotoBatchStatusQuery + handler + endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetPhotoBatchStatusQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetPhotoBatchStatusQueryHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/DTOs/PhotoBatchStatusDto.cs`
- Create: `apps/api/src/Api/Routing/PhotoIngestionEndpoints.cs`
- Test: `tests/Api.Tests/DocumentProcessing/PhotoIngestion/GetPhotoBatchStatusQueryHandlerTests.cs`

- [ ] **Step 1: Define DTO**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/DTOs/PhotoBatchStatusDto.cs
namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

public sealed record PhotoBatchStatusDto(
    Guid BatchId,
    string Status,
    int TotalPages,
    int IndexedPages,
    int LowConfidencePages,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    PhotoPageStatusDto[] Pages
);

public sealed record PhotoPageStatusDto(
    int PageNumber,
    string ThumbnailUrl,
    double Confidence,
    string ConfidenceLevel,  // "High" | "Medium" | "Low"
    string[] Warnings
);
```

- [ ] **Step 2: Define query + handler**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetPhotoBatchStatusQuery.cs
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Mediator;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

public sealed record GetPhotoBatchStatusQuery(Guid UserId, Guid BatchId) : IQuery<PhotoBatchStatusDto>;
```

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetPhotoBatchStatusQueryHandler.cs
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;  // verify exception types in spike
using Api.SharedKernel.Application.Mediator;
using Api.Services.Pdf;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

public sealed class GetPhotoBatchStatusQueryHandler : IQueryHandler<GetPhotoBatchStatusQuery, PhotoBatchStatusDto>
{
    private readonly IPhotoBatchUploadRepository _repo;
    private readonly IBlobStorageService _blob;

    public GetPhotoBatchStatusQueryHandler(IPhotoBatchUploadRepository repo, IBlobStorageService blob)
    {
        _repo = repo;
        _blob = blob;
    }

    public async Task<PhotoBatchStatusDto> Handle(GetPhotoBatchStatusQuery query, CancellationToken ct)
    {
        var batch = await _repo.FindByIdWithPagesAsync(query.BatchId, ct)
            ?? throw new NotFoundException($"Batch {query.BatchId} not found");

        if (batch.UserId != query.UserId)
            throw new ForbiddenException("Cannot access another user's batch");

        var pageDtos = batch.Pages.Select(p => new PhotoPageStatusDto(
            PageNumber: p.PageNumber,
            ThumbnailUrl: _blob.GetPresignedDownloadUrlAsync(p.BlobKey, batch.GameId, expirySeconds: 900).Result ?? string.Empty,
            Confidence: p.Confidence,
            ConfidenceLevel: p.ConfidenceLevel.ToString(),
            Warnings: p.Warnings
        )).OrderBy(p => p.PageNumber).ToArray();

        return new PhotoBatchStatusDto(
            BatchId: batch.Id,
            Status: batch.Status.ToString(),
            TotalPages: batch.TotalPages,
            IndexedPages: batch.IndexedPages,
            LowConfidencePages: batch.Pages.Count(p => p.Confidence < 0.7),
            CreatedAt: batch.CreatedAt,
            CompletedAt: batch.CompletedAt,
            Pages: pageDtos
        );
    }
}
```

(Note: `_blob.GetPresignedDownloadUrlAsync` is async — switch to `await Task.WhenAll(...)` for parallel signed URL generation. Simplified above for clarity.)

- [ ] **Step 3: Endpoints with CQRS rule (only IMediator.Send)**

```csharp
// apps/api/src/Api/Routing/PhotoIngestionEndpoints.cs
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using MediatR;
using System.Security.Claims;

namespace Api.Routing;

public static class PhotoIngestionEndpoints
{
    public static IEndpointRouteBuilder MapPhotoIngestionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/photo-batches")
            .RequireAuthorization()
            .WithTags("Photo Ingestion");

        group.MapPost("/", async (
            UploadPhotoBatchRequest request,
            ClaimsPrincipal user,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var cmd = new UploadPhotoBatchCommand(
                UserId: userId,
                GameId: request.GameId,
                SourceLanguage: request.SourceLanguage,
                Photos: request.Photos);
            var result = await mediator.Send(cmd, ct);
            return Results.Ok(result);
        });

        group.MapGet("/{batchId:guid}", async (
            Guid batchId,
            ClaimsPrincipal user,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var query = new GetPhotoBatchStatusQuery(userId, batchId);
            var result = await mediator.Send(query, ct);
            return Results.Ok(result);
        });

        return app;
    }
}

public sealed record UploadPhotoBatchRequest(Guid GameId, string SourceLanguage, PhotoUploadDto[] Photos);
```

Wire in `Program.cs`: `app.MapPhotoIngestionEndpoints();`

- [ ] **Step 4: Test handler**

```csharp
public class GetPhotoBatchStatusQueryHandlerTests
{
    [Fact]
    public async Task Handle_OwnersOwnBatch_ReturnsStatus()
    {
        var userId = Guid.NewGuid();
        var batch = PhotoBatchUpload.Create(userId, Guid.NewGuid(), "en", 2);

        var repo = Substitute.For<IPhotoBatchUploadRepository>();
        repo.FindByIdWithPagesAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);

        var blob = Substitute.For<IBlobStorageService>();
        blob.GetPresignedDownloadUrlAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<int?>())
            .Returns(Task.FromResult<string?>("https://test/thumb.jpg"));

        var sut = new GetPhotoBatchStatusQueryHandler(repo, blob);
        var result = await sut.Handle(new GetPhotoBatchStatusQuery(userId, batch.Id), CancellationToken.None);

        result.Status.Should().Be("Pending");
        result.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task Handle_OtherUserBatch_ThrowsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var batch = PhotoBatchUpload.Create(ownerId, Guid.NewGuid(), "en", 1);

        var repo = Substitute.For<IPhotoBatchUploadRepository>();
        repo.FindByIdWithPagesAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);

        var sut = new GetPhotoBatchStatusQueryHandler(repo, Substitute.For<IBlobStorageService>());

        Func<Task> act = () => sut.Handle(new GetPhotoBatchStatusQuery(attackerId, batch.Id), CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/DTOs/
git add apps/api/src/Api/Routing/PhotoIngestionEndpoints.cs
git add tests/Api.Tests/DocumentProcessing/PhotoIngestion/GetPhotoBatchStatusQueryHandlerTests.cs
git commit -m "feat(document-processing): GetPhotoBatchStatus query + handler + endpoints"
```

### Task 1.8 — Frontend upload page (using existing HttpClient)

**Files:**
- Create: `apps/web/src/lib/gamebook/schemas.ts`
- Create: `apps/web/src/lib/gamebook/api.ts`
- Create: `apps/web/src/lib/gamebook/hooks/usePhotoBatchUpload.ts`
- Create: `apps/web/src/lib/gamebook/hooks/usePhotoBatchStatus.ts`
- Create: `apps/web/src/lib/gamebook/file-to-base64.ts`
- Create: `apps/web/src/app/(authenticated)/gamebook/upload/page.tsx`
- Create: `apps/web/src/app/(authenticated)/gamebook/upload/_components/PhotoUploader.tsx`
- Create: `apps/web/src/app/(authenticated)/gamebook/upload/_components/ConfidenceBadge.tsx`
- Create: `apps/web/src/i18n/locales/it/gamebook.json`
- Test: `apps/web/__tests__/gamebook/upload/PhotoUploader.test.tsx`

- [ ] **Step 1: Define Zod schemas (validation + type generation)**

```typescript
// apps/web/src/lib/gamebook/schemas.ts
import { z } from 'zod';

export const PhotoBatchStatusSchema = z.object({
  batchId: z.string().uuid(),
  status: z.enum(['Pending', 'Processing', 'Completed', 'Failed']),
  totalPages: z.number().int().nonnegative(),
  indexedPages: z.number().int().nonnegative(),
  lowConfidencePages: z.number().int().nonnegative(),
  createdAt: z.string(),
  completedAt: z.string().nullable(),
  pages: z.array(z.object({
    pageNumber: z.number().int(),
    thumbnailUrl: z.string().url(),
    confidence: z.number(),
    confidenceLevel: z.enum(['High', 'Medium', 'Low']),
    warnings: z.array(z.string()),
  })),
});
export type PhotoBatchStatus = z.infer<typeof PhotoBatchStatusSchema>;

export const UploadPhotoBatchResultSchema = z.object({
  batchId: z.string().uuid(),
  acceptedCount: z.number().int(),
});
export type UploadPhotoBatchResult = z.infer<typeof UploadPhotoBatchResultSchema>;
```

- [ ] **Step 2: API wrapper using existing HttpClient**

```typescript
// apps/web/src/lib/gamebook/api.ts
import { httpClient } from '@/lib/api/core/httpClient';
import {
  PhotoBatchStatusSchema,
  UploadPhotoBatchResultSchema,
  type PhotoBatchStatus,
  type UploadPhotoBatchResult,
} from './schemas';

export async function uploadPhotoBatch(input: {
  gameId: string;
  sourceLanguage: string;
  photos: { filename: string; base64Content: string }[];
}): Promise<UploadPhotoBatchResult> {
  const result = await httpClient.post(
    '/api/v1/photo-batches',
    input,
    UploadPhotoBatchResultSchema
  );
  if (!result) throw new Error('Empty upload response');
  return result;
}

export async function getPhotoBatchStatus(batchId: string): Promise<PhotoBatchStatus> {
  const result = await httpClient.get(
    `/api/v1/photo-batches/${batchId}`,
    PhotoBatchStatusSchema
  );
  if (!result) throw new Error('Empty status response');
  return result;
}
```

- [ ] **Step 3: File-to-base64 helper**

```typescript
// apps/web/src/lib/gamebook/file-to-base64.ts
export async function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const dataUrl = reader.result as string;
      // Strip "data:image/jpeg;base64," prefix
      const base64 = dataUrl.split(',', 2)[1];
      resolve(base64);
    };
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(file);
  });
}
```

- [ ] **Step 4: TanStack Query hooks**

```typescript
// apps/web/src/lib/gamebook/hooks/usePhotoBatchUpload.ts
import { useMutation } from '@tanstack/react-query';
import { uploadPhotoBatch } from '../api';
import { fileToBase64 } from '../file-to-base64';

export function useUploadPhotoBatch() {
  return useMutation({
    mutationFn: async (input: { gameId: string; sourceLanguage: string; photos: File[] }) => {
      const photosBase64 = await Promise.all(
        input.photos.map(async (file) => ({
          filename: file.name,
          base64Content: await fileToBase64(file),
        }))
      );
      return uploadPhotoBatch({
        gameId: input.gameId,
        sourceLanguage: input.sourceLanguage,
        photos: photosBase64,
      });
    },
  });
}
```

```typescript
// apps/web/src/lib/gamebook/hooks/usePhotoBatchStatus.ts
import { useQuery } from '@tanstack/react-query';
import { getPhotoBatchStatus } from '../api';

export function usePhotoBatchStatus(batchId: string | undefined) {
  return useQuery({
    queryKey: ['photo-batch-status', batchId],
    queryFn: () => getPhotoBatchStatus(batchId!),
    enabled: !!batchId,
    refetchInterval: (query) => {
      const data = query.state.data;
      return data?.status === 'Completed' || data?.status === 'Failed' ? false : 2000;
    },
  });
}
```

- [ ] **Step 5: ConfidenceBadge component**

```tsx
// apps/web/src/app/(authenticated)/gamebook/upload/_components/ConfidenceBadge.tsx
type Level = 'High' | 'Medium' | 'Low';

const styles: Record<Level, string> = {
  High: 'bg-green-100 text-green-800',
  Medium: 'bg-yellow-100 text-yellow-800',
  Low: 'bg-red-100 text-red-800',
};

const labels: Record<Level, string> = {
  High: 'Alta',
  Medium: 'Media',
  Low: 'Bassa',
};

export function ConfidenceBadge({ level }: { level: Level }) {
  return (
    <span
      className={`inline-flex items-center px-2 py-0.5 text-xs font-medium rounded ${styles[level]}`}
      data-slot="confidence-badge"
      data-level={level}
      aria-label={`Confidence ${labels[level]}`}
    >
      {labels[level]}
    </span>
  );
}
```

- [ ] **Step 6: PhotoUploader component (with TDD test first)**

```typescript
// apps/web/__tests__/gamebook/upload/PhotoUploader.test.tsx
import { describe, expect, test, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PhotoUploader } from '@/app/(authenticated)/gamebook/upload/_components/PhotoUploader';
import * as api from '@/lib/gamebook/api';

vi.mock('@/lib/gamebook/api');

function renderWithQueryClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe('PhotoUploader', () => {
  test('uploads files and displays progress', async () => {
    vi.mocked(api.uploadPhotoBatch).mockResolvedValue({ batchId: 'b1', acceptedCount: 1 });
    vi.mocked(api.getPhotoBatchStatus).mockResolvedValue({
      batchId: 'b1',
      status: 'Processing',
      totalPages: 1,
      indexedPages: 0,
      lowConfidencePages: 0,
      createdAt: '2026-05-04T00:00:00Z',
      completedAt: null,
      pages: [],
    });

    renderWithQueryClient(<PhotoUploader gameId="g1" />);

    const file = new File(['fake'], 'page.jpg', { type: 'image/jpeg' });
    const input = screen.getByLabelText(/foto del manuale/i) as HTMLInputElement;
    fireEvent.change(input, { target: { files: [file] } });

    fireEvent.click(screen.getByRole('button', { name: /inizia indicizzazione/i }));

    await waitFor(() =>
      expect(screen.getByText(/0 \/ 1 pagine indicizzate/)).toBeInTheDocument()
    );
  });

  test('shows error when upload fails', async () => {
    vi.mocked(api.uploadPhotoBatch).mockRejectedValue(new Error('Network error'));

    renderWithQueryClient(<PhotoUploader gameId="g1" />);

    const file = new File(['fake'], 'page.jpg', { type: 'image/jpeg' });
    fireEvent.change(screen.getByLabelText(/foto del manuale/i), { target: { files: [file] } });
    fireEvent.click(screen.getByRole('button', { name: /inizia indicizzazione/i }));

    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());
  });
});
```

```tsx
// apps/web/src/app/(authenticated)/gamebook/upload/_components/PhotoUploader.tsx
'use client';
import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { useUploadPhotoBatch } from '@/lib/gamebook/hooks/usePhotoBatchUpload';
import { usePhotoBatchStatus } from '@/lib/gamebook/hooks/usePhotoBatchStatus';
import { ConfidenceBadge } from './ConfidenceBadge';

export function PhotoUploader({ gameId }: { gameId: string }) {
  const t = useTranslations('gamebook.upload');
  const [files, setFiles] = useState<File[]>([]);
  const [language, setLanguage] = useState('en');
  const [batchId, setBatchId] = useState<string | undefined>();

  const upload = useUploadPhotoBatch();
  const status = usePhotoBatchStatus(batchId);

  const handleSubmit = async () => {
    try {
      const result = await upload.mutateAsync({ gameId, sourceLanguage: language, photos: files });
      setBatchId(result.batchId);
    } catch {
      // error rendered via upload.isError
    }
  };

  return (
    <div className="space-y-4" data-slot="photo-uploader">
      <label className="block">
        <span className="block mb-1 font-medium">{t('selectFiles')}</span>
        <input
          type="file"
          accept="image/*"
          multiple
          aria-label={t('selectFiles')}
          onChange={(e) => setFiles(Array.from(e.target.files ?? []))}
          className="block w-full"
        />
      </label>

      <label className="block">
        <span className="block mb-1 font-medium">{t('sourceLanguage')}</span>
        <select
          value={language}
          onChange={(e) => setLanguage(e.target.value)}
          className="border px-3 py-2 rounded"
        >
          <option value="en">English</option>
          <option value="de">Deutsch</option>
          <option value="fr">Français</option>
          <option value="es">Español</option>
          <option value="pt">Português</option>
          <option value="nl">Nederlands</option>
        </select>
      </label>

      <button
        type="button"
        onClick={handleSubmit}
        disabled={files.length === 0 || upload.isPending}
        className="bg-blue-600 text-white px-4 py-2 rounded disabled:opacity-50"
      >
        {upload.isPending ? t('uploading') : t('startIndexing', { count: files.length })}
      </button>

      {upload.isError && (
        <div role="alert" className="text-red-600">
          {t('uploadError')}: {upload.error?.message ?? 'Unknown error'}
        </div>
      )}

      {status.data && (
        <section data-slot="batch-progress" className="space-y-3">
          <p>
            {t('progress', { indexed: status.data.indexedPages, total: status.data.totalPages })}
          </p>
          <progress
            value={status.data.indexedPages}
            max={status.data.totalPages}
            className="w-full"
          />
          <ul className="grid grid-cols-2 md:grid-cols-4 gap-3">
            {status.data.pages.map((p) => (
              <li key={p.pageNumber} className="border rounded p-2 space-y-1">
                <img
                  src={p.thumbnailUrl}
                  alt={t('pageAlt', { num: p.pageNumber })}
                  className="w-full h-32 object-cover rounded"
                  loading="lazy"
                />
                <div className="flex items-center justify-between">
                  <span className="text-sm font-medium">
                    {t('pageNumber', { num: p.pageNumber })}
                  </span>
                  <ConfidenceBadge level={p.confidenceLevel} />
                </div>
                {p.confidenceLevel === 'Low' && (
                  <button type="button" className="text-sm text-blue-600">
                    {t('rephoto')}
                  </button>
                )}
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
```

- [ ] **Step 7: i18n strings**

```json
// apps/web/src/i18n/locales/it/gamebook.json
{
  "upload": {
    "selectFiles": "Foto del manuale",
    "sourceLanguage": "Lingua del manuale",
    "startIndexing": "Inizia indicizzazione ({count} foto)",
    "uploading": "Caricamento...",
    "uploadError": "Errore di caricamento",
    "progress": "{indexed} / {total} pagine indicizzate",
    "pageNumber": "Pagina {num}",
    "pageAlt": "Anteprima pagina {num}",
    "rephoto": "📸 Rifotografa"
  }
}
```

- [ ] **Step 8: Run tests**

```bash
cd apps/web
pnpm test PhotoUploader
```

Expected: 2 PASS.

- [ ] **Step 9: Commit**

```bash
git add apps/web/src/lib/gamebook/
git add apps/web/src/app/(authenticated)/gamebook/upload/
git add apps/web/src/i18n/locales/it/gamebook.json
git add apps/web/__tests__/gamebook/upload/
git commit -m "feat(gamebook): photo upload UI with TanStack Query + Zod schemas + i18n"
```

### Task 1.9 — Phase 1 acceptance gate

- [ ] **Step 1: E2E Playwright test su 1 manuale Tainted Grail**

```typescript
// apps/web/e2e/gamebook/upload-flow.spec.ts
import { test, expect } from '@playwright/test';
import path from 'node:path';

test.describe('Gamebook upload flow', () => {
  test('upload 50 Tainted Grail pages indexes within 5 min', async ({ page }) => {
    await page.goto('/login');
    // login flow with test user...

    await page.goto('/gamebook/upload?gameId=tainted-grail-test');

    const files = Array.from({ length: 50 }, (_, i) =>
      path.resolve(__dirname, `fixtures/tainted-grail-pages/page_${String(i).padStart(3, '0')}.jpg`)
    );
    await page.locator('input[type=file]').setInputFiles(files);
    await page.locator('button:has-text("Inizia indicizzazione")').click();

    await expect(page.locator('[data-slot="batch-progress"]')).toBeVisible();

    // Wait up to 5 min for completion
    await expect(page.locator('text=50 / 50 pagine indicizzate')).toBeVisible({ timeout: 5 * 60 * 1000 });

    // Check ≥ 95% high or medium confidence
    const lowBadges = await page.locator('[data-slot="confidence-badge"][data-level="Low"]').count();
    expect(lowBadges).toBeLessThan(3); // ≤ 5% low
  });
});
```

- [ ] **Step 2: Tag**

```bash
git tag mvp-phase-1-complete
git push origin mvp-phase-1-complete
```

---

## Phases 2, 3, 4 — Reference (compressed for plan v2 length)

> ## ⚠️ Plan v2 length pragmatic split
>
> Phase 1 dettagliato sopra (7 tasks corrette + accettazione gate). Per Phases 2-4 riferimento ai task IDs originali del plan v1 (commit `0890cd91e`) MA con queste correzioni applicate per ognuna:
>
> 1. **MediatR pattern**: usare `ICommandHandler<TCmd,TRes>` / `IQueryHandler<TQ,TRes>` (NOT `IRequestHandler`). Naming: `{Feature}Command.cs` + `{Feature}CommandHandler.cs` separated.
> 2. **NO IUserContext**: pass `UserId` as parameter in command/query.
> 3. **`IBlobStorageService`** (not `IBlobStorage`) in `Api.Services.Pdf` namespace.
> 4. **Background**: `IBackgroundTaskService.ExecuteWithCancellation()` (not Hangfire).
> 5. **Frontend**: use `httpClient` from `lib/api/core/httpClient.ts` with Zod schemas (not invented `apiClient`).
> 6. **Testing**: NSubstitute (not Moq), InMemory ConfigurationBuilder (not Testcontainers).
> 7. **RAG**: extend existing `AskQuestionQueryHandler` composing `SearchQueryHandler` + `IEmbeddingService` + `ILlmService` (no `IRagPipeline`).
> 8. **Migrations**: timestamp `yyyyMMddHHmmss` format, FK + indices esplicite, schema organization.

### Phase 2 task list (corrected references)

- **Task 2.1**: TranslationService skeleton + OpenRouter integration (split: `INarrativeTranslationService`, `IGenericTranslationService`)
- **Task 2.2**: TranslationCache Redis (use `IDistributedCache`, not Testcontainers)
- **Task 2.3**: KB Q&A extension — modify existing `AskQuestionQueryHandler` to:
  - Add `ResponseLanguage` parameter
  - Translate output via `IGenericTranslationService`
  - Check house rules first via new `IHouseRuleMatcher`
  - Consume pricing quota via `IPricingEngine.ConsumeQuotaAsync`
- **Task 2.4**: `IQAComplexityClassifier` interface + `HeuristicQAComplexityClassifier` implementation (improve over keyword matching)
- **Task 2.5**: HouseRule entity + repository + matcher + endpoints
- **Task 2.6**: GameGlossaryEntry entity + repository + endpoints + auto-bootstrap NER service
- **Task 2.7**: Hallucination CI gate via golden eval — Python runner + LLM-as-judge using DIFFERENT model (GPT-4 judges Claude)
- **Task 2.8**: `infra/docker-compose.test.yml` for E2E test environment
- **Task 2.9**: Phase 2 acceptance gate

### Phase 3 task list (corrected references)

- **Task 3.1**: `GetParagraphQuery` + handler (numbered + semantic search fallback)
- **Task 3.2**: `IPricingEngine` + `CreditBasedPricingEngine` + `UserQuota` entity + factory
- **Task 3.3a**: Stripe.NET package install + `StripeCheckoutService`
- **Task 3.3b**: Stripe webhook handler with signature verification + idempotency
- **Task 3.3c**: Pricing endpoints (`/quota`, `/credits/checkout`, `/webhooks/stripe`)
- **Task 3.4**: Quartz job `MonthlyQuotaResetJob` for free tier reset
- **Task 3.5**: Frontend gameplay screen — split per component:
  - 3.5a: `useTranslateParagraph`, `useAskQuestion`, `useStartCheckout`, `useQuota` hooks
  - 3.5b: `TranslationViewer` component + tests
  - 3.5c: `QAPanel` component + tests
  - 3.5d: `QuotaExceededModal` + accessibility (focus trap, ARIA, ESC)
  - 3.5e: `HouseRuleModal` + flow integration
  - 3.5f: Compose `play/page.tsx`
- **Task 3.6**: `IUserRateLimiter` middleware (per-user, per-endpoint)
- **Task 3.7**: Privacy policy page UI + footer link
- **Task 3.8**: GDPR `DeleteUserDataCommand` + `ExportUserDataQuery` + endpoints
- **Task 3.9**: Phase 3 acceptance E2E + payment flow test

### Phase 4 task list (corrected references)

- **Task 4.1**: Chaos engineering tests:
  - 4.1a: Production-like staging environment
  - 4.1b: WireMock for OpenRouter rate limit simulation
  - 4.1c: Network namespace WiFi loss simulation
  - 4.1d: PostgreSQL failover (replica → primary) drill
- **Task 4.2**: Usability testing 5 sessions (UX researcher, $250 budget incentives)
- **Task 4.3**: DR drill — restore from Storage Box backup, validate consistency, RTO < 2h
- **Task 4.4**: Cost telemetry dashboard final review + alerting verification
- **Task 4.5**: Final launch checklist (8 items in vision §6.5)
- **Task 4.6**: Production deploy + monitoring sustained 1 week post-launch

---

## Risk Mitigation Matrix (corrected)

| Risk | Mitigation in plan v2 |
|------|------------------------|
| **R-2 OCR quality bassa** | Task 0.1 PR-2 validation upfront, gate decision matrix per game/condition |
| **R-13 OCR layout artistici** | Task 0.1 includes Tainted Grail + ISS Vanguard + manual override UI in Task 1.7 |
| **R-5 LLM hallucination** | Task 2.7 CI gate + LLM-as-judge with DIFFERENT model + threshold ≤ 3% golden, 0% > 0.85 confidence |
| **R-9 WiFi instabile** | Task 4.1c chaos + G4.7 graceful UI scenario in Task 3.5b |
| **R-1 LLM pricing spike** | OpenRouter abstraction (Task 2.1), multi-model routing (Task 2.4 router), Task 0.5 cost alerting |
| **R-15 Test set bottleneck** | Task 0.2 with 4-week lead time + contractor budget + 100% review process |
| **R-3 Copyright takedown** | Task 0.3 PR-1 legal review pre-launch + TOS robust + GDPR data residency disclosure |
| **CAX31 capacity** | Task 0.4 verified 6.4-9 GB on 16 GB capacity, monitoring dashboard alerts |
| **Stripe webhook security** | Task 3.3b dedicated to signature verification + idempotency keys |
| **GDPR right-to-deletion** | Task 3.8 explicit |
| **Rate limiting** | Task 3.6 explicit middleware |
| **Observability gaps** | Task 0.4 step 7-8 stack deploy, Task 4.4 dashboard review |

---

## Self-Review Notes (Plan author v2)

**Spec coverage check** against vision §6:
- ✅ G1 Acquire manuale (Tasks 1.1-1.9)
- ✅ G3 Q&A regole + house rule + pricing integration (Tasks 2.3-2.5)
- ✅ G4 Translation on-demand + chapter-based fallback (Task 3.1)
- ✅ Default Auto LLM mode invisible (Task 2.4 router internal)
- ✅ Pricing 2-tier Free + Credits (Tasks 3.2-3.4)
- ✅ Single-device only (no multi-device QR in plan)
- ✅ CAX31 baseline + observability (Task 0.4)
- ✅ Test plan: Unit + Integration + E2E + LLM eval (Tasks 1.9, 2.7, 2.9, 3.9, 4.1)
- ✅ Prerequisites PR-1/PR-2/PR-3 covered (Tasks 0.1-0.3)
- ✅ NEW: Observability stack (Task 0.4 step 7-8)
- ✅ NEW: Backup automation (Task 0.4 step 5-6)
- ✅ NEW: SharedGameCatalog integration (Task 0.7)
- ✅ NEW: Stripe webhook security (Task 3.3b)
- ✅ NEW: Rate limiting (Task 3.6)
- ✅ NEW: Privacy/GDPR (Tasks 3.7, 3.8)
- ✅ NEW: Cost telemetry dashboard (Task 0.5 step 5-6, Task 4.4)
- ✅ NEW: Glossary CRUD (Task 2.6)
- ✅ NEW: i18n CI gate (Task 4.6 sub-step)

**Type consistency** (verified):
- `PhotoPreprocessResult.ExtractedText` defined in Task 1.4a, used in Task 1.6
- `IPhotoBatchUploadRepository` defined in Task 1.2, used in Tasks 1.5, 1.6, 1.7
- `IBlobStorageService` from existing `Api.Services.Pdf` namespace verified
- `ICommandHandler<TCmd,TRes>` consistent across all backend tasks
- Frontend `httpClient` consistent with existing `lib/api/core/httpClient.ts`

**Pattern compliance**:
- All MediatR handlers use `ICommandHandler` / `IQueryHandler` (NOT `IRequestHandler`)
- UserId passed as parameter (NO `IUserContext`)
- Background tasks via `IBackgroundTaskService` (NOT Hangfire)
- Tests use NSubstitute + FluentAssertions (NOT Moq)
- Frontend uses Zod validation (existing pattern)

**Effort estimate honest**:
- Phase 0: 5-6 weeks (extended by observability + backup automation tasks)
- Phase 1: 6 weeks (long pole ML)
- Phase 2: 6 weeks
- Phase 3: 6 weeks
- Phase 4: 4 weeks
- **Total: 27-28 weeks calendar (6-7 mesi)** with 3 fullstack + 1 ML + 1 designer + legal advisor part-time + UX researcher part-time

> ⚠️ **Honest re-estimate**: 6-7 mesi, NOT 4-5 mesi. Vision doc §6.4 needs update.

---

**Plan v2 end. 6-7 mesi calendar, 3 fullstack + 1 ML + 1 designer + part-time legal + part-time UX researcher.**

**Successor**: implementation execution via `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` (if same session).
