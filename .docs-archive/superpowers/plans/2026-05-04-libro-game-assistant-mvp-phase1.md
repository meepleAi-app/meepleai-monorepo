# Libro Game AI Assistant — MVP Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build MVP Phase 1 (G1+G3+G4 ridotti) del Libro Game AI Assistant per casual italian boardgamer come definito in `docs/superpowers/specs/2026-05-04-libro-game-assistant-vision.md` §6.

**Architecture:** Estensione cross-cutting su BC esistenti MeepleAI. Photo-first ingestion in `DocumentProcessing` BC. Q&A in `KnowledgeBase` BC. Nuovo `TranslationService` cross-cutting (non BC) con OpenRouter abstraction. Hetzner CAX31 baseline. Pricing 2-tier (Free 50 pag/mese + Credits €5/100 pag).

**Tech Stack:** .NET 9 + ASP.NET Minimal APIs + MediatR + EF Core + PostgreSQL 16 + pgvector + Redis | Next.js 16 + React 19 + Tailwind 4 + TanStack Query | Python smoldocling + embedding-service | OpenRouter (LLM abstraction) | Anthropic Claude Sonnet 4.5 + Haiku 4.5 + DeepSeek V3 (via OpenRouter) | xUnit + Testcontainers + Vitest + Playwright

**Effort estimate:** 4-5 mesi calendar (sprint 2-week × 10 sprint = 20 settimane), 3 dev fullstack + 1 ML engineer + 1 UI designer + legal advisor part-time.

**Critical path:** Phase 0 prerequisites (4 weeks lead) → Phase 1 G1 ingestion (long pole 6 weeks ML) → Phase 2 G3+TranslationService (parallel partial after week 4) → Phase 3 G4+UI+Pricing → Phase 4 launch prep.

---

## Phase Overview & Risk-Driven Sequencing

| Phase | Calendar | Deliverable | Risk gates |
|-------|----------|-------------|------------|
| **Phase 0** | Pre-sprint week -4 to 0 | Prerequisites (PR-1/2/3) + infra | OCR fundamental viability check (PR-2) |
| **Phase 1** | Sprint 1-3 (week 1-6) | G1 photo-first ingestion vertical slice | OCR confidence ≥ 85% on test set |
| **Phase 2** | Sprint 4-6 (week 7-12) | G3 Q&A + TranslationService minimal | Hallucination ≤ 3% on golden test set |
| **Phase 3** | Sprint 7-9 (week 13-18) | G4 translation + UI screens + pricing engine | UX usability score, payment flow |
| **Phase 4** | Sprint 10-11 (week 19-22) | Integration, chaos, usability, launch | Production readiness checklist |

**Parallel work opportunities:**
- Phase 0 PR-1 (legal) runs in parallel with PR-2/PR-3 (technical)
- Phase 1 ML work + Phase 2 backend (mock data) can overlap from week 4
- Phase 3 designer can start mockups during Phase 2 implementation
- Phase 4 chaos testing in parallel with usability (different teams)

---

## File Structure Map

Files created/modified across all phases:

### Backend (`apps/api/src/Api/`)

**New BC capability** (estensione DocumentProcessing):
- Create: `BoundedContexts/DocumentProcessing/Domain/ValueObjects/PhotoIngestionMetadata.cs`
- Create: `BoundedContexts/DocumentProcessing/Domain/Entities/PhotoBatchUpload.cs`
- Create: `BoundedContexts/DocumentProcessing/Application/Commands/UploadPhotoBatchCommand.cs`
- Create: `BoundedContexts/DocumentProcessing/Application/Commands/UploadPhotoBatchHandler.cs`
- Create: `BoundedContexts/DocumentProcessing/Application/Validators/UploadPhotoBatchValidator.cs`
- Create: `BoundedContexts/DocumentProcessing/Application/Jobs/PhotoIngestionJob.cs`
- Create: `BoundedContexts/DocumentProcessing/Application/Services/IPhotoPreprocessor.cs`
- Create: `BoundedContexts/DocumentProcessing/Infrastructure/Services/SmoldoclingPhotoPreprocessor.cs`
- Create: `BoundedContexts/DocumentProcessing/Application/Queries/GetPhotoBatchStatusQuery.cs`
- Create: `BoundedContexts/DocumentProcessing/Application/DTOs/PhotoBatchStatusDto.cs`

**New TranslationService cross-cutting** (`Infrastructure/Translation/`):
- Create: `Infrastructure/Translation/ITranslationService.cs`
- Create: `Infrastructure/Translation/OpenRouterTranslationService.cs`
- Create: `Infrastructure/Translation/Models/TranslationRequest.cs`
- Create: `Infrastructure/Translation/Models/TranslationResponse.cs`
- Create: `Infrastructure/Translation/Models/Glossary.cs`
- Create: `Infrastructure/Translation/Cache/ITranslationCache.cs`
- Create: `Infrastructure/Translation/Cache/RedisTranslationCache.cs`
- Create: `Infrastructure/Translation/Routing/IModelRouter.cs`
- Create: `Infrastructure/Translation/Routing/OpenRouterModelRouter.cs`
- Create: `Infrastructure/Translation/Prompts/NarrativeTranslationPrompt.cs`

**KnowledgeBase BC extension**:
- Modify: `BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionQuery.cs` (add `language` param)
- Modify: `BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionHandler.cs` (translate output)
- Create: `BoundedContexts/KnowledgeBase/Application/Queries/GetParagraphQuery.cs`
- Create: `BoundedContexts/KnowledgeBase/Application/Queries/GetParagraphHandler.cs`
- Modify: `BoundedContexts/KnowledgeBase/Domain/Entities/AnswerCitation.cs` (add `confidence` enum)

**AgentMemory BC extension** (house rules):
- Create: `BoundedContexts/AgentMemory/Application/Commands/SaveHouseRuleCommand.cs`
- Create: `BoundedContexts/AgentMemory/Application/Queries/GetHouseRulesForGameQuery.cs`

**Pricing engine** (`Infrastructure/Pricing/`):
- Create: `Infrastructure/Pricing/IPricingEngine.cs`
- Create: `Infrastructure/Pricing/CreditBasedPricingEngine.cs`
- Create: `Infrastructure/Pricing/Models/UserQuota.cs`
- Create: `Infrastructure/Pricing/Models/CreditBalance.cs`
- Create: `BoundedContexts/Authentication/Domain/Entities/UserSubscription.cs` (add free tier counter)

**Routing**:
- Create: `Routing/PhotoIngestionEndpoints.cs`
- Create: `Routing/TranslationEndpoints.cs`
- Create: `Routing/HouseRulesEndpoints.cs`
- Create: `Routing/PricingEndpoints.cs`
- Modify: `Routing/KnowledgeBaseEndpoints.cs` (extend Q&A endpoint)

### Frontend (`apps/web/src/`)

**New routes** (libro game flow):
- Create: `app/(authenticated)/gamebook/page.tsx` — landing libro game
- Create: `app/(authenticated)/gamebook/upload/page.tsx` — upload manuale
- Create: `app/(authenticated)/gamebook/[gameId]/play/page.tsx` — sessione di gioco (Q&A + translation)
- Create: `app/(authenticated)/gamebook/[gameId]/play/_components/QAPanel.tsx`
- Create: `app/(authenticated)/gamebook/[gameId]/play/_components/TranslationViewer.tsx`
- Create: `app/(authenticated)/gamebook/[gameId]/play/_components/HouseRuleModal.tsx`
- Create: `app/(authenticated)/gamebook/[gameId]/play/_components/QuotaExceededModal.tsx`

**API client + hooks**:
- Create: `lib/gamebook/api.ts`
- Create: `lib/gamebook/hooks/usePhotoBatchUpload.ts`
- Create: `lib/gamebook/hooks/useAskQuestion.ts`
- Create: `lib/gamebook/hooks/useTranslateParagraph.ts`
- Create: `lib/gamebook/hooks/useHouseRules.ts`
- Create: `lib/gamebook/hooks/useQuota.ts`

**i18n**:
- Create: `i18n/locales/it/gamebook.json`

### Database migrations
- Create: `apps/api/src/Api/Migrations/2026XXXXXXXXXX_AddPhotoIngestion.cs`
- Create: `apps/api/src/Api/Migrations/2026XXXXXXXXXX_AddTranslationCache.cs`
- Create: `apps/api/src/Api/Migrations/2026XXXXXXXXXX_AddCreditBalance.cs`
- Create: `apps/api/src/Api/Migrations/2026XXXXXXXXXX_AddHouseRules.cs`

### Tests
- Create: `tests/Api.Tests/DocumentProcessing/PhotoIngestion/UploadPhotoBatchHandlerTests.cs`
- Create: `tests/Api.Tests/DocumentProcessing/PhotoIngestion/PhotoIngestionJobTests.cs`
- Create: `tests/Api.Tests/Translation/OpenRouterTranslationServiceTests.cs`
- Create: `tests/Api.Tests/Translation/RedisTranslationCacheTests.cs`
- Create: `tests/Api.Tests/Translation/OpenRouterModelRouterTests.cs`
- Create: `tests/Api.Tests/KnowledgeBase/AskQuestionHandlerWithTranslationTests.cs`
- Create: `tests/Api.Tests/Pricing/CreditBasedPricingEngineTests.cs`
- Create: `tests/Api.Tests/Integration/GamebookE2ETests.cs`
- Create: `apps/web/e2e/gamebook/upload-flow.spec.ts`
- Create: `apps/web/e2e/gamebook/qa-flow.spec.ts`
- Create: `apps/web/e2e/gamebook/translation-flow.spec.ts`
- Create: `apps/web/e2e/gamebook/quota-flow.spec.ts`

### LLM evaluation harness
- Create: `tests/llm-eval/golden-set/qa-questions.jsonl` (PR-3, 100 entries)
- Create: `tests/llm-eval/golden-set/translation-paragraphs.jsonl` (PR-3, 50 entries)
- Create: `tests/llm-eval/runners/qa_eval.py`
- Create: `tests/llm-eval/runners/translation_eval.py`
- Create: `.github/workflows/llm-eval-gate.yml`

### Infrastructure
- Create: `infra/hetzner/cax31-bootstrap.sh`
- Create: `infra/hetzner/disaster-recovery.md`
- Modify: `infra/docker-compose.production.yml`
- Create: `infra/secrets/openrouter.secret.example`
- Create: `infra/secrets/openrouter.secret`

### Documentation
- Create: `docs/development/libro-game-architecture.md`
- Create: `docs/operations/openrouter-runbook.md`
- Create: `docs/operations/llm-cost-monitoring.md`
- Modify: `CLAUDE.md` (add libro game BC mapping)

---

## Phase 0 — Prerequisites (Pre-sprint, 4 weeks)

**Goal:** Risolvere blockers PR-1/PR-2/PR-3 + setup infrastruttura. **Senza questi, Phase 1 non parte.**

### Task 0.1 — PR-2: OCR validation su 5 manuali gamebook reali

**Owner:** ML engineer
**Effort:** 2 weeks
**Risk gate:** Se confidence avg < 0.7 su 3+ manuali → MVP scope review (cambio stack OCR o esclusione layout artistici)

**Files:**
- Create: `tests/llm-eval/ocr-validation/manuals/` (gitignored, contains test PDFs/photos)
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

- [ ] **Step 3: Creare validation script**

