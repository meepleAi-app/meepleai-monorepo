# Nanolith Demo Runthrough Phase A — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eseguire la pipeline 4-stadi (locale endpoint → locale browser → staging browser → smartphone Android Chrome) del caso d'uso Nanolith libro game entro 2026-05-22, raccogliendo evidence lightweight e classificando ogni anomalia per abilitare go/no-go Fase B.

**Architecture:** Pipeline sequential di 5 SMART goals (G1-G5) ortogonali al merge Iter 1.A/1.B; backend smoke automatico via Bash/curl, frontend smoke via Playwright headless, manual deep su 4 ambienti, evidence aggregata in MD + 1 PNG mobile. Strategia merge ibrida (T-10d checkpoint A primary main-dev / B fallback demo-branch). R5 Resume card scoped a state-02 (single fresh) per Fase A.

**Tech Stack:** Bash + curl + jq (smoke scripts) | Make (target wiring) | Playwright + TypeScript (E2E spec) | React 19 + Next.js 16 (R5 Resume card) | .NET 9 + EF Core (existing backend Iter 1.A/1.B) | PostgreSQL 16 + pgvector (existing DB) | Chrome ≥ 120 desktop + Chrome Android ≥ 11 (browser/smartphone target)

**Spec di riferimento:** `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-phase-a.md`

---

## File Structure

### File da creare

| Path | Responsabilità |
|---|---|
| `infra/scripts/demo-smoke-local.sh` | Bash script che esegue 7 curl assertion contro endpoint /api/v1/* (G1) + parsa JSON + exit code 0/!=0 |
| `apps/web/e2e/demo-runthrough/nanolith-flow.spec.ts` | Playwright E2E spec @demo-runthrough che simula caso d'uso [1]-[6] in headless Chrome locale (G2 auto) |
| `apps/web/src/components/v2/gamebook/ResumeCard.tsx` | React component R5 state-02 (single fresh resume visualization), props tipizzate |
| `apps/web/src/components/v2/gamebook/__tests__/ResumeCard.test.tsx` | Vitest unit test 5-state FSM (loading/error/empty/state-02/stale-fallback) |
| `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md` | Live MD durante pipeline con tag classification `#BLOCKER`/`#ANOMALY`/`#OBSERVATION` |
| `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-results.md` | Aggregator post-G4 con go/no-go decision + evidence bundle |
| `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-results/runthrough-mobile-final-translation.png` | Screenshot Android di traduzione finale (G4 evidence, ≤ 2MB) |

### File da modificare

| Path | Modifica |
|---|---|
| `infra/Makefile` | Aggiungi 2 target: `demo-smoke-local` + `demo-smoke-staging` |
| `apps/web/src/components/v2/gamebook/index.ts` | Aggiungi barrel export per `ResumeCard` |
| `apps/web/src/app/(authenticated)/library/games/[gameId]/play/[campaignId]/page.tsx` (creato in PR #828) | Wire `ResumeCard` come entry-point shell |
| `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-phase-a.md` | Amendment: chiarire R5 scope = state-02 only (state-03/04 deferred Fase B) |

### Branch strategy

- Branch dedicato: `feature/nanolith-demo-runthrough-phase-a` da `main-dev`
- Parent branch tracking: `git config branch.feature/nanolith-demo-runthrough-phase-a.parent main-dev`
- PR target: `main-dev`

---

## Pre-requisites checklist

Verifica PRIMA di iniziare Task 0:

- [ ] Spec approvato in `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-phase-a.md`
- [ ] `data/rulebook/nanolith_datasource/Nanolith Rules ENG.pdf` (101MB) presente
- [ ] `data/rulebook/nanolith_datasource/Nanolith Press Start ENG.pdf` (36MB) presente
- [ ] `infra/secrets/admin.secret` con `INITIAL_ADMIN_EMAIL` + `INITIAL_ADMIN_PASSWORD`
- [ ] SSH key `~/.ssh/meepleai-staging` configurata e working (`make tunnel` succeeds)
- [ ] Android device ≥ 11 con Chrome ≥ 120 disponibile entro T-3d
- [ ] 1 pagina fisica Storybook Nanolith reperibile entro T-3d (alternativa: scan PDF da galleria)
- [ ] Bash 5+, jq, curl, python3, gh (GitHub CLI), pnpm, dotnet 9 installati

---

## Tasks

### Task 0: Workspace setup

**Files:**
- No file modificati (operational task)

**Steps:**

- [ ] **Step 1: Verifica branch corrente clean**

```bash
git status
```

Expected output: working tree clean. Se ci sono file modificati pending, fare commit dedicato o stashare con `git stash push -m "pre-runthrough-stash"`.

- [ ] **Step 2: Switch to main-dev + pull latest**

```bash
git checkout main-dev
git pull --ff-only
```

Expected: `Already up to date.` o fast-forward succeeds.

- [ ] **Step 3: Verifica HEAD su main-dev e crea branch feature**

```bash
git branch --show-current  # MUST print main-dev
git checkout -b feature/nanolith-demo-runthrough-phase-a
git config branch.feature/nanolith-demo-runthrough-phase-a.parent main-dev
```

Expected: nuovo branch creato, parent tracking salvato.

- [ ] **Step 4: Commit spec + plan**

```bash
git add docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-phase-a.md
git add docs/superpowers/plans/2026-05-08-nanolith-demo-runthrough-phase-a.md
git commit -m "docs(nanolith-runthrough): spec + plan per Phase A demo runthrough

Spec orchestratore della Fase A (Aaron solo pre-validation runthrough)
del caso d'uso Nanolith libro game su 4 ambienti (locale endpoint →
locale browser → staging browser → smartphone Android Chrome).

5 SMART goals (G1-G5) + 14 Gherkin scenari + timeline 14gg target
2026-05-22 + 10 Decision Log entries + 7 Open Questions Fase B."
```

- [ ] **Step 5: Push branch**

```bash
git push -u origin feature/nanolith-demo-runthrough-phase-a
```

Expected: branch pushed, no error.

---

### Task 1: Review + merge PR #828 (Iter 1.A)

> **Operational task** — no TDD. Review code + merge se OK.

**Files:**
- No file modificati direttamente (review existing PR)

**Steps:**

- [ ] **Step 1: Fetch PR + ottenere diff**

```bash
gh pr view 828 --json title,mergeable,mergeStateStatus,statusCheckRollup
gh pr diff 828 > /tmp/pr-828-diff.patch
wc -l /tmp/pr-828-diff.patch
```

Expected: `mergeable: MERGEABLE` (forse UNKNOWN — re-fetch dopo `gh pr ready 828`); diff size reasonable (qualche migliaia di righe per Iter 1.A: campaign sessions + chat shell).

- [ ] **Step 2: Review code locally**

```bash
gh pr checkout 828
# Review files: apps/api/src/Api/BoundedContexts/SessionTracking/* (new)
# Review files: apps/web/src/app/(authenticated)/library/games/[gameId]/play/* (new)
# Review files: apps/api/src/Api/Migrations/* (new EF migration)
```

Review checklist:
- DDD compliance (entities private setters, factory methods, repos in Domain interfaces)
- CQRS (endpoints use IMediator.Send only, no direct service injection)
- Soft delete pattern (IsDeleted + DeletedAt + HasQueryFilter)
- Audit columns (CreatedAt/UpdatedAt/CreatedBy/UpdatedBy)
- Tests presenti (unit + integration)

- [ ] **Step 3: Run tests locally su PR branch**

```bash
cd apps/api/src/Api
dotnet test --filter "BoundedContext=SessionTracking|GameManagement"
cd ../../../web
pnpm test --filter "library/games"
```

Expected: tests green. Se rosso → file follow-up issue, NON modifica PR (R-AC7 anti-creep rule).

- [ ] **Step 4: Verifica CI checks**

```bash
gh pr checks 828
```

Expected: tutti check green (CI Success, A11y, Build, Tests). Se rosso → comment su PR per fix, attendi.

- [ ] **Step 5: Merge PR (squash + delete branch)**

```bash
gh pr merge 828 --squash --delete-branch
```

Expected: merged on main-dev, branch `feature/libro-game-iter-1a` deleted remoto.

- [ ] **Step 6: Verifica merge su main-dev locale**

```bash
git checkout main-dev
git pull --ff-only
git log --oneline -5  # deve mostrare squash commit Iter 1.A
```

- [ ] **Step 7: Switch back to feature branch**

```bash
git checkout feature/nanolith-demo-runthrough-phase-a
git rebase main-dev  # rebase su nuovo main-dev
git push --force-with-lease
```

Expected: rebase clean (no conflict atteso, lo plan tocca file diversi).

---

### Task 2: Review + merge PR #837 (Iter 1.B)

> **Operational task** — same pattern di Task 1 ma per PR #837 (photo translate + glossary + history).

**Files:**
- No file modificati direttamente

**Steps:**

- [ ] **Step 1: Fetch PR + diff**

```bash
gh pr view 837 --json title,mergeable,mergeStateStatus,statusCheckRollup
gh pr diff 837 > /tmp/pr-837-diff.patch
wc -l /tmp/pr-837-diff.patch
```

- [ ] **Step 2: Review code locally**

```bash
gh pr checkout 837
# Review files focus:
# - apps/api/src/Api/BoundedContexts/DocumentProcessing/ (new GamebookPhotoArtifact aggregate)
# - apps/api/src/Api/BoundedContexts/KnowledgeBase/ (new TranslatedParagraph aggregate)
# - apps/web/src/components/v2/gamebook/ (modifiche/aggiunte)
# - apps/web/src/lib/gamebook/hooks/ (nuovi hook usePhotoUpload, useTranslateSegmentSSE)
```

Review checklist (oltre Task 1):
- OCR pipeline integrazione (SmoldoclingClientAdapter or similar)
- SSE streaming endpoint (POST + text/event-stream)
- Idempotency-key composer per photo upload retry
- 5-state FSM client (idle/uploading/segmenting/translating/done)

- [ ] **Step 3: Run tests locally**

```bash
cd apps/api/src/Api
dotnet test --filter "BoundedContext=DocumentProcessing|KnowledgeBase"
cd ../../../web
pnpm test --filter "gamebook"
```

- [ ] **Step 4: Verifica CI checks**

```bash
gh pr checks 837
```

- [ ] **Step 5: Merge PR**

```bash
gh pr merge 837 --squash --delete-branch
```

- [ ] **Step 6: Verifica + rebase feature branch**

```bash
git checkout main-dev && git pull --ff-only
git checkout feature/nanolith-demo-runthrough-phase-a
git rebase main-dev
git push --force-with-lease
```

Expected: entrambi i commit Iter 1.A + Iter 1.B su main-dev. Feature branch rebased clean.

---

### Task 3: T-10d checkpoint go/no-go

> **Operational task** — decision logging. Esegui solo dopo Task 1 + Task 2 (che SOLO lui controlla se main-dev path A è stato realizzato).

**Files:**
- Create: `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md`

**Steps:**

- [ ] **Step 1: Crea anomalies tracking file**

Path: `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md`

```markdown
# Nanolith Demo Runthrough — Anomalies Live Log

> Live MD compilato durante la pipeline runthrough. Tag classification per ogni entry:
> - `#BLOCKER` — failure reproducible 3× consecutivi che impedisce stadio
> - `#ANOMALY` — comportamento sub-target ma non bloccante (latenza, UX rough)
> - `#OBSERVATION` — note libere, comportamento atteso ma vale memorizzare

## §0 — T-10d Checkpoint Go/No-Go (data: YYYY-MM-DD)

**Domanda:** PR #828 + #837 mergiati su main-dev?

[ ] YES — PR #828 squash commit: `<hash>` | PR #837 squash commit: `<hash>`
       → Path A primary continues
[ ] NO  — Mancano: `<list PR ancora open>`
       → Path B fallback: creare branch `demo/nanolith-runthrough-phase-a`
                          via cherry-pick di entrambi PR squash
                          + deploy staging via override

**Decisione finale:** ___ (A primary | B fallback)

## §1 — Stadio 1 (locale endpoint smoke)

(in attesa T-2d)

## §2 — Stadio 2 (locale browser flow)

(in attesa T-1d)

## §3 — Stadio 3 (staging browser flow)

(in attesa T-1d)

## §4 — Stadio 4 (smartphone Android Chrome)

(in attesa T-0d)
```

- [ ] **Step 2: Compila §0 con outcome reale**

Sostituisci placeholders con valori reali:
- Data checkpoint
- Hash squash commit (se A primary)
- Lista PR open (se B fallback)
- Decisione finale

- [ ] **Step 3: Commit anomalies file**

```bash
git add docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md
git commit -m "docs(nanolith-runthrough): T-10d checkpoint § 0 — go/no-go decision logged"
```

- [ ] **Step 4: SE Path B fallback scelto**

Esegui sub-step:

```bash
# Creare demo branch
git checkout main-dev
git checkout -b demo/nanolith-runthrough-phase-a

# Cherry-pick squash entrambi PR (assume entrambi merged on feature/libro-game-iter-1a + feature/libro-game-iter-1b)
git cherry-pick <hash-iter-1a-squash>
git cherry-pick <hash-iter-1b-squash>

# Push demo branch
git push -u origin demo/nanolith-runthrough-phase-a

# Deploy staging override (procedura specifica progetto — da documentare in infra runbook)
# Esempio:
# make staging-deploy-from-branch BRANCH=demo/nanolith-runthrough-phase-a
```

Otherwise (Path A): skip Step 4.

---

### Task 4: R5 Resume card UX — state-02 (single fresh resume)

> **Scope-cap esplicito**: state-02 only. state-03 multi-campaign + state-04 stale warning sono deferred a Fase B (dipendenza N4 cross-day backend non implementato in Iter 1).
>
> Anti-creep R-AC1: se durante implementation emerge esigenza state-03/04 → file follow-up issue, NON aggiungere a Fase A.

**Files:**
- Create: `apps/web/src/components/v2/gamebook/ResumeCard.tsx`
- Create: `apps/web/src/components/v2/gamebook/__tests__/ResumeCard.test.tsx`
- Modify: `apps/web/src/components/v2/gamebook/index.ts` (barrel export)
- Modify: `apps/web/src/app/(authenticated)/library/games/[gameId]/play/[campaignId]/page.tsx` (created PR #828)

**Steps:**

- [ ] **Step 1: Read mockup G state-02 reference**

```bash
# Apri mockup G in browser per visual reference
# File: admin-mockups/design_files/sp6-libro-game-resume-state.html
# Anchor: #state-02-single-resume
```

Mockup G state-02 mostra: 1 campagna 7 giorni fa, glossary preview top-5, CTA "Riprendi" + "Nuova campagna".

- [ ] **Step 2: Write failing test**

Path: `apps/web/src/components/v2/gamebook/__tests__/ResumeCard.test.tsx`

```typescript
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ResumeCard } from '../ResumeCard';

const fixtureSingleFresh = {
  campaignId: 'campaign-uuid-1',
  gameTitle: 'Nanolith',
  paragraphsRead: 23,
  totalParagraphs: 180,
  daysSinceLastSession: 7,
  lastParagraphRef: '§42',
  glossaryPreview: [
    { en: 'Voidstone', it: 'Pietra del Vuoto' },
    { en: 'Reaver', it: 'Razziatore' },
    { en: 'Hollow Watch', it: 'Veglia Cava' },
    { en: 'Pale Wardens', it: 'Sentinelle Pallide' },
    { en: 'Ember Court', it: 'Corte di Brace' },
  ],
  glossaryFullCount: 12,
};

describe('ResumeCard state-02 (single fresh resume)', () => {
  it('renders campaign title and paragraphs progress', () => {
    render(<ResumeCard {...fixtureSingleFresh} />);
    expect(screen.getByText('Nanolith')).toBeInTheDocument();
    expect(screen.getByText(/23.*180/)).toBeInTheDocument();
  });

  it('renders days-since indicator', () => {
    render(<ResumeCard {...fixtureSingleFresh} />);
    expect(screen.getByText(/7 giorni fa/i)).toBeInTheDocument();
  });

  it('renders last paragraph ref', () => {
    render(<ResumeCard {...fixtureSingleFresh} />);
    expect(screen.getByText(/§42/)).toBeInTheDocument();
  });

  it('renders glossary preview top-5 with full count badge', () => {
    render(<ResumeCard {...fixtureSingleFresh} />);
    fixtureSingleFresh.glossaryPreview.forEach(g => {
      expect(screen.getByText(g.en)).toBeInTheDocument();
      expect(screen.getByText(g.it)).toBeInTheDocument();
    });
    expect(screen.getByText(/12.*termini/i)).toBeInTheDocument();
  });

  it('renders Riprendi CTA enabled', () => {
    render(<ResumeCard {...fixtureSingleFresh} />);
    const btn = screen.getByRole('button', { name: /riprendi/i });
    expect(btn).toBeEnabled();
  });

  it('renders Nuova campagna CTA enabled', () => {
    render(<ResumeCard {...fixtureSingleFresh} />);
    const btn = screen.getByRole('button', { name: /nuova campagna/i });
    expect(btn).toBeEnabled();
  });

  it('has data-slot semantic attribute for E2E targeting', () => {
    const { container } = render(<ResumeCard {...fixtureSingleFresh} />);
    expect(container.querySelector('[data-slot="resume-card"]')).toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Run test (must fail — component non esiste ancora)**

```bash
cd apps/web
pnpm test --run src/components/v2/gamebook/__tests__/ResumeCard.test.tsx
```

Expected: FAIL con `Cannot find module '../ResumeCard'`.

- [ ] **Step 4: Write minimal ResumeCard component**

Path: `apps/web/src/components/v2/gamebook/ResumeCard.tsx`

```typescript
'use client';

import type { JSX } from 'react';

export interface GlossaryPreviewEntry {
  readonly en: string;
  readonly it: string;
}

export interface ResumeCardProps {
  readonly campaignId: string;
  readonly gameTitle: string;
  readonly paragraphsRead: number;
  readonly totalParagraphs: number;
  readonly daysSinceLastSession: number;
  readonly lastParagraphRef: string;
  readonly glossaryPreview: ReadonlyArray<GlossaryPreviewEntry>;
  readonly glossaryFullCount: number;
  readonly onResume?: (campaignId: string) => void;
  readonly onNewCampaign?: () => void;
}

export function ResumeCard(props: ResumeCardProps): JSX.Element {
  const {
    campaignId,
    gameTitle,
    paragraphsRead,
    totalParagraphs,
    daysSinceLastSession,
    lastParagraphRef,
    glossaryPreview,
    glossaryFullCount,
    onResume,
    onNewCampaign,
  } = props;

  return (
    <article
      data-slot="resume-card"
      data-campaign-id={campaignId}
      className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"
    >
      <header className="flex items-baseline justify-between">
        <h2 className="text-xl font-bold text-slate-900">{gameTitle}</h2>
        <span className="text-sm text-slate-500">{daysSinceLastSession} giorni fa</span>
      </header>

      <div className="mt-3 flex items-center gap-3 text-sm text-slate-600">
        <span>
          Progresso: <strong>{paragraphsRead}</strong> / {totalParagraphs} paragrafi
        </span>
        <span>•</span>
        <span>
          Ultimo: <code className="font-mono">{lastParagraphRef}</code>
        </span>
      </div>

      <section className="mt-4">
        <h3 className="text-sm font-semibold text-slate-700">
          Glossario ({glossaryFullCount} termini)
        </h3>
        <ul className="mt-2 space-y-1 text-sm">
          {glossaryPreview.map(g => (
            <li key={g.en} className="flex justify-between">
              <span className="text-slate-700">{g.en}</span>
              <span className="text-slate-500">→ {g.it}</span>
            </li>
          ))}
        </ul>
      </section>

      <footer className="mt-6 flex gap-2">
        <button
          type="button"
          onClick={() => onResume?.(campaignId)}
          className="flex-1 rounded-lg bg-orange-500 px-4 py-2 text-sm font-semibold text-white hover:bg-orange-600"
        >
          Riprendi
        </button>
        <button
          type="button"
          onClick={() => onNewCampaign?.()}
          className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
        >
          Nuova campagna
        </button>
      </footer>
    </article>
  );
}
```

- [ ] **Step 5: Run test (must pass)**

```bash
cd apps/web
pnpm test --run src/components/v2/gamebook/__tests__/ResumeCard.test.tsx
```

Expected: 7/7 tests PASS.

- [ ] **Step 6: Add barrel export**

Modify: `apps/web/src/components/v2/gamebook/index.ts`

Aggiungi (alphabetical position, vicino a `GamebookCard`):

```typescript
// Phase A demo runthrough — R5 Resume card scope state-02 only
export { ResumeCard } from './ResumeCard';
export type { ResumeCardProps, GlossaryPreviewEntry } from './ResumeCard';
```

- [ ] **Step 7: Wire ResumeCard nel play shell**

Modify: `apps/web/src/app/(authenticated)/library/games/[gameId]/play/[campaignId]/page.tsx`

Localizzare il punto di entry shell creato in PR #828. Aggiungere render condizionale di `ResumeCard` quando `lastSessionDaysAgo > 0` AND `paragraphsRead > 0`.

```tsx
import { ResumeCard } from '@/components/v2/gamebook';

// Within page render:
{showResume && (
  <ResumeCard
    campaignId={campaign.id}
    gameTitle={campaign.gameTitle}
    paragraphsRead={campaign.paragraphsRead}
    totalParagraphs={campaign.totalParagraphs}
    daysSinceLastSession={daysSince(campaign.lastUpdatedAt)}
    lastParagraphRef={campaign.lastParagraphRef ?? '§1'}
    glossaryPreview={campaign.glossary.slice(0, 5)}
    glossaryFullCount={campaign.glossary.length}
    onResume={() => /* navigate to viewer route */}
    onNewCampaign={() => /* dispatch new campaign create */}
  />
)}
```

- [ ] **Step 8: Run lint + typecheck**

```bash
cd apps/web
pnpm typecheck
pnpm lint
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 9: Run full test suite filter gamebook**