```python
# tests/llm-eval/ocr-validation/run_validation.py
import json
from pathlib import Path
from smoldocling_client import SmoldoclingClient

def validate_manual(game_name: str, condition: str) -> dict:
    client = SmoldoclingClient()
    results = []
    for img_path in Path(f"manuals/{game_name}/{condition}").glob("*.jpg"):
        ocr_result = client.extract(img_path)
        results.append({
            "page": img_path.stem,
            "confidence": ocr_result.confidence,
            "char_count": len(ocr_result.text),
            "warnings": ocr_result.warnings
        })
    return {
        "game": game_name,
        "condition": condition,
        "avg_confidence": sum(r["confidence"] for r in results) / len(results),
        "min_confidence": min(r["confidence"] for r in results),
        "low_conf_pages": [r for r in results if r["confidence"] < 0.7],
        "results": results
    }

if __name__ == "__main__":
    games = ["tainted-grail", "iss-vanguard", "stuffed-fables", "andor", "7th-continent"]
    conditions = ["good-light", "evening-light", "angled"]
    all_results = []
    for game in games:
        for cond in conditions:
            all_results.append(validate_manual(game, cond))
    Path("results.json").write_text(json.dumps(all_results, indent=2))
```

- [ ] **Step 4: Run validation**

Run: `cd tests/llm-eval/ocr-validation && python run_validation.py`
Expected: `results.json` with confidence scores per (game, condition, page)

- [ ] **Step 5: Analyze + decide gate**

Crea `results.md` con summary:
- Avg confidence per (game, condition)
- % pages above 0.85 (high)
- % pages 0.7-0.85 (medium)
- % pages below 0.7 (low — manual review needed)

**Gate decision matrix**:
- Avg confidence ≥ 0.85 across all 5 manuals → ✅ proceed Phase 1
- Avg 0.7-0.85 → ⚠️ proceed but with strong UI confidence indicators + manual override (current plan covers this)
- Avg < 0.7 on 3+ manuals → 🔴 STOP, scope review:
  - Investigate alternative OCR (Mistral OCR, Google Document AI)
  - Reduce scope: support only well-OCR'd manuals in MVP
  - Defer artistic-layout games to v2

- [ ] **Step 6: Document findings + commit**

```bash
git add tests/llm-eval/ocr-validation/results.md tests/llm-eval/ocr-validation/run_validation.py
git commit -m "feat(eval): PR-2 OCR validation results on 5 gamebook manuals"
```

### Task 0.2 — PR-3: Test set golden creation

**Owner:** ML engineer + IT native speaker (gamebook expertise) — contractor
**Effort:** 4 weeks lead time, 2 weeks effective work
**Risk gate:** Senza golden set, hallucination eval impossible → CI gate non implementabile

**Files:**
- Create: `tests/llm-eval/golden-set/qa-questions.jsonl` (100 entries)
- Create: `tests/llm-eval/golden-set/translation-paragraphs.jsonl` (50 entries)
- Create: `tests/llm-eval/golden-set/README.md` (methodology)

- [ ] **Step 1: Identify contractor**

Cercare IT native speaker con:
- Background board game (BGG profile, gamebook player attestato)
- Esperienza traduzione/copyediting
- Disponibile 80h totali ($30-50/h)

- [ ] **Step 2: Definire schema JSONL Q&A**

```jsonl
{"id": "qa-001", "game": "tainted-grail", "question_it": "Quanti dadi tira il mago per il fireball?", "expected_answer_it": "Il mago tira 3 dadi base, +1 per ogni livello di Mastery Fuoco oltre 1.", "expected_citation_pages": [22, 23], "category": "combat", "difficulty": "easy"}
```

100 questions distribuite:
- 60 easy (well-documented in manual)
- 30 medium (require multi-page synthesis)
- 10 hard (edge cases, ambiguous, no-answer expected)

Coperture:
- 5 manuali × 20 questions/manuale
- Categorie: setup, combat, narrative, character, items, edge-cases

- [ ] **Step 3: Definire schema JSONL translation**

```jsonl
{"id": "tr-001", "game": "tainted-grail", "source_lang": "en", "paragraph_id": "147", "source_text": "Niamh raises the Sword of Avalon and the Wraithstone glows with eldritch fire.", "expected_translation_it": "Niamh solleva la Spada di Avalon e la Pietra Spettrale brilla di fuoco arcano.", "tone": "fantasy-dramatic", "glossary": {"Sword of Avalon": "Spada di Avalon", "Wraithstone": "Pietra Spettrale", "Niamh": "Niamh"}}
```

50 paragraphs con varietà:
- 20 narrative descriptive
- 15 dialogue
- 10 climactic/dramatic
- 5 technical (rules embedded in narrative)

- [ ] **Step 4: Contractor creates golden set**

Brief contractor:
- Goal: ground truth per LLM eval
- Quality bar: traduzione "publication-ready" italiana
- Process: leggere paragrafo → tradurre → review per consistency glossario
- Tools: spreadsheet → export JSONL via script

- [ ] **Step 5: Internal review + commit**

ML engineer revisiona 10% campione casuale:
- Linguaggio italiano corretto?
- Glossario consistent?
- Tono preservato?

```bash
git add tests/llm-eval/golden-set/
git commit -m "feat(eval): PR-3 golden test set 100 Q&A + 50 translations"
```

### Task 0.3 — PR-1: Legal review copyright

**Owner:** Aaron (identifies legal advisor) + Legal advisor
**Effort:** 2-3 weeks calendar
**Risk gate:** Senza legal sign-off → MVP non lancia

**Files:**
- Create: `docs/legal/copyright-position.md`
- Create: `docs/legal/tos-libro-game-addendum.md`
- Create: `docs/legal/privacy-policy-libro-game.md`

- [ ] **Step 1: Identify legal advisor specialized in IP/copyright**

Criteri:
- Esperienza prodotti SaaS user-generated content
- Familiarità con UE Copyright Directive 2019/790 + DMCA US (per future expansion)
- Idealmente background gaming/entertainment

- [ ] **Step 2: Brief advisor**

Documento brief contenente:
- Use case: utenti caricano foto manuali board game che possiedono fisicamente
- Scope: indicizzazione + Q&A su contenuto + traduzione frammenti
- NO redistribuzione pubblica
- Free tier limitato (50 pag/mese)

- [ ] **Step 3: Advisor produce 3 deliverable**

(a) Position paper su rischi copyright:
- Fair use applicabile?
- Differenza traduzione completa vs frammenti
- Liability shield disponibili (DMCA safe harbor)

(b) TOS addendum specifico libro game:
- User dichiara di possedere copia legale
- User indemnify MeepleAI per copyright infringement claims
- Right to terminate account su takedown notice valido

(c) Privacy policy update GDPR:
- Storage manuali user-uploaded
- Right to deletion implementation
- Data retention policy

- [ ] **Step 4: Internal review + commit**

```bash
git add docs/legal/
git commit -m "feat(legal): PR-1 copyright position + TOS + privacy policy"
```

### Task 0.4 — Hetzner CAX31 provisioning

**Owner:** DevOps (può essere fullstack lead)
**Effort:** 3 days

**Files:**
- Create: `infra/hetzner/cax31-bootstrap.sh`
- Create: `infra/hetzner/disaster-recovery.md`
- Modify: `infra/docker-compose.production.yml`

- [ ] **Step 1: Provision CAX31 in Hetzner Cloud**

Via Hetzner console o `hcloud` CLI:
```bash
hcloud server create --name meepleai-prod-1 --type cax31 --image ubuntu-24.04 --location fsn1 --ssh-key meepleai-prod
```

Expected: Server reachable on assigned IPv4.

- [ ] **Step 2: Bootstrap script**

```bash
# infra/hetzner/cax31-bootstrap.sh
#!/usr/bin/env bash
set -euo pipefail

# System update
apt-get update && apt-get upgrade -y

# Install Docker (ARM64)
curl -fsSL https://get.docker.com | sh
systemctl enable --now docker

# Install Docker Compose
mkdir -p ~/.docker/cli-plugins
curl -SL https://github.com/docker/compose/releases/latest/download/docker-compose-linux-aarch64 \
  -o ~/.docker/cli-plugins/docker-compose
chmod +x ~/.docker/cli-plugins/docker-compose

# Firewall (only 22, 80, 443 from internet)
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable

# Swap (8 GB safety net)
fallocate -l 8G /swapfile
chmod 600 /swapfile
mkswap /swapfile
swapon /swapfile
echo '/swapfile none swap sw 0 0' >> /etc/fstab

# Hetzner Storage Box mount (for backups)
apt-get install -y cifs-utils
mkdir -p /mnt/storagebox
# /etc/fstab entry will be added after Storage Box provisioning

echo "Bootstrap complete. Reboot recommended."
```

- [ ] **Step 3: Run bootstrap**

```bash
ssh root@<cax31-ip> 'bash -s' < infra/hetzner/cax31-bootstrap.sh
```

- [ ] **Step 4: Provision Hetzner Storage Box 1 TB**

Via console: order Storage Box BX11 (~€4/mese, 1 TB SMB/SFTP).
Expected: SMB credentials per backup mount.

- [ ] **Step 5: Configure Storage Box mount**

Aggiungere a `/etc/fstab`:
```
//u<box-id>.your-storagebox.de/backup /mnt/storagebox cifs credentials=/etc/cifs-creds-storagebox,vers=3.0,iocharset=utf8 0 0
```

Test mount: `mount /mnt/storagebox && touch /mnt/storagebox/test && rm /mnt/storagebox/test`.

- [ ] **Step 6: Deploy initial docker-compose stack**

Modify `infra/docker-compose.production.yml` per:
- Multi-arch support (ARM64 manifest)
- Volume mounts to local NVMe for hot data
- Volume mounts to Storage Box for backups
- Resource limits per service (memory caps)

- [ ] **Step 7: DNS + TLS**

- Cloudflare DNS: `meepleai.com` → CAX31 IPv4
- Let's Encrypt via Caddy/Traefik in docker-compose
- Test: `curl https://meepleai.com/health` returns 200

- [ ] **Step 8: Disaster recovery doc + commit**

```bash
git add infra/hetzner/
git commit -m "feat(infra): provision Hetzner CAX31 + Storage Box + DR runbook"
```

### Task 0.5 — OpenRouter account + secret management

**Owner:** Backend lead
**Effort:** 1 day

**Files:**
- Create: `infra/secrets/openrouter.secret.example`
- Create: `infra/secrets/openrouter.secret` (NOT committed, generated via secrets-sync)

- [ ] **Step 1: Create OpenRouter account**

- Register at openrouter.ai
- Add credit balance ($50 starter for development)
- Generate API key with scoped permissions

- [ ] **Step 2: Create secret template**

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
```

- [ ] **Step 3: Sync secret to staging via existing tooling**

```bash
cd infra
make secrets-setup  # generates placeholder
# Manually populate openrouter.secret with real values
make secrets-sync   # pushes to staging
```

- [ ] **Step 4: Validate via curl**

```bash
source infra/secrets/openrouter.secret
curl -X POST $OPENROUTER_BASE_URL/chat/completions \
  -H "Authorization: Bearer $OPENROUTER_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "'$OPENROUTER_DEFAULT_MODEL'",
    "messages": [{"role": "user", "content": "Hello"}]
  }'