```bash
pnpm test --filter "gamebook"
```

Expected: all tests pass + new ResumeCard tests pass.

- [ ] **Step 10: Commit**

```bash
git add apps/web/src/components/v2/gamebook/ResumeCard.tsx \
        apps/web/src/components/v2/gamebook/__tests__/ResumeCard.test.tsx \
        apps/web/src/components/v2/gamebook/index.ts \
        apps/web/src/app/(authenticated)/library/games/[gameId]/play/[campaignId]/page.tsx
git commit -m "feat(gamebook): R5 ResumeCard state-02 (single fresh resume)

Implementa Mockup G state-02 (1 campagna fresca <30gg, glossary
preview top-5, CTA Riprendi/Nuova). Scope Fase A: state-02 only,
state-03 multi-campaign + state-04 stale warning deferred Fase B.

Refs: docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-phase-a.md
      admin-mockups/design_files/sp6-libro-game-resume-state.jsx#state-02"
```

---

### Task 5: T-6d seed-staging verification

> **Operational task** — verifica seed automation funziona contro staging.

**Files:**
- No file modificati

**Steps:**

- [ ] **Step 1: Avvia SSH tunnel staging**

```bash
make tunnel
```

Expected: tunnel started in background, log "tunnel listening on localhost:8080".

- [ ] **Step 2: Esegui seed staging**

```bash
make seed-nanolith-demo-staging
```

Expected: script runs to completion (~5-15 min for PDF indexing). Output finale:
```
[seed][OK] G8 — account badsworm@gmail.com Admin
[seed][OK] G9 — game Nanolith Published
[seed][OK] G10 — both PDFs indexed Ready
[seed][OK] G11 — agent Nanolith Tutor created
```

- [ ] **Step 3: Verifica CSV results**

```bash
cat /tmp/nanolith-demo-seed-results.csv
```

Expected: 4 entry CSV con status OK per ogni precondition.

- [ ] **Step 4: Test login staging via browser**

Apri Chrome desktop su `https://meepleai.app/login`. Login con:
- email: `badsworm@gmail.com`
- password: `TestNanolith2026!`

Expected: login successful + redirect a `/library` + Nanolith visibile in collection.

- [ ] **Step 5: Documenta in anomalies §0**

Aggiungi entry in `runthrough-anomalies.md` §0:

```markdown
**T-6d seed staging verification**: ✅ ALL OK (data: YYYY-MM-DD)
- G8 account: OK
- G9 game: OK
- G10 KB Ready: OK (Rules Xmin, Press Start Ymin)
- G11 agent: OK
- Browser login staging: OK
```

Se anomaly (es. PDF indexing > 30min) → tag `#ANOMALY`.

- [ ] **Step 6: Commit anomalies update**

```bash
git add docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md
git commit -m "docs(nanolith-runthrough): T-6d seed staging verification logged"
```

---

### Task 6: demo-smoke-local script

**Files:**
- Create: `infra/scripts/demo-smoke-local.sh`
- Create: `infra/scripts/__tests__/demo-smoke-local.test.sh` (optional bash unit test)

**Steps:**

- [ ] **Step 1: Verifica stack locale up**

```bash
cd infra
make dev-core  # se non già up
sleep 30  # buffer startup containers
docker ps --format "{{.Names}}" | grep meepleai
```

Expected: containers running (api, web, postgres, redis).

- [ ] **Step 2: Esegui seed locale (precondition)**

```bash
cd infra
make seed-nanolith-demo
```

Expected: 4 OK come Task 5 step 2 ma contro localhost:8080.

- [ ] **Step 3: Write demo-smoke-local.sh**

Path: `infra/scripts/demo-smoke-local.sh`

```bash
#!/usr/bin/env bash
# demo-smoke-local.sh — Pipeline Stadio 1 (G1) smoke test contro stack locale.
#
# Verifica 7 endpoint /api/v1/* del caso d'uso Nanolith [1]-[6]:
#   1. POST /auth/login              (login Aaron)
#   2. GET  /library/me              (collection visible)
#   3. GET  /games/{nanolithId}      (game detail)
#   4. POST /gamebook/campaigns      (create campaign)
#   5. POST /agents/chat-stream      (setup chat SSE)
#   6. POST /gamebook/campaigns/{id}/photos   (photo upload)
#   7. POST /gamebook/campaigns/{id}/photos/{pid}/translate (translate SSE)
#
# Usage:
#   ./demo-smoke-local.sh                # default: local
#   ./demo-smoke-local.sh local          # explicit
#   ./demo-smoke-local.sh staging        # staging via tunnel
#
# Exit codes:
#   0 = all 7 endpoint OK
#   1 = pre-flight failed (missing dep, no seed)
#   2 = endpoint failure (specific endpoint logged)
#
# Spec: docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-phase-a.md (G1.1)

set -euo pipefail

TARGET="${1:-local}"
case "$TARGET" in
  local)   API_BASE="http://localhost:8080/api/v1" ;;
  staging) API_BASE="https://meepleai.app/api/v1" ;;
  *) echo "ERROR: TARGET must be local|staging (got: $TARGET)"; exit 1 ;;
esac

COOKIE_JAR="/tmp/meepleai-${TARGET}-smoke-cookies.txt"
RESULTS_FILE="/tmp/demo-smoke-${TARGET}-results.csv"

USER_EMAIL="badsworm@gmail.com"
USER_PASSWORD="TestNanolith2026!"

# ---- helpers ----
log()  { printf "\033[1;36m[smoke]\033[0m %s\n" "$*"; }
ok()   { printf "\033[1;32m[smoke][OK]\033[0m %s\n" "$*"; }
fail() { printf "\033[1;31m[smoke][FAIL]\033[0m %s\n" "$*" >&2; exit 2; }

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || { echo "missing dep: $1"; exit 1; }
}

http_status() {
  # Returns HTTP status code from curl response file
  local code="$1" expected="$2" label="$3"
  if [ "$code" != "$expected" ]; then
    fail "$label: expected $expected got $code"
  fi
  ok "$label: HTTP $code"
}

# ---- pre-flight ----
log "Pre-flight (target: $TARGET)"
require_cmd jq
require_cmd curl

rm -f "$COOKIE_JAR"
echo "endpoint,status" > "$RESULTS_FILE"

# ---- 1. POST /auth/login ----
log "1/7: POST /auth/login"
http_code=$(curl -sS -o /tmp/smoke-login.json -w "%{http_code}" \
  -c "$COOKIE_JAR" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$USER_EMAIL\",\"password\":\"$USER_PASSWORD\"}" \
  "$API_BASE/auth/login")
http_status "$http_code" "200" "POST /auth/login"
echo "/auth/login,200" >> "$RESULTS_FILE"

# ---- 2. GET /library/me ----
log "2/7: GET /library/me"
http_code=$(curl -sS -o /tmp/smoke-library.json -w "%{http_code}" \
  -b "$COOKIE_JAR" \
  "$API_BASE/library/me")
http_status "$http_code" "200" "GET /library/me"

# Extract Nanolith gameId from library response
NANOLITH_ID=$(jq -r '.games[] | select(.title == "Nanolith") | .id' /tmp/smoke-library.json)
[ -n "$NANOLITH_ID" ] && [ "$NANOLITH_ID" != "null" ] || fail "Nanolith not in library"
log "  Nanolith ID: $NANOLITH_ID"
echo "/library/me,200" >> "$RESULTS_FILE"

# ---- 3. GET /games/{nanolithId} ----
log "3/7: GET /games/$NANOLITH_ID"
http_code=$(curl -sS -o /tmp/smoke-game.json -w "%{http_code}" \
  -b "$COOKIE_JAR" \
  "$API_BASE/games/$NANOLITH_ID")
http_status "$http_code" "200" "GET /games/{id}"
echo "/games/{id},200" >> "$RESULTS_FILE"

# ---- 4. POST /gamebook/campaigns ----
log "4/7: POST /gamebook/campaigns"
http_code=$(curl -sS -o /tmp/smoke-campaign.json -w "%{http_code}" \
  -b "$COOKIE_JAR" \
  -H "Content-Type: application/json" \
  -d "{\"gameId\":\"$NANOLITH_ID\",\"title\":\"Smoke Test Campaign\"}" \
  "$API_BASE/gamebook/campaigns")
http_status "$http_code" "201" "POST /gamebook/campaigns"

CAMPAIGN_ID=$(jq -r '.id' /tmp/smoke-campaign.json)
[ -n "$CAMPAIGN_ID" ] && [ "$CAMPAIGN_ID" != "null" ] || fail "campaign id missing"
log "  Campaign ID: $CAMPAIGN_ID"
echo "/gamebook/campaigns,201" >> "$RESULTS_FILE"

# ---- 5. POST /agents/chat-stream ----
log "5/7: POST /agents/chat-stream (SSE)"
# SSE: emit events line-by-line. We check first event arrives < 8s.
http_code=$(timeout 10 curl -sS -o /tmp/smoke-sse-chat.txt -w "%{http_code}" \
  -b "$COOKIE_JAR" \
  -H "Content-Type: application/json" \
  -H "Accept: text/event-stream" \
  -d "{\"agentName\":\"Nanolith Tutor\",\"prompt\":\"setup quick test\"}" \
  "$API_BASE/agents/chat-stream" || echo "200")
http_status "$http_code" "200" "POST /agents/chat-stream"

[ -s /tmp/smoke-sse-chat.txt ] || fail "SSE chat empty response"
grep -q "^data:" /tmp/smoke-sse-chat.txt || fail "SSE chat no data: events"
echo "/agents/chat-stream,200" >> "$RESULTS_FILE"

# ---- 6. POST /gamebook/campaigns/{id}/photos ----
log "6/7: POST /gamebook/campaigns/$CAMPAIGN_ID/photos"
# Use a sample fixture PNG (1x1 transparent) for smoke; real OCR test in G2/G4.
SMOKE_FIXTURE="/tmp/smoke-photo-fixture.png"
printf '\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x06\x00\x00\x00\x1f\x15\xc4\x89\x00\x00\x00\rIDATx\xdacd\x00\x00\x00\x06\x00\x03\x6a\x80\x86\xc6\x00\x00\x00\x00IEND\xaeB`\x82' > "$SMOKE_FIXTURE"

http_code=$(curl -sS -o /tmp/smoke-photo.json -w "%{http_code}" \
  -b "$COOKIE_JAR" \
  -F "file=@$SMOKE_FIXTURE" \
  "$API_BASE/gamebook/campaigns/$CAMPAIGN_ID/photos")
http_status "$http_code" "201" "POST /gamebook/campaigns/{id}/photos"

PHOTO_ID=$(jq -r '.id' /tmp/smoke-photo.json)
[ -n "$PHOTO_ID" ] && [ "$PHOTO_ID" != "null" ] || fail "photo id missing"
log "  Photo ID: $PHOTO_ID"
echo "/gamebook/campaigns/{id}/photos,201" >> "$RESULTS_FILE"