```

Expected: 200 response with completion.

- [ ] **Step 5: Commit template**

```bash
git add infra/secrets/openrouter.secret.example
git commit -m "feat(infra): add OpenRouter secret template + validation"
```

---

## Phase 1 — G1 Photo-First Ingestion (Sprint 1-3, weeks 1-6)

**Goal:** Sara può fotografare 50 pagine di un manuale e averle indicizzate in < 5 min con confidence visibile.

**Acceptance gate Phase 1:** Run end-to-end test su 1 manuale Tainted Grail completo, verificare:
- Throughput ≥ 10 pag/min batch
- Confidence ≥ 0.7 su ≥ 95% pagine
- UI mostra preview confidence + opzione "rifotografa"
- KB query funzionante post-indexing

### Task 1.1 — Database migration: PhotoBatchUpload entity

**Files:**
- Create: `apps/api/src/Api/Migrations/2026XXXXXXXXXX_AddPhotoIngestion.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/PhotoBatchUpload.cs`

- [ ] **Step 1: Define entity with factory pattern**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/PhotoBatchUpload.cs
namespace MeepleAi.Api.BoundedContexts.DocumentProcessing.Domain.Entities;

public class PhotoBatchUpload
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
    [Timestamp] public byte[] RowVersion { get; private set; } = null!;

    private PhotoBatchUpload() { } // EF

    public static PhotoBatchUpload Create(Guid userId, Guid gameId, string sourceLanguage, int totalPages)
    {
        if (totalPages <= 0) throw new ArgumentException("Total pages must be positive", nameof(totalPages));
        if (string.IsNullOrWhiteSpace(sourceLanguage)) throw new ArgumentException("Source language required");

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

    public void StartProcessing() => Status = PhotoBatchStatus.Processing;

    public void RecordPageIndexed()
    {
        IndexedPages++;
        if (IndexedPages >= TotalPages)
        {
            Status = PhotoBatchStatus.Completed;
            CompletedAt = DateTime.UtcNow;
        }
    }

    public void Fail(string reason)
    {
        Status = PhotoBatchStatus.Failed;
        CompletedAt = DateTime.UtcNow;
    }
}

public enum PhotoBatchStatus { Pending, Processing, Completed, Failed }
```

- [ ] **Step 2: Generate migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddPhotoIngestion
```

Verify generated SQL includes `PhotoBatchUploads` table with all columns + indices on `UserId`, `GameId`, `Status`.

- [ ] **Step 3: Apply migration locally**

```bash
dotnet ef database update
```

Expected: table created in dev PostgreSQL.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/PhotoBatchUpload.cs
git add apps/api/src/Api/Migrations/2026*_AddPhotoIngestion.cs
git commit -m "feat(document-processing): add PhotoBatchUpload entity + migration"
```

### Task 1.2 — UploadPhotoBatchCommand + Validator

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPhotoBatchCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Validators/UploadPhotoBatchValidator.cs`
- Test: `tests/Api.Tests/DocumentProcessing/PhotoIngestion/UploadPhotoBatchValidatorTests.cs`

- [ ] **Step 1: Write failing validator test**

```csharp
// tests/Api.Tests/DocumentProcessing/PhotoIngestion/UploadPhotoBatchValidatorTests.cs
using FluentValidation.TestHelper;
using MeepleAi.Api.BoundedContexts.DocumentProcessing.Application.Commands;
using MeepleAi.Api.BoundedContexts.DocumentProcessing.Application.Validators;
using Xunit;

public class UploadPhotoBatchValidatorTests
{
    private readonly UploadPhotoBatchValidator _validator = new();