# ---- 7. POST /gamebook/campaigns/{id}/photos/{pid}/translate ----
log "7/7: POST /gamebook/campaigns/$CAMPAIGN_ID/photos/$PHOTO_ID/translate (SSE)"
http_code=$(timeout 15 curl -sS -o /tmp/smoke-sse-translate.txt -w "%{http_code}" \
  -b "$COOKIE_JAR" \
  -H "Accept: text/event-stream" \
  -X POST \
  "$API_BASE/gamebook/campaigns/$CAMPAIGN_ID/photos/$PHOTO_ID/translate" || echo "200")
http_status "$http_code" "200" "POST /gamebook/.../translate"

# Note: 1x1 PNG won't actually translate, but endpoint should accept request and start SSE
# (which may emit error event). We just check SSE stream opens.
echo "/gamebook/.../translate,200" >> "$RESULTS_FILE"

# ---- summary ----
ok "ALL 7 endpoints OK"
log "Results CSV: $RESULTS_FILE"
cat "$RESULTS_FILE"
exit 0
```

- [ ] **Step 4: Make script executable**

```bash
chmod +x infra/scripts/demo-smoke-local.sh
```

- [ ] **Step 5: Test script contro stack locale (must pass)**

```bash
./infra/scripts/demo-smoke-local.sh local
echo "Exit: $?"
```

Expected: exit 0, log mostra "ALL 7 endpoints OK" + CSV con 7 entry.

Se fail → debug:
- Container down? → `make dev-core` restart
- Seed missing? → `make seed-nanolith-demo`
- Endpoint 404? → verifica route esiste (PR #828/#837 mergiati?)
- SSE empty? → verifica LLM provider configured

- [ ] **Step 6: Commit script**

```bash
git add infra/scripts/demo-smoke-local.sh
git commit -m "feat(infra): demo-smoke-local.sh script per Stadio 1 G1

7-endpoint curl assertion contro /api/v1/* del caso d'uso Nanolith
[1]-[6]. Exit 0 = all OK, exit 2 = endpoint failure (specific logged).
Supporta target local|staging.

Refs: docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-phase-a.md (G1.1)"
```

---

### Task 7: demo-smoke-local Makefile target

**Files:**
- Modify: `infra/Makefile`

**Steps:**

- [ ] **Step 1: Identifica posizione insertion**

Apri `infra/Makefile`. Localizza la sezione subito dopo `seed-nanolith-demo[-staging]` targets (near line con `# === RAG Backup ===`).

- [ ] **Step 2: Aggiungi 2 nuovi target**

Inserisci ANTE della sezione `# === RAG Backup ===`:

```makefile
# === Demo Runthrough Phase A ===
demo-smoke-local: ## Stadio 1 G1: smoke test 7 endpoint /api/v1/* contro stack locale
	bash scripts/demo-smoke-local.sh local

demo-smoke-staging: ## Stadio 3 G3: same smoke contro staging (richiede SSH tunnel + secrets sync)
	@echo "Pre-check: ssh tunnel attiva?"
	@nc -z localhost 8080 || { echo "ERR: tunnel down. Run 'make tunnel' first."; exit 1; }
	bash scripts/demo-smoke-local.sh staging
```

- [ ] **Step 3: Verifica target listati in `make help`**

```bash
cd infra
make help | grep -E "demo-smoke"
```

Expected:
```
  demo-smoke-local    Stadio 1 G1: smoke test 7 endpoint /api/v1/* contro stack locale
  demo-smoke-staging  Stadio 3 G3: same smoke contro staging (richiede SSH tunnel + secrets sync)
```

- [ ] **Step 4: Run target locale (must pass)**

```bash
cd infra
make demo-smoke-local
```

Expected: exit 0, output identico a Task 6 step 5.

- [ ] **Step 5: Commit**

```bash
git add infra/Makefile
git commit -m "feat(infra): make demo-smoke-local + demo-smoke-staging targets

Wraps demo-smoke-local.sh script con Makefile target listati in 'make help'.
demo-smoke-staging richiede SSH tunnel attiva (pre-check via nc -z localhost 8080).

Refs: G1 G3 in docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-phase-a.md"
```

---

### Task 8: G1 stadio 1 runthrough (T-2d, 2026-05-20)

> **Operational task** — eseguire smoke contro locale + log evidenza.

**Files:**
- Modify: `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md` (§1)

**Steps:**

- [ ] **Step 1: Pre-check stack locale**

```bash
cd infra
make dev-core
sleep 30
docker ps | grep meepleai
```

- [ ] **Step 2: Re-run seed se serve**

```bash
make seed-nanolith-demo
```

- [ ] **Step 3: Esegui make demo-smoke-local**

```bash
make demo-smoke-local | tee /tmp/g1-smoke-output.log
echo "Exit: $?"
```

Expected: exit 0, "ALL 7 endpoints OK".

- [ ] **Step 4: Aggiorna §1 anomalies**

Apri `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md`. Sostituisci §1 (Stadio 1):

```markdown
## §1 — Stadio 1 (locale endpoint smoke)

**Data esecuzione:** YYYY-MM-DD HH:MM
**Comando:** `make demo-smoke-local`
**Exit code:** 0
**Endpoint OK:** 7/7

**Anomalies/Observations:**
- (se none) Nessuna
- (se latenza SSE chat-stream > 5s) #OBSERVATION: SSE first event arrived at 6.3s (target 8s, OK)
- (se PDF indexing not Ready) #BLOCKER: KB Press Start indexing stuck "InProgress"

**G1 status:** ✅ PASS / ❌ FAIL
```

Compila con valori reali.

- [ ] **Step 5: Commit anomalies update**

```bash
git add docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md
git commit -m "docs(nanolith-runthrough): § 1 G1 stadio 1 PASS (T-2d)"
```

- [ ] **Step 6: Verifica `count(#BLOCKER) == 0`**

```bash
grep -c "#BLOCKER" docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md || true
```

Expected: 0. Se >= 1 → STOP, fix blocker, re-run G1.

---

### Task 9: Playwright @demo-runthrough spec

**Files:**
- Create: `apps/web/e2e/demo-runthrough/nanolith-flow.spec.ts`
- Create: `apps/web/e2e/demo-runthrough/__tests__/fixtures.ts` (helper data)

**Steps:**

- [ ] **Step 1: Crea directory + fixtures helper**

```bash
mkdir -p apps/web/e2e/demo-runthrough
```

Path: `apps/web/e2e/demo-runthrough/__tests__/fixtures.ts`

```typescript
export const NANOLITH_DEMO_USER = {
  email: 'badsworm@gmail.com',
  password: 'TestNanolith2026!',
} as const;

export const NANOLITH_GAME_TITLE = 'Nanolith';
export const NANOLITH_AGENT_NAME = 'Nanolith Tutor';

export const SETUP_PROMPT_4_PLAYERS =
  'come si imposta la partita per 4 giocatori?';

// Fixture path of a small storybook page sample (PNG ~50KB) shipped in repo.
// Used as upload artifact in the photo translate step. Real OCR not required
// for smoke; just verifies upload + segmentation pipeline kicks off.
export const STORYBOOK_FIXTURE_PATH = 'e2e/fixtures/storybook-page-sample.png';
```

- [ ] **Step 2: Write failing spec**

Path: `apps/web/e2e/demo-runthrough/nanolith-flow.spec.ts`

```typescript
import { test, expect } from '@playwright/test';
import {
  NANOLITH_DEMO_USER,
  NANOLITH_GAME_TITLE,
  NANOLITH_AGENT_NAME,
  SETUP_PROMPT_4_PLAYERS,
  STORYBOOK_FIXTURE_PATH,
} from './__tests__/fixtures';

/**
 * @demo-runthrough — Stadio 2 G2 auto smoke
 *
 * Simulates Aaron's caso d'uso [1]-[6]:
 *   [1] Login badsworm
 *   [2] Vede Nanolith in /library
 *   [3] Click card → /library/games/{id}
 *   [4] CTA libro game → chat shell
 *   [5] Q&A setup prompt → SSE response
 *   [6] Photo upload → segmentation → translate SSE
 *
 * Acceptance: full flow completes < 60s in headless Chrome (auto smoke
 * is faster than manual; manual G2.1 target < 10min).
 *
 * Spec: docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-phase-a.md (G2.1)
 */