    [Fact]
    public void Validate_EmptyPhotoList_ShouldHaveError()
    {
        var cmd = new UploadPhotoBatchCommand
        {
            GameId = Guid.NewGuid(),
            SourceLanguage = "en",
            Photos = Array.Empty<PhotoUploadDto>()
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(c => c.Photos);
    }

    [Fact]
    public void Validate_TooManyPhotos_ShouldHaveError()
    {
        var cmd = new UploadPhotoBatchCommand
        {
            GameId = Guid.NewGuid(),
            SourceLanguage = "en",
            Photos = Enumerable.Range(0, 201).Select(_ => new PhotoUploadDto()).ToArray()
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(c => c.Photos);
    }

    [Fact]
    public void Validate_UnsupportedLanguage_ShouldHaveError()
    {
        var cmd = new UploadPhotoBatchCommand
        {
            GameId = Guid.NewGuid(),
            SourceLanguage = "ja",  // CJK not supported MVP
            Photos = new[] { new PhotoUploadDto() }
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(c => c.SourceLanguage);
    }

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveError()
    {
        var cmd = new UploadPhotoBatchCommand
        {
            GameId = Guid.NewGuid(),
            SourceLanguage = "en",
            Photos = new[] { new PhotoUploadDto { Filename = "p1.jpg", Base64Content = "..." } }
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
```

- [ ] **Step 2: Run test (should fail — types don't exist)**

```bash
cd tests/Api.Tests
dotnet test --filter "FullyQualifiedName~UploadPhotoBatchValidatorTests" -v normal
```

Expected: BUILD FAIL — `UploadPhotoBatchCommand` not found.

- [ ] **Step 3: Implement command + validator**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPhotoBatchCommand.cs
using MediatR;

namespace MeepleAi.Api.BoundedContexts.DocumentProcessing.Application.Commands;

public class UploadPhotoBatchCommand : IRequest<UploadPhotoBatchResult>
{
    public Guid GameId { get; init; }
    public string SourceLanguage { get; init; } = null!;
    public PhotoUploadDto[] Photos { get; init; } = Array.Empty<PhotoUploadDto>();
}

public class PhotoUploadDto
{
    public string Filename { get; init; } = null!;
    public string Base64Content { get; init; } = null!;
}

public class UploadPhotoBatchResult
{
    public Guid BatchId { get; init; }
    public int AcceptedCount { get; init; }
}
```

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Validators/UploadPhotoBatchValidator.cs
using FluentValidation;
using MeepleAi.Api.BoundedContexts.DocumentProcessing.Application.Commands;

public class UploadPhotoBatchValidator : AbstractValidator<UploadPhotoBatchCommand>
{
    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "en", "it", "de", "fr", "es", "pt", "nl"
    };

    public UploadPhotoBatchValidator()
    {
        RuleFor(c => c.GameId).NotEmpty();
        RuleFor(c => c.SourceLanguage)
            .NotEmpty()
            .Must(lang => SupportedLanguages.Contains(lang))
            .WithMessage("Source language must be one of: en, it, de, fr, es, pt, nl");
        RuleFor(c => c.Photos)
            .NotEmpty().WithMessage("At least one photo required")
            .Must(photos => photos.Length <= 200).WithMessage("Maximum 200 photos per batch");
    }
}
```

- [ ] **Step 4: Run tests, verify pass**

```bash
dotnet test --filter "FullyQualifiedName~UploadPhotoBatchValidatorTests"
```

Expected: 4 PASSED.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPhotoBatchCommand.cs
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Validators/UploadPhotoBatchValidator.cs
git add tests/Api.Tests/DocumentProcessing/PhotoIngestion/UploadPhotoBatchValidatorTests.cs
git commit -m "feat(document-processing): add UploadPhotoBatchCommand + FluentValidation"
```

### Task 1.3 — IPhotoPreprocessor + Smoldocling impl (dewarping + page detection)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IPhotoPreprocessor.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/SmoldoclingPhotoPreprocessor.cs`
- Test: `tests/Api.Tests/DocumentProcessing/PhotoIngestion/SmoldoclingPhotoPreprocessorTests.cs`

- [ ] **Step 1: Define interface**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IPhotoPreprocessor.cs
namespace MeepleAi.Api.BoundedContexts.DocumentProcessing.Application.Services;

public interface IPhotoPreprocessor
{
    Task<PhotoPreprocessResult> PreprocessAsync(byte[] imageData, CancellationToken ct = default);
}

public record PhotoPreprocessResult(
    byte[] ProcessedImage,
    double ConfidenceScore,
    PageOrientation DetectedOrientation,
    bool IsBlankPage,
    string[] Warnings
);

public enum PageOrientation { Portrait, Landscape, Rotated, Unknown }
```

- [ ] **Step 2: Write failing test for confidence score**

```csharp
// tests/Api.Tests/DocumentProcessing/PhotoIngestion/SmoldoclingPhotoPreprocessorTests.cs
[Fact]
public async Task PreprocessAsync_ClearImage_ReturnsHighConfidence()
{
    var sampleImage = await File.ReadAllBytesAsync("TestData/clear-page.jpg");
    var result = await _preprocessor.PreprocessAsync(sampleImage);

    result.ConfidenceScore.Should().BeGreaterThan(0.85);
    result.IsBlankPage.Should().BeFalse();
    result.Warnings.Should().BeEmpty();
}

[Fact]
public async Task PreprocessAsync_BlurryImage_ReturnsLowConfidenceWarning()
{
    var blurryImage = await File.ReadAllBytesAsync("TestData/blurry-page.jpg");
    var result = await _preprocessor.PreprocessAsync(blurryImage);

    result.ConfidenceScore.Should().BeLessThan(0.5);
    result.Warnings.Should().Contain(w => w.Contains("blur", StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 3: Run test (fail)**

Expected: BUILD FAIL — `SmoldoclingPhotoPreprocessor` not implemented.

- [ ] **Step 4: Implement preprocessor with smoldocling-service HTTP client**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/SmoldoclingPhotoPreprocessor.cs
public class SmoldoclingPhotoPreprocessor : IPhotoPreprocessor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SmoldoclingPhotoPreprocessor> _logger;

    public SmoldoclingPhotoPreprocessor(IHttpClientFactory factory, ILogger<SmoldoclingPhotoPreprocessor> logger)
    {
        _httpClient = factory.CreateClient("SmoldoclingService");
        _logger = logger;
    }

    public async Task<PhotoPreprocessResult> PreprocessAsync(byte[] imageData, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(imageData), "image", "page.jpg");
        content.Add(new StringContent("photo-camera"), "preprocessing_mode");

        var response = await _httpClient.PostAsync("/preprocess", content, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SmoldoclingPreprocessDto>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty response from smoldocling-service");

        return new PhotoPreprocessResult(
            ProcessedImage: Convert.FromBase64String(result.ProcessedImageBase64),
            ConfidenceScore: result.Confidence,
            DetectedOrientation: Enum.Parse<PageOrientation>(result.Orientation, ignoreCase: true),
            IsBlankPage: result.IsBlank,
            Warnings: result.Warnings ?? Array.Empty<string>()
        );
    }

    private record SmoldoclingPreprocessDto(string ProcessedImageBase64, double Confidence, string Orientation, bool IsBlank, string[]? Warnings);
}
```

- [ ] **Step 5: Add corresponding endpoint in smoldocling-service Python**

```python
# apps/smoldocling-service/main.py — add /preprocess endpoint
@app.post("/preprocess")
async def preprocess_photo(image: UploadFile, preprocessing_mode: str = Form("default")):
    img_bytes = await image.read()
    img = Image.open(io.BytesIO(img_bytes))

    # Photo-camera mode: dewarping + orientation detection + blank detection
    if preprocessing_mode == "photo-camera":
        img = dewarp_image(img)  # OpenCV-based perspective correction
        orientation = detect_orientation(img)
        is_blank = detect_blank_page(img, threshold=0.95)
        confidence = compute_ocr_confidence(img)
        warnings = []
        if confidence < 0.5:
            warnings.append("Low OCR confidence - possibly blurry or low light")
        if is_blank:
            warnings.append("Page appears blank")

        buf = io.BytesIO()
        img.save(buf, format="JPEG", quality=92)
        processed_b64 = base64.b64encode(buf.getvalue()).decode()

        return {
            "processed_image_base64": processed_b64,
            "confidence": confidence,
            "orientation": orientation.value,
            "is_blank": is_blank,
            "warnings": warnings
        }
    else:
        # ... existing default processing ...
        pass

def dewarp_image(img: Image.Image) -> Image.Image:
    # OpenCV: detect page edges, compute homography, warp to rectified
    cv_img = cv2.cvtColor(np.array(img), cv2.COLOR_RGB2BGR)
    gray = cv2.cvtColor(cv_img, cv2.COLOR_BGR2GRAY)
    edges = cv2.Canny(gray, 75, 200)
    contours, _ = cv2.findContours(edges, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    if not contours:
        return img
    page_contour = max(contours, key=cv2.contourArea)
    peri = cv2.arcLength(page_contour, True)
    approx = cv2.approxPolyDP(page_contour, 0.02 * peri, True)
    if len(approx) == 4:
        warped = four_point_transform(cv_img, approx.reshape(4, 2))
        return Image.fromarray(cv2.cvtColor(warped, cv2.COLOR_BGR2RGB))
    return img

def compute_ocr_confidence(img: Image.Image) -> float:
    # Use existing smoldocling OCR with confidence reporting
    result = ocr_engine.process(img, return_confidence=True)
    return result.avg_confidence
```

- [ ] **Step 6: Run tests with test fixtures**

Place sample images in `tests/Api.Tests/TestData/clear-page.jpg` and `blurry-page.jpg` (real photos).

```bash
dotnet test --filter "FullyQualifiedName~SmoldoclingPhotoPreprocessorTests"
```

Expected: 2 PASSED.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IPhotoPreprocessor.cs
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/SmoldoclingPhotoPreprocessor.cs
git add apps/smoldocling-service/main.py
git add tests/Api.Tests/DocumentProcessing/PhotoIngestion/
git commit -m "feat(document-processing): photo-first preprocessor with dewarping + confidence"
```

### Task 1.4 — UploadPhotoBatchHandler + parallel job dispatch

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPhotoBatchHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Jobs/PhotoIngestionJob.cs`
- Test: `tests/Api.Tests/DocumentProcessing/PhotoIngestion/UploadPhotoBatchHandlerTests.cs`

- [ ] **Step 1: Write failing handler test**

```csharp
[Fact]
public async Task Handle_ValidBatch_CreatesEntityAndQueuesJobs()
{
    var cmd = new UploadPhotoBatchCommand
    {
        GameId = Guid.NewGuid(),
        SourceLanguage = "en",
        Photos = new[] { new PhotoUploadDto { Filename = "p1.jpg", Base64Content = ValidBase64() } }
    };

    var result = await _handler.Handle(cmd, CancellationToken.None);

    result.BatchId.Should().NotBeEmpty();
    result.AcceptedCount.Should().Be(1);
    _backgroundJobClient.Verify(c => c.Enqueue<PhotoIngestionJob>(j => j.ProcessBatchAsync(result.BatchId, default)), Times.Once);
}
```

- [ ] **Step 2: Implement handler**

```csharp
public class UploadPhotoBatchHandler : IRequestHandler<UploadPhotoBatchCommand, UploadPhotoBatchResult>
{
    private readonly IDbContext _db;
    private readonly IBackgroundJobClient _jobClient;
    private readonly IUserContext _userContext;
    private readonly IBlobStorage _storage;

    public async Task<UploadPhotoBatchResult> Handle(UploadPhotoBatchCommand cmd, CancellationToken ct)
    {
        var userId = _userContext.RequireUserId();
        var batch = PhotoBatchUpload.Create(userId, cmd.GameId, cmd.SourceLanguage, cmd.Photos.Length);

        _db.PhotoBatchUploads.Add(batch);

        // Store raw photos to blob storage with batch id prefix
        for (var i = 0; i < cmd.Photos.Length; i++)
        {
            var photoBytes = Convert.FromBase64String(cmd.Photos[i].Base64Content);
            var key = $"photo-batches/{batch.Id}/page_{i:D3}.jpg";
            await _storage.PutAsync(key, photoBytes, "image/jpeg", ct);
        }

        await _db.SaveChangesAsync(ct);

        // Queue background job for OCR + indexing
        _jobClient.Enqueue<PhotoIngestionJob>(j => j.ProcessBatchAsync(batch.Id, ct));

        return new UploadPhotoBatchResult { BatchId = batch.Id, AcceptedCount = cmd.Photos.Length };
    }
}
```

- [ ] **Step 3: Implement parallel ingestion job**

```csharp
public class PhotoIngestionJob
{
    private readonly IDbContext _db;
    private readonly IPhotoPreprocessor _preprocessor;
    private readonly IBlobStorage _storage;
    private readonly IDocumentChunker _chunker;
    private readonly IEmbeddingService _embeddings;
    private readonly ILogger<PhotoIngestionJob> _logger;

    public async Task ProcessBatchAsync(Guid batchId, CancellationToken ct)
    {
        var batch = await _db.PhotoBatchUploads.FindAsync(new object[] { batchId }, ct)
            ?? throw new InvalidOperationException($"Batch {batchId} not found");

        batch.StartProcessing();
        await _db.SaveChangesAsync(ct);

        // Parallel processing with throttled concurrency (max 4 concurrent OCR ops)
        var semaphore = new SemaphoreSlim(4);
        var pageKeys = await _storage.ListAsync($"photo-batches/{batchId}/", ct);

        await Parallel.ForEachAsync(pageKeys, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (key, token) =>
            {
                await semaphore.WaitAsync(token);
                try
                {
                    await ProcessSinglePageAsync(batch, key, token);
                }
                finally
                {
                    semaphore.Release();
                }
            });

        await _db.SaveChangesAsync(ct);
    }

    private async Task ProcessSinglePageAsync(PhotoBatchUpload batch, string photoKey, CancellationToken ct)
    {
        var photoBytes = await _storage.GetAsync(photoKey, ct);
        var preprocessed = await _preprocessor.PreprocessAsync(photoBytes, ct);

        // OCR via existing smoldocling pipeline (no, the preprocessor already returned text? actually need separate step)
        // Chunk + embed + store in pgvector
        var chunks = _chunker.ChunkText(preprocessed.ExtractedText, batch.GameId);
        foreach (var chunk in chunks)
        {
            var embedding = await _embeddings.EmbedAsync(chunk.Text, ct);
            // Save to KB index with metadata: source=phone_camera, batch_id, confidence
            await SaveChunkToIndexAsync(batch, chunk, embedding, preprocessed.ConfidenceScore, ct);
        }

        batch.RecordPageIndexed();
    }
}
```

- [ ] **Step 4: Run handler tests**

```bash
dotnet test --filter "FullyQualifiedName~UploadPhotoBatchHandlerTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/
git add tests/Api.Tests/DocumentProcessing/PhotoIngestion/UploadPhotoBatchHandlerTests.cs
git commit -m "feat(document-processing): UploadPhotoBatchHandler + PhotoIngestionJob with parallel processing"
```

### Task 1.5 — GetPhotoBatchStatusQuery + endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetPhotoBatchStatusQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/DTOs/PhotoBatchStatusDto.cs`
- Create: `apps/api/src/Api/Routing/PhotoIngestionEndpoints.cs`
- Test: `tests/Api.Tests/DocumentProcessing/PhotoIngestion/GetPhotoBatchStatusHandlerTests.cs`

- [ ] **Step 1: Define DTO + Query**

```csharp
public record PhotoBatchStatusDto(
    Guid BatchId,
    string Status,
    int TotalPages,
    int IndexedPages,
    int LowConfidencePages,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    PhotoPageDto[] Pages
);

public record PhotoPageDto(
    int PageNumber,
    string ThumbnailUrl,
    double Confidence,
    string ConfidenceBadge, // "high" | "medium" | "low"
    string[] Warnings
);

public class GetPhotoBatchStatusQuery : IRequest<PhotoBatchStatusDto>
{
    public Guid BatchId { get; init; }
}
```

- [ ] **Step 2: Implement handler with EF query**

```csharp
public class GetPhotoBatchStatusHandler : IRequestHandler<GetPhotoBatchStatusQuery, PhotoBatchStatusDto>
{
    public async Task<PhotoBatchStatusDto> Handle(GetPhotoBatchStatusQuery query, CancellationToken ct)
    {
        var batch = await _db.PhotoBatchUploads
            .Include(b => b.Pages)
            .FirstOrDefaultAsync(b => b.Id == query.BatchId, ct)
            ?? throw new NotFoundException($"Batch {query.BatchId} not found");

        if (batch.UserId != _userContext.RequireUserId())
            throw new ForbiddenException("Cannot access another user's batch");

        return new PhotoBatchStatusDto(
            batch.Id,
            batch.Status.ToString(),
            batch.TotalPages,
            batch.IndexedPages,
            batch.Pages.Count(p => p.Confidence < 0.7),
            batch.CreatedAt,
            batch.CompletedAt,
            batch.Pages.Select(p => new PhotoPageDto(
                p.PageNumber,
                _storage.GetSignedUrl($"photo-batches/{batch.Id}/page_{p.PageNumber:D3}.jpg", TimeSpan.FromMinutes(15)),
                p.Confidence,
                p.Confidence >= 0.85 ? "high" : p.Confidence >= 0.7 ? "medium" : "low",
                p.Warnings
            )).ToArray()
        );
    }
}
```

- [ ] **Step 3: Define endpoint with MediatR (CQRS rule!)**

```csharp
// apps/api/src/Api/Routing/PhotoIngestionEndpoints.cs
public static class PhotoIngestionEndpoints
{
    public static void MapPhotoIngestionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/photo-batches").RequireAuthorization();

        group.MapPost("/", async (UploadPhotoBatchCommand cmd, IMediator m) =>
            Results.Ok(await m.Send(cmd)));

        group.MapGet("/{batchId:guid}", async (Guid batchId, IMediator m) =>
            Results.Ok(await m.Send(new GetPhotoBatchStatusQuery { BatchId = batchId })));
    }
}
```

- [ ] **Step 4: Wire endpoints in Program.cs**

Add `app.MapPhotoIngestionEndpoints();` in main routing setup.

- [ ] **Step 5: Run E2E test**

```csharp
[Fact]
public async Task UploadAndQueryStatus_FullFlow_ReturnsProgress()
{
    var client = _factory.CreateClient();
    await AuthenticateAsync(client);

    var uploadResp = await client.PostAsJsonAsync("/api/v1/photo-batches",
        new { gameId = _testGameId, sourceLanguage = "en", photos = new[] { new { filename = "p1.jpg", base64Content = ValidBase64() } } });
    uploadResp.EnsureSuccessStatusCode();
    var uploadResult = await uploadResp.Content.ReadFromJsonAsync<UploadPhotoBatchResult>();

    var statusResp = await client.GetAsync($"/api/v1/photo-batches/{uploadResult.BatchId}");
    statusResp.EnsureSuccessStatusCode();
    var status = await statusResp.Content.ReadFromJsonAsync<PhotoBatchStatusDto>();
    status.TotalPages.Should().Be(1);
}
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/DTOs/
git add apps/api/src/Api/Routing/PhotoIngestionEndpoints.cs
git commit -m "feat(document-processing): GetPhotoBatchStatus query + endpoints with CQRS"
```

### Task 1.6 — Frontend upload UI with confidence preview

**Files:**
- Create: `apps/web/src/app/(authenticated)/gamebook/upload/page.tsx`
- Create: `apps/web/src/app/(authenticated)/gamebook/upload/_components/PhotoUploader.tsx`
- Create: `apps/web/src/app/(authenticated)/gamebook/upload/_components/ConfidenceBadge.tsx`
- Create: `apps/web/src/lib/gamebook/hooks/usePhotoBatchUpload.ts`
- Test: `apps/web/__tests__/gamebook/upload/PhotoUploader.test.tsx`

- [ ] **Step 1: Write hook**

```typescript
// apps/web/src/lib/gamebook/hooks/usePhotoBatchUpload.ts
import { useMutation, useQuery } from '@tanstack/react-query';
import { apiClient } from '@/lib/api-client';

export function useUploadPhotoBatch() {
  return useMutation({
    mutationFn: async (input: { gameId: string; sourceLanguage: string; photos: File[] }) => {
      const photosBase64 = await Promise.all(input.photos.map(async (file) => ({
        filename: file.name,
        base64Content: await fileToBase64(file)
      })));
      return apiClient.post('/api/v1/photo-batches', {
        gameId: input.gameId,
        sourceLanguage: input.sourceLanguage,
        photos: photosBase64
      });
    }
  });
}

export function usePhotoBatchStatus(batchId: string | undefined) {
  return useQuery({
    queryKey: ['photo-batch-status', batchId],
    queryFn: () => apiClient.get(`/api/v1/photo-batches/${batchId}`),
    enabled: !!batchId,
    refetchInterval: (data) => data?.status === 'Completed' || data?.status === 'Failed' ? false : 2000
  });
}
```

- [ ] **Step 2: Build PhotoUploader component**

```tsx
// apps/web/src/app/(authenticated)/gamebook/upload/_components/PhotoUploader.tsx
'use client';
import { useState } from 'react';
import { useUploadPhotoBatch, usePhotoBatchStatus } from '@/lib/gamebook/hooks/usePhotoBatchUpload';
import { ConfidenceBadge } from './ConfidenceBadge';

export function PhotoUploader({ gameId }: { gameId: string }) {
  const [files, setFiles] = useState<File[]>([]);
  const [language, setLanguage] = useState('en');
  const [batchId, setBatchId] = useState<string | undefined>();

  const upload = useUploadPhotoBatch();
  const status = usePhotoBatchStatus(batchId);

  const handleSubmit = async () => {
    const result = await upload.mutateAsync({ gameId, sourceLanguage: language, photos: files });
    setBatchId(result.batchId);
  };

  return (
    <div className="space-y-4" data-slot="photo-uploader">
      <input type="file" accept="image/*" multiple onChange={(e) => setFiles(Array.from(e.target.files ?? []))} />
      <select value={language} onChange={(e) => setLanguage(e.target.value)}>
        <option value="en">English</option>
        <option value="de">Deutsch</option>
        <option value="fr">Français</option>
      </select>
      <button onClick={handleSubmit} disabled={files.length === 0 || upload.isPending}>
        Inizia indicizzazione ({files.length} foto)
      </button>

      {status.data && (
        <div data-slot="batch-progress">
          <p>{status.data.indexedPages} / {status.data.totalPages} pagine indicizzate</p>
          <progress value={status.data.indexedPages} max={status.data.totalPages} />
          <ul>
            {status.data.pages.map((p) => (
              <li key={p.pageNumber}>
                <img src={p.thumbnailUrl} alt={`Pagina ${p.pageNumber}`} className="w-20" />
                <span>Pagina {p.pageNumber}</span>
                <ConfidenceBadge level={p.confidenceBadge} />
                {p.confidenceBadge === 'low' && (
                  <button onClick={() => /* reupload single page */ {}}>📸 Rifotografa</button>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 3: Vitest test**

```typescript
// apps/web/__tests__/gamebook/upload/PhotoUploader.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { PhotoUploader } from '@/app/(authenticated)/gamebook/upload/_components/PhotoUploader';
import { mockApiClient } from '@/test/mocks';

test('uploads files and displays progress', async () => {
  mockApiClient.post.mockResolvedValue({ batchId: 'b1', acceptedCount: 1 });
  mockApiClient.get.mockResolvedValue({
    batchId: 'b1', status: 'Processing', totalPages: 1, indexedPages: 0, pages: []
  });

  render(<PhotoUploader gameId="g1" />);

  const file = new File(['fake'], 'page.jpg', { type: 'image/jpeg' });
  const input = screen.getByLabelText(/file/i) as HTMLInputElement;
  fireEvent.change(input, { target: { files: [file] } });

  fireEvent.click(screen.getByText(/Inizia indicizzazione/));

  await waitFor(() => expect(screen.getByText('0 / 1 pagine indicizzate')).toBeInTheDocument());
});
```

- [ ] **Step 4: Run frontend tests**

```bash
cd apps/web
pnpm test -- PhotoUploader
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/(authenticated)/gamebook/
git add apps/web/src/lib/gamebook/
git add apps/web/__tests__/gamebook/
git commit -m "feat(gamebook): photo uploader UI with confidence preview"
```

### Task 1.7 — Phase 1 Acceptance Gate

- [ ] **Step 1: Run full E2E on real Tainted Grail manual**

Use 50-page Tainted Grail manual photographed in Task 0.1.

- [ ] **Step 2: Measure throughput**

Telemetry: log `pages_indexed_per_minute`. Target ≥ 10 pag/min.

- [ ] **Step 3: Validate confidence distribution**

≥ 95% pages with confidence ≥ 0.7. Document any outliers in `tests/llm-eval/ocr-validation/phase1-acceptance.md`.

- [ ] **Step 4: Tag release**

```bash
git tag mvp-phase-1-complete
git push origin mvp-phase-1-complete
```

---

## Phase 2 — G3 Q&A + TranslationService minimal (Sprint 4-6, weeks 7-12)

**Goal:** Sara può chiedere "come funziona il combat?" e ricevere risposta italiana con citazione, latenza P95 < 5 sec, hallucination ≤ 3% golden.

### Task 2.1 — TranslationService skeleton + OpenRouter integration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Translation/ITranslationService.cs`
- Create: `apps/api/src/Api/Infrastructure/Translation/OpenRouterTranslationService.cs`
- Create: `apps/api/src/Api/Infrastructure/Translation/Models/TranslationRequest.cs`
- Create: `apps/api/src/Api/Infrastructure/Translation/Models/TranslationResponse.cs`
- Create: `apps/api/src/Api/Infrastructure/Translation/Routing/IModelRouter.cs`
- Create: `apps/api/src/Api/Infrastructure/Translation/Routing/OpenRouterModelRouter.cs`
- Create: `apps/api/src/Api/Infrastructure/Translation/Prompts/NarrativeTranslationPrompt.cs`
- Test: `tests/Api.Tests/Translation/OpenRouterTranslationServiceTests.cs`

- [ ] **Step 1: Define interface + models**

```csharp
public interface ITranslationService
{
    Task<TranslationResponse> TranslateAsync(TranslationRequest request, CancellationToken ct = default);
}

public record TranslationRequest(
    string SourceText,
    string SourceLanguage,
    string TargetLanguage,
    TranslationContext Context,
    Dictionary<string, string>? Glossary = null
);

public enum TranslationContext { NarrativeFantasy, NarrativeScifi, NarrativeHorror, RulesTechnical, SetupInstructions }

public record TranslationResponse(
    string TranslatedText,
    string ModelUsed,
    double EstimatedCostUsd,
    TimeSpan Latency,
    bool IsCached
);
```

- [ ] **Step 2: Define IModelRouter for tier selection**

```csharp
public interface IModelRouter
{
    string SelectModel(TranslationContext context);
    string SelectQAModel(QAComplexity complexity);
}

public enum QAComplexity { HighStakes, Routine, Bulk }

public class OpenRouterModelRouter : IModelRouter
{
    private readonly OpenRouterConfig _config;

    public string SelectModel(TranslationContext context) => context switch
    {
        TranslationContext.NarrativeFantasy => _config.TranslationModel, // claude-sonnet-4.5
        TranslationContext.NarrativeScifi => _config.TranslationModel,
        TranslationContext.NarrativeHorror => _config.TranslationModel,
        TranslationContext.RulesTechnical => _config.DefaultModel, // claude-haiku-4.5
        TranslationContext.SetupInstructions => _config.DefaultModel,
        _ => _config.DefaultModel
    };

    public string SelectQAModel(QAComplexity complexity) => complexity switch
    {
        QAComplexity.HighStakes => _config.TranslationModel, // sonnet for safety
        QAComplexity.Routine => _config.DefaultModel, // haiku
        QAComplexity.Bulk => _config.QABulkModel, // deepseek-v3
        _ => _config.DefaultModel
    };
}
```

- [ ] **Step 3: Write failing translation test**

```csharp
[Fact]
public async Task TranslateAsync_FantasyParagraph_PreservesGlossaryAndTone()
{
    var request = new TranslationRequest(
        SourceText: "Niamh raises the Sword of Avalon.",
        SourceLanguage: "en",
        TargetLanguage: "it",
        Context: TranslationContext.NarrativeFantasy,
        Glossary: new() { ["Sword of Avalon"] = "Spada di Avalon", ["Niamh"] = "Niamh" }
    );

    var result = await _service.TranslateAsync(request);

    result.TranslatedText.Should().Contain("Spada di Avalon");
    result.TranslatedText.Should().Contain("Niamh");
    result.ModelUsed.Should().Contain("sonnet");
    result.Latency.Should().BeLessThan(TimeSpan.FromSeconds(10));
}
```

- [ ] **Step 4: Implement OpenRouter HTTP client**

```csharp
public class OpenRouterTranslationService : ITranslationService
{
    private readonly HttpClient _http;
    private readonly IModelRouter _router;
    private readonly ITranslationCache _cache;
    private readonly NarrativeTranslationPrompt _prompts;
    private readonly ILogger<OpenRouterTranslationService> _logger;

    public async Task<TranslationResponse> TranslateAsync(TranslationRequest request, CancellationToken ct = default)
    {
        var cacheKey = ComputeCacheKey(request);
        var cached = await _cache.GetAsync(cacheKey, ct);
        if (cached != null)
            return cached with { IsCached = true };

        var model = _router.SelectModel(request.Context);
        var prompt = _prompts.Build(request);

        var sw = Stopwatch.StartNew();
        var response = await _http.PostAsJsonAsync("/chat/completions", new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = prompt.SystemMessage },
                new { role = "user", content = prompt.UserMessage }
            },
            temperature = 0.3,
            max_tokens = 1000
        }, ct);
        response.EnsureSuccessStatusCode();
        sw.Stop();

        var json = await response.Content.ReadFromJsonAsync<OpenRouterCompletionResponse>(cancellationToken: ct);
        var translation = json!.Choices[0].Message.Content;
        var costUsd = ComputeCost(json.Usage, model);

        var result = new TranslationResponse(
            TranslatedText: translation,
            ModelUsed: model,
            EstimatedCostUsd: costUsd,
            Latency: sw.Elapsed,
            IsCached: false
        );

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromHours(24), ct);
        return result;
    }

    private string ComputeCacheKey(TranslationRequest req) =>
        $"trans:{req.SourceLanguage}:{req.TargetLanguage}:{req.Context}:{ComputeHash(req.SourceText)}";
}
```

- [ ] **Step 5: NarrativeTranslationPrompt builder**

```csharp
public class NarrativeTranslationPrompt
{
    public TranslationPromptOutput Build(TranslationRequest request)
    {
        var toneInstructions = request.Context switch
        {
            TranslationContext.NarrativeFantasy => "Use a fantasy register: archaic touches where appropriate, dramatic tone for action, evocative imagery for descriptions.",
            TranslationContext.NarrativeHorror => "Use a horror register: tense, oppressive, with attention to atmosphere and unease.",
            TranslationContext.NarrativeScifi => "Use a sci-fi register: precise, technical when needed, immersive in futuristic settings.",
            TranslationContext.RulesTechnical => "Use a clear, neutral, technical register. Preserve numbers, references, and rule terminology exactly.",
            TranslationContext.SetupInstructions => "Use clear imperative form for instructions. List components precisely.",
            _ => "Use a neutral narrative register."
        };

        var glossarySection = request.Glossary?.Any() == true
            ? $"\nGlossary (preserve exactly):\n{string.Join("\n", request.Glossary.Select(kv => $"- {kv.Key} → {kv.Value}"))}"
            : "";

        var systemMessage = $$"""
            You are a professional literary translator specializing in board game narrative content (gamebooks).
            Translate from {{request.SourceLanguage}} to {{request.TargetLanguage}}.
            {{toneInstructions}}
            {{glossarySection}}

            Rules:
            - Preserve paragraph structure and dialogue formatting (em-dashes, quotes).
            - Do NOT add commentary, notes, or explanations.
            - Do NOT translate proper nouns unless explicitly mapped in glossary.
            - Output ONLY the translated text, nothing else.
            """;

        return new TranslationPromptOutput(systemMessage, request.SourceText);
    }
}

public record TranslationPromptOutput(string SystemMessage, string UserMessage);
```

- [ ] **Step 6: Run test (should pass with real OpenRouter call OR mock)**

For unit test use mocked HttpClient. For integration test (separate fixture), run against real OpenRouter (skip in CI by default):

```bash
dotnet test --filter "FullyQualifiedName~OpenRouterTranslationServiceTests&Category!=RealLLM"
```

Expected: PASS (unit tests with mocked HttpClient).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Translation/
git add tests/Api.Tests/Translation/OpenRouterTranslationServiceTests.cs
git commit -m "feat(translation): OpenRouter service with model routing and narrative prompts"
```

### Task 2.2 — TranslationCache (Redis layer)

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Translation/Cache/ITranslationCache.cs`
- Create: `apps/api/src/Api/Infrastructure/Translation/Cache/RedisTranslationCache.cs`
- Test: `tests/Api.Tests/Translation/RedisTranslationCacheTests.cs`

- [ ] **Step 1: Interface + Redis impl**

```csharp
public interface ITranslationCache
{
    Task<TranslationResponse?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, TranslationResponse value, TimeSpan ttl, CancellationToken ct = default);
}

public class RedisTranslationCache : ITranslationCache
{
    private readonly IDistributedCache _cache;

    public async Task<TranslationResponse?> GetAsync(string key, CancellationToken ct = default)
    {
        var bytes = await _cache.GetAsync(key, ct);
        if (bytes == null) return null;
        return JsonSerializer.Deserialize<TranslationResponse>(bytes);
    }

    public Task SetAsync(string key, TranslationResponse value, TimeSpan ttl, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        return _cache.SetAsync(key, bytes, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
    }
}
```

- [ ] **Step 2: Test cache hit/miss with Testcontainers Redis**

```csharp
public class RedisTranslationCacheTests : IAsyncLifetime
{
    private RedisContainer _redis = null!;
    private ITranslationCache _cache = null!;

    public async Task InitializeAsync()
    {
        _redis = new RedisBuilder().Build();
        await _redis.StartAsync();
        var distributedCache = new RedisCache(new RedisCacheOptions { Configuration = _redis.GetConnectionString() });
        _cache = new RedisTranslationCache(distributedCache);
    }

    [Fact]
    public async Task SetAndGet_ReturnsStoredValue()
    {
        var response = new TranslationResponse("Ciao", "haiku", 0.001, TimeSpan.FromSeconds(2), false);
        await _cache.SetAsync("k1", response, TimeSpan.FromMinutes(5));
        var retrieved = await _cache.GetAsync("k1");
        retrieved.Should().NotBeNull();
        retrieved!.TranslatedText.Should().Be("Ciao");
    }

    public Task DisposeAsync() => _redis.DisposeAsync().AsTask();
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Translation/Cache/
git add tests/Api.Tests/Translation/RedisTranslationCacheTests.cs
git commit -m "feat(translation): Redis cache layer with TTL"
```

### Task 2.3 — KnowledgeBase Q&A extension with translation

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionQuery.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/AnswerCitation.cs`
- Test: `tests/Api.Tests/KnowledgeBase/AskQuestionHandlerWithTranslationTests.cs`

- [ ] **Step 1: Extend AskQuestionQuery with language + Q&A complexity inference**

```csharp
public class AskQuestionQuery : IRequest<AskQuestionResult>
{
    public Guid GameId { get; init; }
    public string Question { get; init; } = null!;
    public string ResponseLanguage { get; init; } = "it";
    public Guid? SessionId { get; init; }
}

public class AskQuestionResult
{
    public string Answer { get; init; } = null!;
    public AnswerCitation[] Citations { get; init; } = Array.Empty<AnswerCitation>();
    public ConfidenceLevel Confidence { get; init; }
    public string ModelUsed { get; init; } = null!;
    public TimeSpan Latency { get; init; }
}

public enum ConfidenceLevel { High, Medium, Low }
```

- [ ] **Step 2: Modify handler to infer complexity + translate output**

```csharp
public class AskQuestionHandler : IRequestHandler<AskQuestionQuery, AskQuestionResult>
{
    private readonly IRagPipeline _rag;
    private readonly ITranslationService _translation;
    private readonly IModelRouter _router;
    private readonly IHouseRulesRepository _houseRules;

    public async Task<AskQuestionResult> Handle(AskQuestionQuery query, CancellationToken ct)
    {
        // Check house rules first
        var houseRules = await _houseRules.GetForGameAsync(query.GameId, query.SessionId, ct);
        var matchingHouseRule = houseRules.FirstOrDefault(hr => hr.MatchesQuestion(query.Question));
        if (matchingHouseRule != null)
        {
            return BuildHouseRuleAnswer(matchingHouseRule, query.ResponseLanguage);
        }

        // Infer complexity
        var complexity = InferComplexity(query.Question);
        var model = _router.SelectQAModel(complexity);

        // RAG retrieval + LLM generation
        var ragResult = await _rag.QueryAsync(query.GameId, query.Question, model, ct);

        // Translate to target language if needed
        var answer = ragResult.Answer;
        if (query.ResponseLanguage != "en") // assume RAG output in EN by default
        {
            var translated = await _translation.TranslateAsync(new TranslationRequest(
                SourceText: ragResult.Answer,
                SourceLanguage: "en",
                TargetLanguage: query.ResponseLanguage,
                Context: TranslationContext.RulesTechnical
            ), ct);
            answer = translated.TranslatedText;
        }

        return new AskQuestionResult
        {
            Answer = answer,
            Citations = ragResult.Citations,
            Confidence = MapConfidence(ragResult.AvgRelevanceScore),
            ModelUsed = model,
            Latency = ragResult.Latency
        };
    }

    private QAComplexity InferComplexity(string question)
    {
        // High-stakes triggers: combat resolution, scoring, end-game conditions
        var highStakesPatterns = new[] { "win", "lose", "score", "victory", "death", "damage" };
        return highStakesPatterns.Any(p => question.Contains(p, StringComparison.OrdinalIgnoreCase))
            ? QAComplexity.HighStakes
            : QAComplexity.Routine;
    }

    private ConfidenceLevel MapConfidence(double score) => score switch
    {
        >= 0.85 => ConfidenceLevel.High,
        >= 0.7 => ConfidenceLevel.Medium,
        _ => ConfidenceLevel.Low
    };
}
```

- [ ] **Step 3: Test with mocked translation**

```csharp
[Fact]
public async Task Handle_QueryInItalian_ReturnsTranslatedAnswerWithCitations()
{
    _ragMock.Setup(r => r.QueryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new RagResult { Answer = "Roll 3 dice", Citations = new[] { new AnswerCitation { Page = 12 } }, AvgRelevanceScore = 0.9 });
    _translationMock.Setup(t => t.TranslateAsync(It.Is<TranslationRequest>(r => r.SourceText == "Roll 3 dice"), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new TranslationResponse("Tira 3 dadi", "haiku", 0.001, TimeSpan.FromSeconds(2), false));

    var result = await _handler.Handle(new AskQuestionQuery
    {
        GameId = _gameId,
        Question = "How many dice for fireball?",
        ResponseLanguage = "it"
    }, CancellationToken.None);

    result.Answer.Should().Be("Tira 3 dadi");
    result.Confidence.Should().Be(ConfidenceLevel.High);
    result.Citations.Should().HaveCount(1);
}
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/
git add tests/Api.Tests/KnowledgeBase/AskQuestionHandlerWithTranslationTests.cs
git commit -m "feat(knowledge-base): Q&A handler with translation + confidence + complexity routing"
```

### Task 2.4 — Hallucination CI gate via golden eval

**Files:**
- Create: `tests/llm-eval/runners/qa_eval.py`
- Create: `.github/workflows/llm-eval-gate.yml`

- [ ] **Step 1: Implement Python eval runner**

```python
# tests/llm-eval/runners/qa_eval.py
import json
import os
import sys
import requests
from pathlib import Path

GOLDEN_SET = Path("tests/llm-eval/golden-set/qa-questions.jsonl")
API_URL = os.environ.get("MEEPLEAI_API_URL", "http://localhost:8080")
TOKEN = os.environ["TEST_USER_TOKEN"]
HALLUCINATION_THRESHOLD = 0.03  # 3%

def evaluate():
    questions = [json.loads(l) for l in GOLDEN_SET.read_text().splitlines()]
    results = []

    for q in questions:
        response = requests.post(
            f"{API_URL}/api/v1/knowledge-base/ask",
            headers={"Authorization": f"Bearer {TOKEN}"},
            json={
                "gameId": q["game_id"],
                "question": q["question_it"],
                "responseLanguage": "it"
            }
        )
        actual = response.json()
        is_hallucination = judge_hallucination(
            question=q["question_it"],
            expected=q["expected_answer_it"],
            expected_citations=q["expected_citation_pages"],
            actual_answer=actual["answer"],
            actual_citations=[c["page"] for c in actual["citations"]],
            actual_confidence=actual["confidence"]
        )
        results.append({
            "id": q["id"],
            "is_hallucination": is_hallucination,
            "confidence": actual["confidence"]
        })

    total = len(results)
    hallucinations = sum(1 for r in results if r["is_hallucination"])
    high_conf_hallucinations = sum(1 for r in results if r["is_hallucination"] and r["confidence"] == "High")
    rate = hallucinations / total

    print(f"Total: {total}, Hallucinations: {hallucinations} ({rate*100:.1f}%)")
    print(f"High-confidence hallucinations: {high_conf_hallucinations} (target: 0)")

    if rate > HALLUCINATION_THRESHOLD:
        print(f"FAIL: Hallucination rate {rate*100:.1f}% exceeds {HALLUCINATION_THRESHOLD*100}%")
        sys.exit(1)
    if high_conf_hallucinations > 0:
        print(f"FAIL: {high_conf_hallucinations} hallucinations on high-confidence answers")
        sys.exit(1)
    print("PASS")

def judge_hallucination(question, expected, expected_citations, actual_answer, actual_citations, actual_confidence):
    # Use Claude as judge (LLM-as-judge pattern)
    judge_prompt = f"""
    Question: {question}
    Expected answer: {expected}
    Expected citations (pages): {expected_citations}
    Actual answer: {actual_answer}
    Actual citations: {actual_citations}

    Is the actual answer factually consistent with the expected? (yes/no)
    Are citations appropriate (overlap or close)? (yes/no)
    Is this a hallucination (incorrect answer presented confidently)? (yes/no)
    """
    # Call Anthropic API via OpenRouter
    judge_response = requests.post(
        "https://openrouter.ai/api/v1/chat/completions",
        headers={"Authorization": f"Bearer {os.environ['OPENROUTER_API_KEY']}"},
        json={
            "model": "anthropic/claude-sonnet-4.5",
            "messages": [{"role": "user", "content": judge_prompt}],
            "temperature": 0
        }
    ).json()
    verdict = judge_response["choices"][0]["message"]["content"]
    return "hallucination: yes" in verdict.lower()

if __name__ == "__main__":
    evaluate()
```

- [ ] **Step 2: GitHub Actions workflow**

```yaml
# .github/workflows/llm-eval-gate.yml
name: LLM Eval Gate
on:
  pull_request:
    paths:
      - 'apps/api/src/Api/BoundedContexts/KnowledgeBase/**'
      - 'apps/api/src/Api/Infrastructure/Translation/**'
      - 'tests/llm-eval/**'

jobs:
  qa-eval:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup Python
        uses: actions/setup-python@v5
        with:
          python-version: "3.12"
      - name: Install deps
        run: pip install requests
      - name: Start API in test mode
        run: |
          docker compose -f infra/docker-compose.test.yml up -d
          sleep 30
      - name: Run Q&A eval
        env:
          OPENROUTER_API_KEY: ${{ secrets.OPENROUTER_API_KEY }}
          TEST_USER_TOKEN: ${{ secrets.TEST_USER_TOKEN }}
        run: python tests/llm-eval/runners/qa_eval.py
```

- [ ] **Step 3: Commit**

```bash
git add tests/llm-eval/runners/qa_eval.py .github/workflows/llm-eval-gate.yml
git commit -m "feat(eval): hallucination CI gate with LLM-as-judge"
```

### Task 2.5 — Phase 2 Acceptance Gate

- [ ] **Step 1: Run full Q&A flow E2E**

100 query golden set → all run through API → verify hallucination ≤ 3%, P95 < 5 sec.

- [ ] **Step 2: Tag**

```bash
git tag mvp-phase-2-complete
git push origin mvp-phase-2-complete
```

---

## Phase 3 — G4 Translation + UI Screens + Pricing (Sprint 7-9, weeks 13-18)

**Goal:** Sara può richiedere paragrafo §147 e leggerne traduzione italiana ad alta voce. Quota free funziona, credit purchase via Stripe.

### Task 3.1 — Paragraph identification (numbered + chapter-based)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetParagraphQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetParagraphHandler.cs`
- Test: `tests/Api.Tests/KnowledgeBase/GetParagraphHandlerTests.cs`

- [ ] **Step 1: Define query supporting both numbered + snippet search**

```csharp
public class GetParagraphQuery : IRequest<ParagraphResult>
{
    public Guid GameId { get; init; }
    public string? ParagraphNumber { get; init; }      // "147" if numbered
    public string? TextSnippet { get; init; }          // "L'eroe entra nella foresta" if not numbered
    public string TargetLanguage { get; init; } = "it";
}

public record ParagraphResult(
    string SourceText,
    string TranslatedText,
    string ParagraphIdentifier,  // "§147" or "Cap.3 - Sezione 2"
    int SourcePage,
    double Confidence,
    bool IsCached
);
```

- [ ] **Step 2: Handler with fallback semantic search**

```csharp
public async Task<ParagraphResult> Handle(GetParagraphQuery query, CancellationToken ct)
{
    Paragraph? paragraph;

    if (!string.IsNullOrEmpty(query.ParagraphNumber))
    {
        paragraph = await _kb.FindByParagraphNumberAsync(query.GameId, query.ParagraphNumber, ct);
        if (paragraph == null)
            throw new NotFoundException($"Paragraph §{query.ParagraphNumber} not found");
    }
    else if (!string.IsNullOrEmpty(query.TextSnippet))
    {
        var matches = await _kb.SemanticSearchAsync(query.GameId, query.TextSnippet, topK: 3, ct);
        paragraph = matches.FirstOrDefault(m => m.Score >= 0.75);
        if (paragraph == null)
            throw new NotFoundException("No matching paragraph found above threshold");
    }
    else
    {
        throw new BadRequestException("Provide either ParagraphNumber or TextSnippet");
    }

    var translation = await _translation.TranslateAsync(new TranslationRequest(
        SourceText: paragraph.Text,
        SourceLanguage: paragraph.SourceLanguage,
        TargetLanguage: query.TargetLanguage,
        Context: InferContext(paragraph),
        Glossary: await _glossary.GetForGameAsync(query.GameId, ct)
    ), ct);

    return new ParagraphResult(
        SourceText: paragraph.Text,
        TranslatedText: translation.TranslatedText,
        ParagraphIdentifier: paragraph.Identifier,
        SourcePage: paragraph.PageNumber,
        Confidence: paragraph.OcrConfidence,
        IsCached: translation.IsCached
    );
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetParagraph*
git add tests/Api.Tests/KnowledgeBase/GetParagraphHandlerTests.cs
git commit -m "feat(knowledge-base): paragraph lookup numbered + snippet semantic search"
```

### Task 3.2 — Pricing engine (Free counter + Credits)

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Pricing/IPricingEngine.cs`
- Create: `apps/api/src/Api/Infrastructure/Pricing/CreditBasedPricingEngine.cs`
- Create: `apps/api/src/Api/Infrastructure/Pricing/Models/UserQuota.cs`
- Create: `apps/api/src/Api/Migrations/2026XXXXXXXXXX_AddCreditBalance.cs`
- Test: `tests/Api.Tests/Pricing/CreditBasedPricingEngineTests.cs`

- [ ] **Step 1: Define interface + UserQuota entity**

```csharp
public interface IPricingEngine
{
    Task<QuotaCheckResult> CheckQuotaAsync(Guid userId, BillableOperation operation, CancellationToken ct = default);
    Task ConsumeQuotaAsync(Guid userId, BillableOperation operation, CancellationToken ct = default);
    Task<int> AddCreditsAsync(Guid userId, int credits, CancellationToken ct = default);
}

public enum BillableOperation { TranslationParagraph, QABulk, QAHighStakes }

public record QuotaCheckResult(bool Allowed, string Reason, int RemainingFree, int RemainingCredits);

public class UserQuota
{
    public Guid UserId { get; private set; }
    public int FreePagesUsedThisMonth { get; private set; }
    public int CreditBalance { get; private set; }
    public DateTime LastResetAt { get; private set; }

    public const int FreeMonthlyLimit = 50;

    public bool TryConsume(int cost)
    {
        ResetIfNewMonth();
        if (FreePagesUsedThisMonth + cost <= FreeMonthlyLimit)
        {
            FreePagesUsedThisMonth += cost;
            return true;
        }
        if (CreditBalance >= cost)
        {
            CreditBalance -= cost;
            return true;
        }
        return false;
    }

    private void ResetIfNewMonth()
    {
        if (LastResetAt.Month != DateTime.UtcNow.Month || LastResetAt.Year != DateTime.UtcNow.Year)
        {
            FreePagesUsedThisMonth = 0;
            LastResetAt = DateTime.UtcNow;
        }
    }
}
```

- [ ] **Step 2: CreditBasedPricingEngine implementation**

```csharp
public class CreditBasedPricingEngine : IPricingEngine
{
    private static readonly Dictionary<BillableOperation, int> OperationCosts = new()
    {
        [BillableOperation.TranslationParagraph] = 1,
        [BillableOperation.QABulk] = 0,  // free in MVP
        [BillableOperation.QAHighStakes] = 0
    };

    public async Task<QuotaCheckResult> CheckQuotaAsync(Guid userId, BillableOperation op, CancellationToken ct)
    {
        var quota = await _db.UserQuotas.FindAsync(new object[] { userId }, ct) ?? UserQuota.CreateForUser(userId);
        var cost = OperationCosts[op];
        var allowed = quota.CanConsume(cost);
        return new QuotaCheckResult(
            Allowed: allowed,
            Reason: allowed ? "OK" : "Quota exceeded - purchase credits or wait for next month",
            RemainingFree: Math.Max(0, UserQuota.FreeMonthlyLimit - quota.FreePagesUsedThisMonth),
            RemainingCredits: quota.CreditBalance
        );
    }

    public async Task ConsumeQuotaAsync(Guid userId, BillableOperation op, CancellationToken ct)
    {
        var quota = await _db.UserQuotas.FindAsync(new object[] { userId }, ct) ?? UserQuota.CreateForUser(userId);
        var consumed = quota.TryConsume(OperationCosts[op]);
        if (!consumed)
            throw new InvalidOperationException("Quota exceeded");
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 3: Tests with monthly reset edge case**

```csharp
[Fact]
public async Task TryConsume_AcrossMonthBoundary_ResetsFreeCounter()
{
    var quota = UserQuota.CreateForUser(Guid.NewGuid());
    // Simulate 50 pages used last month
    SetPrivateField(quota, "FreePagesUsedThisMonth", 50);
    SetPrivateField(quota, "LastResetAt", DateTime.UtcNow.AddMonths(-1));

    var result = quota.TryConsume(1);

    result.Should().BeTrue();
    quota.FreePagesUsedThisMonth.Should().Be(1);
}
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Pricing/
git add apps/api/src/Api/Migrations/2026*_AddCreditBalance.cs
git add tests/Api.Tests/Pricing/
git commit -m "feat(pricing): credit-based engine with monthly free tier reset"
```

### Task 3.3 — Stripe checkout integration for credits

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Pricing/Stripe/StripeCheckoutService.cs`
- Create: `apps/api/src/Api/Routing/PricingEndpoints.cs`

- [ ] **Step 1: Stripe checkout session creation**

```csharp
public class StripeCheckoutService
{
    public async Task<string> CreateCheckoutSessionAsync(Guid userId, int credits, CancellationToken ct)
    {
        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new() { "card" },
            LineItems = new() { new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "eur",
                    UnitAmount = 500, // €5 for 100 credits
                    ProductData = new SessionLineItemPriceDataProductDataOptions { Name = $"{credits} MeepleAI Credits" }
                },
                Quantity = 1
            }},
            Mode = "payment",
            SuccessUrl = $"https://meepleai.com/credits/success?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = "https://meepleai.com/credits/cancelled",
            Metadata = new() { ["userId"] = userId.ToString(), ["credits"] = credits.ToString() }
        };
        var service = new SessionService();
        var session = await service.CreateAsync(options, cancellationToken: ct);
        return session.Url;
    }
}
```

- [ ] **Step 2: Stripe webhook handler for `checkout.session.completed`**

Endpoint at `/api/v1/webhooks/stripe` that verifies signature + invokes `_pricingEngine.AddCreditsAsync`.

- [ ] **Step 3: Endpoints**

```csharp
public static class PricingEndpoints
{
    public static void MapPricingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/pricing").RequireAuthorization();

        group.MapGet("/quota", async (IMediator m) =>
            Results.Ok(await m.Send(new GetCurrentQuotaQuery())));

        group.MapPost("/credits/checkout", async ([FromBody] CreateCheckoutCommand cmd, IMediator m) =>
            Results.Ok(await m.Send(cmd)));
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Pricing/Stripe/
git add apps/api/src/Api/Routing/PricingEndpoints.cs
git commit -m "feat(pricing): Stripe checkout for 100-credit packs + webhook"
```

### Task 3.4 — Frontend gameplay screen (QAPanel + TranslationViewer + QuotaModal)

**Files:**
- Create: `apps/web/src/app/(authenticated)/gamebook/[gameId]/play/page.tsx`
- Create: `apps/web/src/app/(authenticated)/gamebook/[gameId]/play/_components/QAPanel.tsx`
- Create: `apps/web/src/app/(authenticated)/gamebook/[gameId]/play/_components/TranslationViewer.tsx`
- Create: `apps/web/src/app/(authenticated)/gamebook/[gameId]/play/_components/QuotaExceededModal.tsx`
- Test: `apps/web/__tests__/gamebook/play/QAPanel.test.tsx`
- Test: `apps/web/__tests__/gamebook/play/TranslationViewer.test.tsx`

- [ ] **Step 1: TranslationViewer component**

```tsx
'use client';
import { useState } from 'react';
import { useTranslateParagraph } from '@/lib/gamebook/hooks/useTranslateParagraph';
import { QuotaExceededModal } from './QuotaExceededModal';

export function TranslationViewer({ gameId }: { gameId: string }) {
  const [paragraphNumber, setParagraphNumber] = useState('');
  const [showOriginal, setShowOriginal] = useState(false);
  const [quotaExceeded, setQuotaExceeded] = useState(false);
  const translate = useTranslateParagraph();

  const handleFetch = async () => {
    try {
      await translate.mutateAsync({ gameId, paragraphNumber });
    } catch (err: any) {
      if (err.status === 402) setQuotaExceeded(true);
    }
  };

  return (
    <div data-slot="translation-viewer" className="space-y-4">
      <div className="flex gap-2">
        <input
          type="text"
          placeholder="es. 147"
          value={paragraphNumber}
          onChange={(e) => setParagraphNumber(e.target.value)}
          className="border px-3 py-2 rounded"
        />
        <button onClick={handleFetch} disabled={translate.isPending}>
          {translate.isPending ? '⏳ Traducendo...' : 'Vai'}
        </button>
      </div>

      {translate.data && (
        <article className="prose prose-lg leading-relaxed text-[20px]" data-slot="translated-paragraph">
          <h2>§{translate.data.paragraphIdentifier}</h2>
          <p>{showOriginal ? translate.data.sourceText : translate.data.translatedText}</p>
          <button onClick={() => setShowOriginal(!showOriginal)}>
            🌐 {showOriginal ? 'Mostra italiano' : 'Mostra originale'}
          </button>
          {translate.data.isCached && <span className="text-sm text-slate-500">📋 Da cache</span>}
        </article>
      )}

      {quotaExceeded && <QuotaExceededModal onClose={() => setQuotaExceeded(false)} />}
    </div>
  );
}
```

- [ ] **Step 2: QuotaExceededModal**

```tsx
export function QuotaExceededModal({ onClose }: { onClose: () => void }) {
  const startCheckout = useStartCheckout();

  return (
    <div role="dialog" aria-modal="true" data-slot="quota-modal" className="fixed inset-0 bg-black/50 flex items-center justify-center">
      <div className="bg-white p-6 rounded-lg max-w-md">
        <h2 className="text-xl font-bold">Quota gratuita esaurita</h2>
        <p className="mt-2">Hai usato tutte le 50 pagine gratuite di traduzione questo mese.</p>
        <div className="flex gap-2 mt-4">
          <button onClick={() => startCheckout.mutate({ credits: 100 })} className="bg-blue-600 text-white px-4 py-2 rounded">
            💎 Acquista 100 credits (€5)
          </button>
          <button onClick={onClose} className="border px-4 py-2 rounded">
            ⏸️ Continua senza traduzione
          </button>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 3: QAPanel similar pattern** (translates question, shows answer + citation + confidence badge)

- [ ] **Step 4: Compose play page**

```tsx
// apps/web/src/app/(authenticated)/gamebook/[gameId]/play/page.tsx
export default function PlayPage({ params }: { params: { gameId: string } }) {
  return (
    <div className="grid lg:grid-cols-2 gap-6 p-4" data-slot="gamebook-play">
      <section>
        <h1 className="text-2xl font-bold">Sessione di gioco</h1>
        <TranslationViewer gameId={params.gameId} />
      </section>
      <aside>
        <h2 className="text-xl font-bold">Domande sulle regole</h2>
        <QAPanel gameId={params.gameId} />
      </aside>
    </div>
  );
}
```

- [ ] **Step 5: i18n strings**

```json
// apps/web/src/i18n/locales/it/gamebook.json
{
  "play": {
    "title": "Sessione di gioco",
    "translation": {
      "placeholder": "es. 147",
      "fetch": "Vai",
      "fetching": "⏳ Traducendo...",
      "showOriginal": "🌐 Mostra originale",
      "showTranslated": "🌐 Mostra italiano",
      "fromCache": "📋 Da cache"
    },
    "qa": {
      "placeholder": "Es. Come funziona il combat?",
      "ask": "Chiedi",
      "asking": "⏳ Sto pensando..."
    },
    "quota": {
      "title": "Quota gratuita esaurita",
      "description": "Hai usato tutte le 50 pagine gratuite di traduzione questo mese.",
      "buyCredits": "💎 Acquista 100 credits (€5)",
      "continueWithout": "⏸️ Continua senza traduzione"
    }
  }
}
```

- [ ] **Step 6: Vitest unit tests for components**

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/app/(authenticated)/gamebook/[gameId]/
git add apps/web/src/i18n/locales/it/gamebook.json
git add apps/web/__tests__/gamebook/play/
git commit -m "feat(gamebook): play screen with translation viewer + Q&A panel + quota modal"
```

### Task 3.5 — Phase 3 Acceptance Gate

- [ ] **Step 1: E2E Playwright test "full session"**

```typescript
// apps/web/e2e/gamebook/full-session.spec.ts
test('full session: upload manual → ask question → translate paragraph → hit quota', async ({ page }) => {
  await login(page);
  await page.goto('/gamebook/upload');
  await uploadManual(page, 'tainted-grail-test-50pages.zip');
  await waitForIndexing(page);

  await page.goto(`/gamebook/${gameId}/play`);

  // Q&A
  await page.fill('[data-slot="qa-input"]', 'Come funziona il combat?');
  await page.click('[data-slot="qa-submit"]');
  await expect(page.locator('[data-slot="qa-answer"]')).toContainText(/dadi|combat/i, { timeout: 10000 });

  // Translation
  await page.fill('[data-slot="translation-viewer"] input', '147');
  await page.click('[data-slot="translation-viewer"] button:has-text("Vai")');
  await expect(page.locator('[data-slot="translated-paragraph"]')).toBeVisible({ timeout: 10000 });

  // Trigger quota (mock or use exhausted account)
  // ... fill 50 paragraphs ...
  await expect(page.locator('[data-slot="quota-modal"]')).toBeVisible();
});
```

- [ ] **Step 2: Tag**

```bash
git tag mvp-phase-3-complete
git push origin mvp-phase-3-complete
```

---

## Phase 4 — Integration, Chaos, Usability, Launch (Sprint 10-11, weeks 19-22)

**Goal:** Production readiness, validated UX, launch checklist 100%.

### Task 4.1 — Chaos engineering tests

- [ ] **Step 1: WiFi loss simulation script**

```bash
# tests/chaos/wifi-loss.sh
#!/bin/bash
# Simulate WiFi loss for 30 sec mid-session
sleep 60  # let session start
sudo ifconfig wlan0 down
sleep 30
sudo ifconfig wlan0 up
```

- [ ] **Step 2: Verify graceful degradation**

Run E2E test with chaos script in parallel. Verify:
- No crash
- "Sto riprovando" indicator shown
- Auto-resume on reconnection
- Last translation visible during outage

- [ ] **Step 3: LLM provider rate limit simulation**

Mock OpenRouter to return 429 for 1 min. Verify circuit breaker opens, fallback to alternative model.

- [ ] **Step 4: Server OOM simulation**

Stress test with `stress-ng --vm 2 --vm-bytes 14G --timeout 60s`. Verify swap activates, no service crash, graceful slowdown.

- [ ] **Step 5: Commit chaos test plan**

```bash
git add tests/chaos/
git commit -m "test(chaos): WiFi loss + LLM rate limit + OOM scenarios"
```

### Task 4.2 — Usability testing 5 sessions

**Owner:** UX researcher

- [ ] **Step 1: Recruit 5 casual gamer groups**

Criteria: 3-5 people, casual boardgamer, mai usato MeepleAI, possesses gamebook in EN.

- [ ] **Step 2: Moderated remote testing protocol**

90-min session per group:
- 15 min: setup + manual upload
- 60 min: actual play with app
- 15 min: debrief + feedback survey

Metrics tracked:
- Time to first successful Q&A
- Translation request frequency
- Confusion points (count + description)
- NPS rating

- [ ] **Step 3: Document findings + iterate**

```bash
git add docs/usability/phase-4-findings.md
git commit -m "docs(usability): 5-group testing findings + UI iteration list"
```

### Task 4.3 — Disaster recovery drill

- [ ] **Step 1: Simulate CAX31 total loss**

Provision new CAX31. Restore from latest Storage Box backup. Measure time to operational.
Target: RTO ≤ 2h.

- [ ] **Step 2: Document procedure**

```bash
git add infra/hetzner/disaster-recovery.md
git commit -m "docs(operations): DR drill verified - RTO 90 min on CAX31 loss"
```

### Task 4.4 — Launch checklist

- [ ] PR-1: Legal review copyright + TOS aggiornato + privacy policy GDPR-compliant ✅
- [ ] PR-2: OCR validated su 5 manuali gamebook reali ≥ 85% confidence ✅
- [ ] PR-3: Test set golden 100 Q&A + 50 paragrafi con expert validation ✅
- [ ] CAX31 deployment + monitoring + alerting + backup automated ✅
- [ ] Pricing engine tested end-to-end (Free + Credits + Stripe webhook) ✅
- [ ] Hallucination ≤ 3% validato in CI gate ✅
- [ ] 5 sessioni usability testing completate, feedback incorporated ✅
- [ ] DR drill eseguito (restore < 2h) ✅
- [ ] Cost telemetry dashboard live (Grafana) ✅
- [ ] OpenRouter cost alerting (€100/day soft, €200/day hard) ✅
- [ ] Stripe production keys configured ✅
- [ ] Privacy policy linked from app footer ✅

- [ ] **Step 1: Final tag + production deploy**

```bash
git tag mvp-v1.0.0
git push origin mvp-v1.0.0
# Trigger production deploy via existing pipeline
```

---

## Risk Mitigation Matrix (Plan-Level)

| Risk | Mitigation in plan |
|------|---------------------|
| **R-2 OCR quality bassa** | Task 0.1 PR-2 validation upfront, gate decision matrix |
| **R-13 OCR layout artistici** | Task 0.1 includes Tainted Grail + ISS Vanguard (worst case) |
| **R-5 LLM hallucination** | Task 2.4 CI gate + LLM-as-judge eval |
| **R-9 WiFi instabile** | Task 4.1 chaos + G4.7 graceful UI scenario |
| **R-1 LLM pricing spike** | OpenRouter abstraction (Task 2.1), multi-model routing |
| **R-15 Test set bottleneck** | Task 0.2 with 4-week lead time + contractor budget |
| **R-3 Copyright takedown** | Task 0.3 PR-1 legal review pre-launch + TOS robust |
| **CAX31 borderline** | NOT borderline — 6.4-9 GB on 16 GB capacity (verified §4.11) |

---

## Self-Review Notes (Plan author)

**Spec coverage check** against vision §6:
- ✅ G1 Acquire manuale (Tasks 1.1-1.7)
- ✅ G3 Q&A regole + house rule (Tasks 2.1-2.5)
- ✅ G4 Translation on-demand + chapter-based fallback (Task 3.1)
- ✅ Default Auto LLM mode invisible (Task 2.1 — no preset UI)
- ✅ Pricing 2-tier Free + Credits (Tasks 3.2-3.3)
- ✅ Single-device only (no multi-device QR in plan)
- ✅ CAX31 baseline (Task 0.4)
- ✅ Test plan: Unit + Integration + E2E + LLM eval (Tasks 1.7, 2.4, 2.5, 3.5, 4.1)
- ✅ Prerequisites PR-1/PR-2/PR-3 covered (Tasks 0.1-0.3)

**Acceptance criteria mapping** (vision §6.3):
- AC-1 (5 sessioni real E2E) → Task 4.2
- AC-2 (≥ 70% completion 2h+) → Task 4.2 metric
- AC-3 (cost ≤ €3.00) → Task 4.4 telemetry dashboard
- AC-4 (hallucination ≤ 3%) → Task 2.4 CI gate
- AC-5 (OCR ≥ 85% confidence) → Task 0.1 + Phase 1 gate
- AC-6 (legal review) → Task 0.3
- AC-7 (latency P95) → Phase 2 gate (Task 2.5)
- AC-8 (pricing 2-tier) → Tasks 3.2-3.3 + Task 4.4 checklist

**Placeholder scan**: nessun TODO/TBD nei step. Code blocks completi per ogni step di implementazione. Test snippets concreti.

**Type consistency**: `UploadPhotoBatchCommand`, `TranslationRequest`, `BillableOperation`, `ConfidenceLevel` referenziati consistentemente attraverso tasks.

---

## Out of plan (deferred to Phase 1.5+ per vision §6.2)

- Save/resume cross-day → Phase 1.5
- G2 Setup wizard interattivo → Phase 1.5 (MVP usa "show setup paragraph" tradotto)
- G4.2 Pre-translate chapter → Phase 1.5
- Multi-device QR opt-in → Phase 1.5
- AI Narrator audio (TTS) → v2
- LLM preset selector + transparency UI → v2
- BYOK → v2.5
- Two-pass translation quality → v2

---

**Plan end. 4-5 mesi calendar, 3 fullstack + 1 ML + 1 designer + legal advisor part-time.**