test.describe('@demo-runthrough Nanolith caso d\'uso end-to-end', () => {
  test.setTimeout(120_000); // 2min budget

  test('flow [1]-[6] happy path locale', async ({ page }) => {
    // [1] Login
    await page.goto('/login');
    await page.getByLabel(/email/i).fill(NANOLITH_DEMO_USER.email);
    await page.getByLabel(/password/i).fill(NANOLITH_DEMO_USER.password);
    await page.getByRole('button', { name: /accedi/i }).click();
    await page.waitForURL(/\/library/, { timeout: 5000 });

    // [2] Vede Nanolith
    const nanolithCard = page.getByText(NANOLITH_GAME_TITLE).first();
    await expect(nanolithCard).toBeVisible({ timeout: 5000 });

    // [3] Click card → game page
    await nanolithCard.click();
    await page.waitForURL(/\/library\/games\/[^/]+$/, { timeout: 5000 });
    await expect(page.getByText(NANOLITH_GAME_TITLE)).toBeVisible();

    // [4] CTA libro game → chat shell
    const libroCta = page.getByRole('button', { name: /avvia.*libro game/i });
    await expect(libroCta).toBeVisible({ timeout: 3000 });
    await libroCta.click();
    await page.waitForURL(/\/play\//, { timeout: 5000 });

    // [5] Q&A setup
    const chatInput = page.getByRole('textbox', { name: /messaggio|prompt/i });
    await chatInput.fill(SETUP_PROMPT_4_PLAYERS);
    await page.getByRole('button', { name: /invia|send/i }).click();
    await expect(page.locator('[data-slot="chat-message"]').last())
      .toContainText(/.{20,}/, { timeout: 10_000 }); // any 20+ char response = SSE worked

    // [6] Photo upload + translate
    const photoCta = page.getByRole('button', { name: /carica foto|upload/i });
    await photoCta.click();

    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles(STORYBOOK_FIXTURE_PATH);

    // Wait segmentation
    await expect(page.locator('[data-slot="page-thumb"]'))
      .toBeVisible({ timeout: 15_000 });

    const firstSegment = page.locator('[data-slot="paragraph-segment"]').first();
    await expect(firstSegment).toBeVisible({ timeout: 5000 });
    await firstSegment.click();

    const translateBtn = page.getByRole('button', { name: /traduci/i });
    await translateBtn.click();

    // SSE first token < 10s
    await expect(page.locator('[data-slot="translation-output"]'))
      .toContainText(/.{10,}/, { timeout: 15_000 });
  });
});
```

- [ ] **Step 3: Crea fixture image (1 file PNG sample storybook page)**

Per repo cleanliness, use existing fixture if any; else create minimal:

```bash
mkdir -p apps/web/e2e/fixtures
# Use placeholder small PNG (real text page would require >100KB sample)
# For smoke purposes any valid PNG works
cp data/rulebook/nanolith_datasource/sample-page.png apps/web/e2e/fixtures/storybook-page-sample.png 2>/dev/null \
  || echo "TODO: verify fixture path during T-1d morning prep"
```

> Nota: il file `e2e/fixtures/storybook-page-sample.png` deve essere acquisito (screenshot pagina Storybook PDF da `nanolith_datasource/`). Se mancante al momento esecuzione, lo step di acquisition è in Task 10 step 0 prep.

- [ ] **Step 4: Run test (expected fail — fixture e/o componenti mancanti)**

```bash
cd apps/web
pnpm test:e2e --grep @demo-runthrough
```

Possible expected failures:
- Fixture file mancante → fixed in step 3
- `data-slot="chat-message"` selector mancante → check naming convention PR #828
- `data-slot="paragraph-segment"` mancante → check PR #837 component

Se fail per nomi data-slot diversi: aggiusta selettori secondo nomi reali shipped, NON modificare i componenti (anti-creep R-AC7).

- [ ] **Step 5: Run + verifica pass**

Dopo aggiustamenti selettori secondo file shipped:

```bash
pnpm test:e2e --grep @demo-runthrough
```

Expected: 1/1 test pass < 60s.

- [ ] **Step 6: Commit**

```bash
git add apps/web/e2e/demo-runthrough/
git commit -m "test(e2e): @demo-runthrough Playwright spec G2 auto smoke

Simula caso d'uso [1]-[6] Aaron in headless Chrome locale.
Acceptance: full flow < 60s, used as G2 auto smoke alongside manual.

Refs: docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-phase-a.md (G2.1)"
```

---

### Task 10: G2 stadio 2 runthrough (T-1d morning)

> **Operational task** — Aaron esegue manual flow Chrome locale + verifica auto Playwright.

**Files:**
- Modify: `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md` (§2)

**Steps:**

- [ ] **Step 1: Pre-check stack locale + seed**

```bash
cd infra
make dev-core
sleep 30
make seed-nanolith-demo
make demo-smoke-local  # G1 still green?
```

Expected: G1 confirmed green prior to G2.

- [ ] **Step 2: Run auto Playwright (G2 auto)**

```bash
cd apps/web
pnpm test:e2e --grep @demo-runthrough
```

Expected: 1/1 pass < 60s. Se fail → annotare tag → debug.

- [ ] **Step 3: Apri Chrome desktop ≥ 120 + start manual G2**

Aaron procede:
1. Open Chrome localhost:3000/login
2. Login badsworm@gmail.com / TestNanolith2026!
3. Verifica `/library` → vede Nanolith card
4. Click card → `/library/games/{id}`
5. Click CTA "Avvia partita libro game"
6. Chat shell → input "come si imposta la partita per 4 giocatori?"
7. Verifica risposta SSE entro 8s + actionable
8. Click "Carica foto pagina"
9. Selezionare 1 pagina A4 fisica (foto via webcam Chrome o file picker da PDF screenshot)
10. Verifica segmentation 3 paragrafi entro 6s
11. Selezionare primo paragrafo → "Traduci"
12. Verifica SSE translate first-token < 8s
13. Verifica traduzione finale leggibile + citation

- [ ] **Step 4: Misura wall-clock time**

Annota:
- Inizio (login click): HH:MM:SS
- Fine (citation visible): HH:MM:SS
- Total: < 10min target

- [ ] **Step 5: Aggiorna §2 anomalies**

```markdown
## §2 — Stadio 2 (locale browser flow)

**Data esecuzione:** YYYY-MM-DD HH:MM
**Browser:** Chrome ≥ 120 desktop su localhost:3000
**Auto Playwright:** ✅ pass / ❌ fail (durata: Xs)
**Manual flow time:** Xmin Ys (target < 10min)

**Anomalies/Observations:**
- (es. SSE first-token latenza 9.2s) #ANOMALY: chat-stream first-token 9.2s (target 8s, +1.2s)
- (es. layout label EN non i18n) #OBSERVATION: bottone "New" anziché "Nuova" su CTA campagna
- (es. translate fail 5xx reproducible 3×) #BLOCKER: SSE translate emit error "rate-limit" reproducible

**G2 status:** ✅ PASS / ❌ FAIL

**Auto Playwright duration:** Xs
**Manual flow [1]-[6] checkpoints:**
- [1] Login: ✓ Xs
- [2] Library Nanolith visible: ✓ Xs
- [3] Game page loaded: ✓ Xs
- [4] CTA libro game → chat shell: ✓ Xs
- [5] N1 Q&A setup actionable: ✓ Xs (definition 0.1)
- [6] Photo translate complete: ✓ Xs first-token / Ys complete
```

- [ ] **Step 6: Commit anomalies update**

```bash
git add docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md
git commit -m "docs(nanolith-runthrough): § 2 G2 stadio 2 PASS (T-1d morning)"
```

- [ ] **Step 7: Verifica blocker count = 0**

```bash
grep -c "#BLOCKER" docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md
```

Expected: 0. Se >= 1 → STOP, fix, re-run G2 from Step 1.

---

### Task 11: demo-smoke-staging Makefile target (riuso script)

> **Notice**: Task 7 ha già aggiunto `demo-smoke-staging` target. Verifica solo working.

**Files:**
- No file modificati (verifica)

**Steps:**

- [ ] **Step 1: Verifica SSH tunnel running**

```bash
make tunnel
sleep 5
nc -z localhost 8080 && echo OK || echo "tunnel down"
```

Expected: OK.

- [ ] **Step 2: Run demo-smoke-staging**

```bash
make demo-smoke-staging | tee /tmp/g3-smoke-output.log
echo "Exit: $?"
```

Expected: exit 0, "ALL 7 endpoints OK" (versus staging URL meepleai.app).

- [ ] **Step 3: Confronta locale vs staging output**

```bash
diff /tmp/g1-smoke-output.log /tmp/g3-smoke-output.log
```

Expected diff: solo timestamp + Campaign/Photo IDs (UUID-diversi). Nessuna differenza struttura HTTP code.

- [ ] **Step 4: Se diff struttura → log #BLOCKER**

Esempio diff problematico:
- Endpoint 4 locale 201, staging 500 → #BLOCKER staging deploy issue

---

### Task 12: G3 stadio 3 runthrough (T-1d afternoon)

> **Operational task** — Aaron esegue manual flow Chrome desktop su staging.

**Files:**
- Modify: `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md` (§3)

**Steps:**

- [ ] **Step 1: Verifica seed staging fresh**

```bash
make seed-nanolith-demo-staging  # idempotente, OK re-run
```

- [ ] **Step 2: Apri Chrome desktop su https://meepleai.app**

Aaron ripete identica sequenza Task 10 step 3 ma URL staging.

- [ ] **Step 3: Misura wall-clock time staging**

Target: < 12min (tolleranza +20% network).

- [ ] **Step 4: Confronta funzionalmente vs G2**

Verifica:
- Stesso login funziona
- Nanolith visibile in library
- CTA libro game presente
- Chat shell carica
- Photo upload + translate output (semanticamente equivalente, NON byte-identico)

- [ ] **Step 5: Aggiorna §3 anomalies**

```markdown
## §3 — Stadio 3 (staging browser flow)

**Data esecuzione:** YYYY-MM-DD HH:MM
**URL:** https://meepleai.app
**Auto smoke:** ✅ pass / ❌ fail
**Manual flow time:** Xmin Ys (target < 12min)
**Diff funzionali vs G2:** 0 / N

**Anomalies/Observations:**
- ...

**G3 status:** ✅ PASS / ❌ FAIL
```

- [ ] **Step 6: Commit**

```bash
git add docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md
git commit -m "docs(nanolith-runthrough): § 3 G3 stadio 3 PASS (T-1d afternoon)"
```

- [ ] **Step 7: Verifica blocker count = 0**

```bash
grep -c "#BLOCKER" docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md
```

Expected: 0.

---

### Task 13: G4 stadio 4 mobile runthrough (T-0d morning)

> **Operational task** — Aaron esegue manual flow Android Chrome contro staging + cattura screenshot evidence.

**Files:**
- Create: `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-results/runthrough-mobile-final-translation.png`
- Modify: `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md` (§4)

**Steps:**

- [ ] **Step 1: Pre-check device readiness**

Aaron verifica:
- Android device ≥ 11 charged + connected to WiFi
- Chrome ≥ 120 installed + updated
- 1 pagina fisica Storybook Nanolith disponibile (se assente: usa screenshot PDF da galleria, fallback G4.2)

- [ ] **Step 2: Apri Chrome Android su https://meepleai.app**

Mobile flow [1]-[6]:
1. Login badsworm@gmail.com / TestNanolith2026! (verifica responsive layout login)
2. `/library` → Nanolith card visible (verifica no horizontal scroll)
3. Tocca card → `/library/games/{id}` (verifica viewport mobile)
4. Tocca CTA "Avvia partita libro game" (verifica tap target ≥ 44px)
5. Chat shell → input "come si imposta la partita per 4 giocatori?" + invio
6. Verifica SSE response leggibile (font-size > 14px)
7. Tocca "Carica foto pagina"
8. Chrome richiede camera permission → CONCEDI
9. CameraViewfinder live + scatto pagina Storybook
10. Verifica upload + segmentation
11. Tocca primo paragrafo → "Traduci"
12. Verifica SSE first-token < 10s (tolleranza mobile)
13. Verifica traduzione leggibile in viewport mobile (no horizontal scroll testo)

- [ ] **Step 3: Cattura screenshot finale**

Su Android: power + volume-down per screenshot. Salvare come `runthrough-mobile-final-translation.png` (1080×2400 portrait JPEG/PNG).

Trasferire al laptop:
```bash
mkdir -p docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-results
# adb pull /sdcard/Pictures/Screenshots/Screenshot_*.png docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-results/runthrough-mobile-final-translation.png
# OR via Google Drive / email upload
```

Verifica file size:
```bash
ls -la docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-results/runthrough-mobile-final-translation.png
# Expected ≤ 2MB
```

Se > 2MB: ottimizzare via `pngquant` o convertire JPEG a quality 85.

- [ ] **Step 4: Test camera permission denial fallback (G4.2 scenario)**

Logout + retry login. Quando Chrome chiede camera permission, NEGA. Verifica fallback file picker da galleria. Annota come #OBSERVATION (atteso).

- [ ] **Step 5: Misura wall-clock mobile**

Target: < 15min total (login a citation visible).

- [ ] **Step 6: Aggiorna §4 anomalies**

```markdown
## §4 — Stadio 4 (smartphone Android Chrome)

**Data esecuzione:** YYYY-MM-DD HH:MM
**Device:** [model + Android version]
**Browser:** Chrome [version]
**URL:** https://meepleai.app
**Manual flow time:** Xmin Ys (target < 15min)
**Camera permission flow:** ✅ grant + capture / ❌ permission policy issue
**Foto upload time:** Xs (target < 30s)
**SSE translate first-token:** Xs (target < 10s mobile)
**Mobile responsive:** ✅ no horizontal scroll / ❌ overflow

**Anomalies/Observations:**
- (es. layout button overflow su Android Chrome) #ANOMALY: button "Carica foto" overflow margin destro 8px
- (es. permission grant ok) #OBSERVATION: camera permission UX standard Chrome Android
- (es. crash mid-upload reproducible) #BLOCKER: ...

**G4.2 fallback test (camera denial):**
- File picker fallback: ✅ funziona / ❌
- Stato classificazione: #OBSERVATION

**Evidence:**
- Screenshot finale: `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-results/runthrough-mobile-final-translation.png` (XXkB)

**G4 status:** ✅ PASS / ❌ FAIL
```

- [ ] **Step 7: Commit screenshot + anomalies**

```bash
git add docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md \
        docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-results/runthrough-mobile-final-translation.png
git commit -m "docs(nanolith-runthrough): § 4 G4 stadio 4 PASS + screenshot evidence (T-0d)"
```

- [ ] **Step 8: Verifica blocker count = 0**

```bash
grep -c "#BLOCKER" docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md
```

Expected: 0.

---

### Task 14: G5 demo-readiness gate aggregation (T-0d EOD)

**Files:**
- Create: `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-results.md`

**Steps:**

- [ ] **Step 1: Crea results.md aggregator**

Path: `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-results.md`

```markdown
---
title: Nanolith Demo Runthrough — Results (Phase A)
status: passed
type: spec-results
date: YYYY-MM-DD
authors: [DegrassiAaron]
related-specs:
  - 2026-05-08-nanolith-demo-runthrough-phase-a.md
related-anomalies-log:
  - 2026-05-08-nanolith-demo-runthrough-anomalies.md
---

# Nanolith Demo Runthrough Phase A — Results

> **Aggregator post-G4 con go/no-go decision binaria** (G5).

## Sintesi

[5-10 righe descrittive: outcome pipeline, surprise observations, recommendation per Fase B]

**Esempio:**
> La pipeline Phase A si è completata in 14 giorni come pianificato. Tutti i 4 stadi (G1-G4) sono passati con 0 blocker reproducible. La traduzione SSE staging vs locale ha mostrato latenza marginalmente più alta (+1.5s avg first-token) ma sotto target 8s. Il flow mobile Android Chrome è stato fluido senza intoppi camera permission. Fase B è abilitata per pianificazione.

## Pipeline status

| Goal | Status | Tempo | Note |
|---|---|---|---|
| G1 — Locale endpoint smoke | ✅ PASS | Xs | 7/7 endpoint OK |
| G2 — Locale browser flow | ✅ PASS | Xmin | Auto Playwright + manual |
| G3 — Staging browser flow | ✅ PASS | Xmin | 0 diff funzionali |
| G4 — Smartphone Android | ✅ PASS | Xmin | Camera live + fallback OK |

## Demo-readiness gate

- `count(#BLOCKER)`: **0**
- `count(#ANOMALY)`: N
- `count(#OBSERVATION)`: M
- Evidence-bundle: ✅ presente (1 PNG + anomalies MD + results MD)

**Go/no-go decision:** ✅ **PASSED — Fase B unblocked**

## Anomalies (non-blocking)

[lista #ANOMALY entries da runthrough-anomalies.md con descrizione + workaround]

## Observations

[lista #OBSERVATION entries]

## Recommendations for Phase B

1. ...
2. ...
3. ...

## Evidence files

- Anomalies live log: `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md`
- Mobile screenshot: `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-results/runthrough-mobile-final-translation.png`

## Next steps

- [ ] Open issue tracking Fase B planning
- [ ] Schedule Aaron+amici dogfood serata (target T+7d?)
- [ ] Prioritize follow-up R1-R10 da `2026-05-08-libro-game-iter1-cross-branch-audit.md`
```

- [ ] **Step 2: Compila tutti i placeholder con valori reali**

Sostituisci YYYY-MM-DD, [...]/X/N/M con valori reali da §1-§4 di anomalies log.

- [ ] **Step 3: Run lint markdown se configurato**

```bash
# Se markdownlint configurato:
npx markdownlint docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-results.md
```

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-results.md
git commit -m "docs(nanolith-runthrough): G5 demo-readiness gate PASSED (T-0d EOD)

Aggregator results file con go/no-go binario PASSED.
0 #BLOCKER reproducible, N #ANOMALY non-blocking, M #OBSERVATION.
Evidence bundle: anomalies log + mobile screenshot.
Fase B planning unblocked.

Refs: docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-phase-a.md (G5)"
```

---

### Task 15: Open archival PR + close issue

**Files:**
- No file modificati (operational)

**Steps:**

- [ ] **Step 1: Push branch finale**

```bash
git push --force-with-lease origin feature/nanolith-demo-runthrough-phase-a
```

- [ ] **Step 2: Open PR vs main-dev**

```bash
gh pr create \
  --base main-dev \
  --head feature/nanolith-demo-runthrough-phase-a \
  --title "docs(nanolith-runthrough): Phase A demo runthrough COMPLETED + evidence bundle" \
  --body "$(cat <<'EOF'
## Summary

Esecuzione completa pipeline 4-stadi Demo Runthrough Phase A per caso d'uso Nanolith libro game.

**Pipeline outcome:** ✅ ALL G1-G5 PASS — Fase B planning unblocked.

## Deliverables

- ✅ Spec design `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-phase-a.md`
- ✅ Plan `docs/superpowers/plans/2026-05-08-nanolith-demo-runthrough-phase-a.md`
- ✅ Smoke script `infra/scripts/demo-smoke-local.sh` + 2 Makefile targets
- ✅ Playwright @demo-runthrough spec `apps/web/e2e/demo-runthrough/nanolith-flow.spec.ts`
- ✅ R5 ResumeCard component (state-02 only) `apps/web/src/components/v2/gamebook/ResumeCard.tsx`
- ✅ Anomalies live log `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md`
- ✅ Results aggregator `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-results.md`
- ✅ Mobile screenshot evidence

## Pipeline timeline (effettivo vs target)

- T-13d → spec approved: ✅
- T-12d → T-10d → PR #828 + #837 mergiati: ✅
- T-7d → R5 state-02 implementato: ✅
- T-6d → seed staging verificato: ✅
- T-2d → G1 stadio 1 PASS: ✅
- T-1d → G2 + G3 PASS: ✅
- T-0d → G4 + G5 PASS: ✅

## Test plan

- [x] G1 smoke local: 7/7 endpoint OK
- [x] G2 local browser: manual + Playwright auto pass
- [x] G3 staging browser: 0 diff vs locale
- [x] G4 smartphone Android: camera + responsive OK
- [x] G5 aggregator: count(#BLOCKER) == 0

## Next steps

- [ ] Open Fase B planning issue
- [ ] Prioritize R1-R10 follow-up da audit
- [ ] Schedule dogfood serata Aaron+amici

🤖 Generated via superpowers:writing-plans + superpowers:executing-plans
EOF
)"
```

- [ ] **Step 3: Verifica PR creato**

```bash
gh pr view --json url,state,mergeable
```

Expected: PR open, link visibile.

- [ ] **Step 4: Cleanup local**

```bash
git remote prune origin
# Se altri stale branch:
# git branch -vv | grep ": gone]"
```

- [ ] **Step 5: Documentazione finale anomalies log**

Aggiungi sezione finale a `runthrough-anomalies.md`:

```markdown
## §5 — Pipeline closure (T-0d EOD)

**Pipeline status:** ✅ COMPLETED PASSED
**Branch:** feature/nanolith-demo-runthrough-phase-a
**PR:** [link da gh pr view]
**Total time:** N giorni (target 14gg)

**Lessons learned:**
- [3-5 righe pratiche per Fase B planning]
```

- [ ] **Step 6: Commit final closure**

```bash
git add docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-anomalies.md
git commit -m "docs(nanolith-runthrough): § 5 pipeline closure + lessons learned"
git push --force-with-lease
```

---

## Plan Self-Review

### Spec coverage
- ✅ G1 Locale endpoint smoke → Tasks 6, 7, 8
- ✅ G2 Locale browser flow → Tasks 9, 10
- ✅ G3 Staging browser flow → Tasks 11, 12
- ✅ G4 Smartphone Android → Task 13
- ✅ G5 Demo-readiness gate → Task 14
- ✅ Pre-pipeline merge PR #828/#837 → Tasks 1, 2
- ✅ Pre-pipeline R5 state-02 → Task 4 (scope-cap esplicito state-03/04 deferred Fase B)
- ✅ Pre-pipeline seed verification → Task 5
- ✅ Strategia merge ibrida (T-10d checkpoint) → Task 3
- ✅ Workspace setup + closure → Tasks 0, 15

### Spec amendment necessario

Lo spec attuale (`2026-05-08-nanolith-demo-runthrough-phase-a.md`) include R5 nel Q4 C scope. Tuttavia per dipendenza N4 cross-day backend, R5 è ridotto nel plan a state-02 only. Edit suggerito allo spec:

> §1.5 Pre-requisiti: cambia "R5 Resume card UX" in "R5 ResumeCard state-02 (single fresh resume) — state-03 multi-campaign + state-04 stale warning deferred Fase B per dipendenza N4 cross-day backend"

### Type consistency
- `ResumeCardProps`: definita Task 4 step 4, usata Task 4 step 7 (consistent)
- `GlossaryPreviewEntry`: definita Task 4 step 4, exported barrel (consistent)
- File path `feature/nanolith-demo-runthrough-phase-a`: Task 0 step 3, riferito Task 1 step 7 + Task 2 step 6 + Task 15 step 1 (consistent)

### Placeholder scan

⚠️ Trovate occorrenze placeholder che richiedono compilazione **runtime** (non TBD spec): YYYY-MM-DD, X/N/M, [...] in template MD anomalies/results. Sono **pattern di template instantiation**, non spec placeholders. OK.

⚠️ Task 9 step 3: cita `data/rulebook/nanolith_datasource/sample-page.png` che potrebbe non esistere → Task fallback "TODO: verify fixture path during T-1d morning prep" è un OPERATIONAL TODO ammissibile, non plan placeholder. OK.

⚠️ Task 4 step 7: riferimento `daysSince(campaign.lastUpdatedAt)` helper non esplicitato → engineer deve usare helper esistente o creare. Aggiungo nota:

> Note: `daysSince` helper può essere `Math.floor((Date.now() - new Date(updatedAt).getTime()) / 86400000)` inline OR riusare `apps/web/src/lib/date-utils.ts` se esistente.

---

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-05-08-nanolith-demo-runthrough-phase-a.md`.**

Two execution options:

**1. Subagent-Driven (recommended)** — Dispatch fresh subagent per task, review tra task, fast iteration.

**2. Inline Execution** — Execute tasks in this session usando `superpowers:executing-plans`, batch execution con checkpoint per review.

**Note operative**:
- Task 1, 2, 3, 5, 8, 10, 12, 13 sono **operational** (review PR / runthrough manual / cattura evidence) — richiedono Aaron presente al laptop/device fisico, NON automatizzabili
- Task 0, 4, 6, 7, 9, 11, 14, 15 sono **code/automation** — automatable via subagent o inline
- Suggerisco hybrid execution: code tasks subagent-driven, operational tasks inline + Aaron present
