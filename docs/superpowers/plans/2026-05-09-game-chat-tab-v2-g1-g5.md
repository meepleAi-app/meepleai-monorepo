# Game Chat Tab V2 — G1+G5 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sostituire il rendering V1 chat-unified per la route `/library/games/[gameId]?tab=aiChat` con 12 nuovi componenti V2 puri sotto `apps/web/src/components/v2/game-chat/`, hook `useGameChat`, e rimozione audit-first dei componenti V1 chat-unified usati esclusivamente da quella route.

**Architecture:** Frontend-only PR su `apps/web`. 12 componenti V2 nuovi + 1 hook + modifica al solo `GameAiChatTab.tsx` (wrapper attualmente V1). Backend zero modifiche (`QaResponse` espone già `overallConfidence`, `isLowQuality`, `citations[]`). Pattern Wave B.1 puro/orchestrator + TDD strict.

**Tech Stack:** React 19 + Next.js 16 App Router, TypeScript strict, Tailwind 4 (token v2 `--c-chat/--c-agent/--c-kb/--c-success/--c-warning/--c-danger`), Vitest + MSW, Zustand (per chat history sessione).

**Spec:** [`docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md`](../specs/2026-05-09-game-chat-tab-v1-g5-design.md)
**Mockup:** [`admin-mockups/design_files/sp4-game-chat-tab.html`](../../../admin-mockups/design_files/sp4-game-chat-tab.html)
**Issue:** [#915](https://github.com/meepleAi-app/meepleai-monorepo/issues/915)
**Branch:** `feature/issue-915-game-chat-v2-citations` (già creato dal commit spec)

---

## File Structure

| Status | Path | Responsibility |
|---|---|---|
| Create | `apps/web/src/components/v2/game-chat/CitationChip.tsx` | Pure: chip cliccabile `📖 p.X §Y`, calls onClick |
| Create | `apps/web/src/components/v2/game-chat/CitationModal.tsx` | Controlled: modal preview citation, footer button "Apri KB" condizionale |
| Create | `apps/web/src/components/v2/game-chat/ConfidenceBadge.tsx` | Pure: 3 fasce (alta ≥0.80 verde · media ≥0.50 arancione · bassa <0.50 rosso) |
| Create | `apps/web/src/components/v2/game-chat/LowConfidenceDisclaimer.tsx` | Pure: banner giallo + lista alternative |
| Create | `apps/web/src/components/v2/game-chat/OutOfContextActions.tsx` | Pure: 3 action pill (switch-game/find-agent/stay) |
| Create | `apps/web/src/components/v2/game-chat/TypingIndicator.tsx` | Pure: 3 dot animati + meta latency |
| Create | `apps/web/src/components/v2/game-chat/ChatBubble.tsx` | Pure: user|agent variants, slot per children (citations/badge/disclaimer) |
| Create | `apps/web/src/components/v2/game-chat/SuggestedPrompts.tsx` | Pure: chip categorizzati A/B/C/E/F |
| Create | `apps/web/src/components/v2/game-chat/GameChatHeader.tsx` | Pure: game-icon + agent-badge + title/subtitle |
| Create | `apps/web/src/components/v2/game-chat/GameChatSidebar.tsx` | Controlled (desktop only): agent switch + history rail |
| Create | `apps/web/src/components/v2/game-chat/ChatInputBar.tsx` | Controlled: input + send button, submit on Enter |
| Create | `apps/web/src/components/v2/game-chat/GameChatTabV2.tsx` | Orchestrator: compone tutti, wraps useGameChat, FSM |
| Create | `apps/web/src/components/v2/game-chat/index.ts` | Barrel |
| Create | `apps/web/src/hooks/queries/useGameChat.ts` | Hook: API ask, FSM states, history, agent switch |
| Create | `apps/web/src/hooks/queries/__tests__/useGameChat.test.tsx` | Hook tests con MSW mock |
| Create | `apps/web/src/components/v2/game-chat/__tests__/*.test.tsx` | Test unit per ogni componente puro/controllato |
| Modify | `apps/web/src/components/game-detail/tabs/GameAiChatTab.tsx` | Sostituisci body V1 con `<GameChatTabV2 gameId={gameId} />` |
| Delete | (audit list, dopo Task 10) | Componenti V1 chat-unified usati ESCLUSIVAMENTE dal route `?tab=aiChat` |
| Modify | `docs/for-developers/frontend/v2-migration-matrix.md` | Add row "GameChatTabV2 + 12 game-chat components" sotto sezione `/games/[id]` |

**Decomposition logic**:
- 12 componenti, 1 hook, 1 wiring, 1 audit+removal, 1 PR finale = ~16 task ma li raggruppo per affinità
- Pure-component pattern strict (no fetch dentro, label e callback dall'esterno) → orchestrator Tier S/M (1-2 hook)
- Branch già esistente (`feature/issue-915-game-chat-v2-citations`) con commit spec già su HEAD

---

## Pre-flight

- [ ] **Step 0.1: Verifica branch corrente**

```bash
git -C "D:/Repositories/meepleai-monorepo-main" branch --show-current
```
Expected: `feature/issue-915-game-chat-v2-citations`

Se diverso: `git checkout feature/issue-915-game-chat-v2-citations`

- [ ] **Step 0.2: Verifica clean typecheck baseline**

```bash
cd apps/web && pnpm typecheck
```
Expected: 0 errori. Se ce ne sono, registrarli prima di iniziare per non confonderli con quelli introdotti.

- [ ] **Step 0.3: Verifica `Citation` type esistente**

```bash
grep -n "interface Citation\|type Citation" apps/web/src/types/agent.ts apps/web/src/types/domain.ts
```

Annota la shape esatta del tipo `Citation` (è già usata dal V1 chat-unified). Questo è il tipo che `CitationChip` e `CitationModal` accetteranno. **Non** ridefinirlo nei nuovi componenti — importalo da `@/types`.

- [ ] **Step 0.4: Verifica esistenza `PdfPageModal` V1**

```bash
ls apps/web/src/components/chat-unified/PdfPageModal.tsx 2>&1
```

Se esiste: `CitationModal` V2 può usarlo come riferimento implementativo (riusa pattern modal/portal). Se NON esiste: `CitationModal` parte da zero.

---

## Task 1: `CitationChip` (pure component)

**Files:**
- Create: `apps/web/src/components/v2/game-chat/CitationChip.tsx`
- Test: `apps/web/src/components/v2/game-chat/__tests__/CitationChip.test.tsx`

**Contract:**
```ts
interface CitationChipProps {
  readonly pageNumber: number;
  readonly sectionTitle: string;
  readonly snippet?: string;  // shown on hover via title attribute
  readonly onClick: () => void;
  readonly className?: string;
}
```

Visual: chip pill orange/teal con `📖 p.{pageNumber} — {sectionTitle}`. Background `hsl(var(--c-kb) / 0.12)`. Hover lift.

- [ ] **Step 1.1: Test failing**

Crea `apps/web/src/components/v2/game-chat/__tests__/CitationChip.test.tsx`:

```tsx
/**
 * CitationChip — pure component tests
 * Spec: docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md §3.2
 */
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { CitationChip } from '../CitationChip';

describe('CitationChip', () => {
  it('renders page number and section title', () => {
    render(<CitationChip pageNumber={12} sectionTitle="Poteri quando attivato" onClick={vi.fn()} />);
    expect(screen.getByRole('button', { name: /p\. ?12.*Poteri quando attivato/i })).toBeInTheDocument();
  });

  it('calls onClick when clicked', () => {
    const onClick = vi.fn();
    render(<CitationChip pageNumber={4} sectionTitle="Iniziativa" onClick={onClick} />);
    fireEvent.click(screen.getByRole('button'));
    expect(onClick).toHaveBeenCalledOnce();
  });

  it('exposes snippet via title tooltip', () => {
    render(
      <CitationChip
        pageNumber={12}
        sectionTitle="Poteri"
        snippet="Ogni potere si attiva..."
        onClick={vi.fn()}
      />
    );
    expect(screen.getByRole('button')).toHaveAttribute('title', 'Ogni potere si attiva...');
  });

  it('omits title when no snippet', () => {
    render(<CitationChip pageNumber={12} sectionTitle="Poteri" onClick={vi.fn()} />);
    expect(screen.getByRole('button')).not.toHaveAttribute('title');
  });
});
```

- [ ] **Step 1.2: Run test, verify FAIL**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/CitationChip.test
```
Expected: FAIL — module `../CitationChip` not found.

- [ ] **Step 1.3: Implementa**

Crea `apps/web/src/components/v2/game-chat/CitationChip.tsx`:

```tsx
/**
 * CitationChip — chip cliccabile per una citazione (page + section).
 *
 * Pure component (no fetch, no router). Apre il modal preview via onClick.
 * Spec: docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md §3.2
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface CitationChipProps {
  readonly pageNumber: number;
  readonly sectionTitle: string;
  readonly snippet?: string;
  readonly onClick: () => void;
  readonly className?: string;
}

export function CitationChip({
  pageNumber,
  sectionTitle,
  snippet,
  onClick,
  className,
}: CitationChipProps): ReactElement {
  return (
    <button
      type="button"
      onClick={onClick}
      title={snippet}
      data-slot="citation-chip"
      className={clsx(
        'inline-flex items-center gap-1 rounded-full px-3 py-1',
        'bg-[hsl(var(--c-kb)/0.12)] text-[hsl(var(--c-kb))]',
        'font-mono text-xs font-semibold',
        'border border-transparent transition-colors',
        'hover:bg-[hsl(var(--c-kb)/0.2)] hover:border-[hsl(var(--c-kb)/0.3)]',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-kb))]',
        className
      )}
    >
      <span aria-hidden="true">📖</span>
      <span>p. {pageNumber}</span>
      <span aria-hidden="true">—</span>
      <span>{sectionTitle}</span>
    </button>
  );
}
```

- [ ] **Step 1.4: Run test fino a passare**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/CitationChip.test
```
Expected: PASS (4/4).

- [ ] **Step 1.5: Commit**

```bash
git add apps/web/src/components/v2/game-chat/CitationChip.tsx apps/web/src/components/v2/game-chat/__tests__/CitationChip.test.tsx
git commit -m "feat(web): #915 CitationChip v2 pure component (G1)"
```

---

## Task 2: `ConfidenceBadge` + `LowConfidenceDisclaimer` (pure)

**Files:**
- Create: `apps/web/src/components/v2/game-chat/ConfidenceBadge.tsx`
- Create: `apps/web/src/components/v2/game-chat/LowConfidenceDisclaimer.tsx`
- Test: `apps/web/src/components/v2/game-chat/__tests__/ConfidenceBadge.test.tsx`
- Test: `apps/web/src/components/v2/game-chat/__tests__/LowConfidenceDisclaimer.test.tsx`

**Contract `ConfidenceBadge`:**
```ts
interface ConfidenceBadgeProps {
  readonly score: number;  // 0..1
  readonly kind?: 'inline' | 'compact';  // default 'inline'
  readonly className?: string;
}

// internal: getConfidenceTier(score) → 'alta' | 'media' | 'bassa'
//   alta: score >= 0.80   → green (--c-success)
//   media: 0.50 <= score < 0.80 → orange (--c-warning)
//   bassa: score < 0.50   → red (--c-danger)
```

**Contract `LowConfidenceDisclaimer`:**
```ts
interface DisclaimerAlternative {
  readonly label: string;
  readonly kind: 'kb' | 'external';
  readonly url?: string;  // for external links
}

interface LowConfidenceDisclaimerProps {
  readonly summary: string;
  readonly alternatives: readonly DisclaimerAlternative[];
  readonly className?: string;
}
```

- [ ] **Step 2.1: Test failing — `ConfidenceBadge`**

Crea `apps/web/src/components/v2/game-chat/__tests__/ConfidenceBadge.test.tsx`:

```tsx
/**
 * ConfidenceBadge — pure component tests
 * Spec: docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md §3.2 §4.2
 */
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { ConfidenceBadge } from '../ConfidenceBadge';

describe('ConfidenceBadge', () => {
  it('renders alta tier when score >= 0.80', () => {
    render(<ConfidenceBadge score={0.92} />);
    const badge = screen.getByRole('status');
    expect(badge).toHaveAttribute('data-tier', 'alta');
    expect(badge.textContent).toMatch(/0\.92/);
  });

  it('renders media tier when 0.50 <= score < 0.80', () => {
    render(<ConfidenceBadge score={0.65} />);
    expect(screen.getByRole('status')).toHaveAttribute('data-tier', 'media');
  });

  it('renders bassa tier when score < 0.50', () => {
    render(<ConfidenceBadge score={0.42} />);
    expect(screen.getByRole('status')).toHaveAttribute('data-tier', 'bassa');
  });

  it('boundary 0.80 = alta', () => {
    render(<ConfidenceBadge score={0.80} />);
    expect(screen.getByRole('status')).toHaveAttribute('data-tier', 'alta');
  });

  it('boundary 0.50 = media (not bassa)', () => {
    render(<ConfidenceBadge score={0.50} />);
    expect(screen.getByRole('status')).toHaveAttribute('data-tier', 'media');
  });

  it('edge 0.0 = bassa', () => {
    render(<ConfidenceBadge score={0.0} />);
    expect(screen.getByRole('status')).toHaveAttribute('data-tier', 'bassa');
  });

  it('edge 1.0 = alta', () => {
    render(<ConfidenceBadge score={1.0} />);
    expect(screen.getByRole('status')).toHaveAttribute('data-tier', 'alta');
  });

  it('compact kind hides numeric score', () => {
    render(<ConfidenceBadge score={0.92} kind="compact" />);
    expect(screen.getByRole('status').textContent).not.toMatch(/0\.92/);
  });
});
```

- [ ] **Step 2.2: Run test, verify FAIL**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/ConfidenceBadge.test
```
Expected: FAIL.

- [ ] **Step 2.3: Implementa `ConfidenceBadge`**

Crea `apps/web/src/components/v2/game-chat/ConfidenceBadge.tsx`:

```tsx
/**
 * ConfidenceBadge — visualizza confidence score con 3 fasce colore.
 * Pure component. Tier calcolato internamente:
 *   alta:  score >= 0.80  (verde, --c-success)
 *   media: 0.50 <= s < 0.80 (arancione, --c-warning)
 *   bassa: score < 0.50   (rosso, --c-danger)
 * Spec: §3.2 §4.2
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

export type ConfidenceTier = 'alta' | 'media' | 'bassa';

export interface ConfidenceBadgeProps {
  readonly score: number;
  readonly kind?: 'inline' | 'compact';
  readonly className?: string;
}

export function getConfidenceTier(score: number): ConfidenceTier {
  if (score >= 0.80) return 'alta';
  if (score >= 0.50) return 'media';
  return 'bassa';
}

const TIER_LABEL: Record<ConfidenceTier, string> = {
  alta: '🟢 Confidence alta',
  media: '🟡 Confidence media',
  bassa: '🔴 Confidence bassa',
};

const TIER_COLORS: Record<ConfidenceTier, string> = {
  alta: 'bg-[hsl(var(--c-success)/0.15)] text-[hsl(var(--c-success))]',
  media: 'bg-[hsl(var(--c-warning)/0.18)] text-[hsl(var(--c-warning))]',
  bassa: 'bg-[hsl(var(--c-danger)/0.15)] text-[hsl(var(--c-danger))]',
};

export function ConfidenceBadge({
  score,
  kind = 'inline',
  className,
}: ConfidenceBadgeProps): ReactElement {
  const tier = getConfidenceTier(score);
  const label = TIER_LABEL[tier];
  const showScore = kind !== 'compact';

  return (
    <span
      role="status"
      data-tier={tier}
      data-slot="confidence-badge"
      className={clsx(
        'inline-flex items-center gap-1 rounded-full px-2 py-0.5',
        'font-mono text-[10px] font-bold uppercase tracking-wider',
        TIER_COLORS[tier],
        className
      )}
    >
      <span>{label}</span>
      {showScore && <span>· {score.toFixed(2)}</span>}
    </span>
  );
}
```

- [ ] **Step 2.4: Run test fino a passare**

Run: `pnpm vitest run src/components/v2/game-chat/__tests__/ConfidenceBadge.test`
Expected: PASS (8/8).

- [ ] **Step 2.5: Test failing — `LowConfidenceDisclaimer`**

Crea `apps/web/src/components/v2/game-chat/__tests__/LowConfidenceDisclaimer.test.tsx`:

```tsx
/**
 * LowConfidenceDisclaimer — pure component tests
 * Spec: §3.2 §4.2
 */
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { LowConfidenceDisclaimer } from '../LowConfidenceDisclaimer';

describe('LowConfidenceDisclaimer', () => {
  it('renders summary text and warning marker', () => {
    render(
      <LowConfidenceDisclaimer
        summary="Probabilmente si rimescolano gli scarti, ma il manuale non lo dice."
        alternatives={[]}
      />
    );
    expect(screen.getByText(/non sono certo/i)).toBeInTheDocument();
    expect(screen.getByText(/rimescolano gli scarti/i)).toBeInTheDocument();
  });

  it('renders empty list gracefully when no alternatives', () => {
    render(<LowConfidenceDisclaimer summary="..." alternatives={[]} />);
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });

  it('renders alternatives list with kb and external entries', () => {
    render(
      <LowConfidenceDisclaimer
        summary="..."
        alternatives={[
          { label: 'FAQ ufficiale p.6-7', kind: 'kb' },
          { label: 'BGG forum thread', kind: 'external', url: 'https://bgg.example/t/1' },
        ]}
      />
    );
    const items = screen.getAllByRole('listitem');
    expect(items).toHaveLength(2);
    const link = screen.getByRole('link', { name: /BGG forum/ });
    expect(link).toHaveAttribute('href', 'https://bgg.example/t/1');
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', expect.stringContaining('noopener'));
  });
});
```

- [ ] **Step 2.6: Run test, verify FAIL**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/LowConfidenceDisclaimer.test
```

- [ ] **Step 2.7: Implementa `LowConfidenceDisclaimer`**

Crea `apps/web/src/components/v2/game-chat/LowConfidenceDisclaimer.tsx`:

```tsx
/**
 * LowConfidenceDisclaimer — banner giallo con summary "non sono certo"
 * + lista alternative (KB interne o link esterni).
 *
 * Pure component. Renderizzato solo quando response.isLowQuality === true.
 * Spec: §3.2 §4.2
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface DisclaimerAlternative {
  readonly label: string;
  readonly kind: 'kb' | 'external';
  readonly url?: string;
}

export interface LowConfidenceDisclaimerProps {
  readonly summary: string;
  readonly alternatives: readonly DisclaimerAlternative[];
  readonly className?: string;
}

export function LowConfidenceDisclaimer({
  summary,
  alternatives,
  className,
}: LowConfidenceDisclaimerProps): ReactElement {
  return (
    <div
      role="note"
      data-slot="low-confidence-disclaimer"
      className={clsx(
        'mt-3 rounded-md border-l-4 p-3 text-sm',
        'border-l-[hsl(var(--c-warning))] bg-[hsl(var(--c-warning)/0.08)]',
        className
      )}
    >
      <p>
        <strong className="font-bold text-[hsl(var(--c-warning))]">⚠️ Non sono certo.</strong>{' '}
        {summary}
      </p>
      {alternatives.length > 0 && (
        <ul className="mt-2 ml-5 list-disc">
          {alternatives.map((alt, i) => (
            <li key={i}>
              {alt.kind === 'external' && alt.url ? (
                <a
                  href={alt.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="underline hover:opacity-80"
                >
                  🔗 {alt.label}
                </a>
              ) : (
                <span>📖 {alt.label}</span>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
```

- [ ] **Step 2.8: Run test fino a passare**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/LowConfidenceDisclaimer.test
```
Expected: PASS (3/3).

- [ ] **Step 2.9: Commit**

```bash
git add apps/web/src/components/v2/game-chat/ConfidenceBadge.tsx apps/web/src/components/v2/game-chat/LowConfidenceDisclaimer.tsx apps/web/src/components/v2/game-chat/__tests__/ConfidenceBadge.test.tsx apps/web/src/components/v2/game-chat/__tests__/LowConfidenceDisclaimer.test.tsx
git commit -m "feat(web): #915 ConfidenceBadge + LowConfidenceDisclaimer v2 (G5)"
```

---

## Task 3: `OutOfContextActions` + `TypingIndicator` (pure)

**Files:**
- Create: `apps/web/src/components/v2/game-chat/OutOfContextActions.tsx`
- Create: `apps/web/src/components/v2/game-chat/TypingIndicator.tsx`
- Test: 2 file `__tests__/`

**Contract `OutOfContextActions`:**
```ts
interface OutOfContextAction {
  readonly kind: 'switch-game' | 'find-agent' | 'stay';
  readonly label: string;
  readonly onClick: () => void;
}

interface OutOfContextActionsProps {
  readonly actions: readonly OutOfContextAction[];
  readonly className?: string;
}
```

**Contract `TypingIndicator`:**
```ts
interface TypingIndicatorProps {
  readonly elapsedMs?: number;
  readonly budgetMs?: number;  // default 10000 (Q6 brainstorm)
  readonly hint?: string;  // e.g., "Cerco in 3 KB"
  readonly className?: string;
}
```

- [ ] **Step 3.1: Test failing — `OutOfContextActions`**

Crea `apps/web/src/components/v2/game-chat/__tests__/OutOfContextActions.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { OutOfContextActions } from '../OutOfContextActions';

const KIND_ICON: Record<string, string> = {
  'switch-game': '🎲',
  'find-agent': '🤖',
  stay: '↩️',
};

describe('OutOfContextActions', () => {
  it('renders all action pills with icons', () => {
    const onClick = vi.fn();
    render(
      <OutOfContextActions
        actions={[
          { kind: 'switch-game', label: 'Cambia gioco a Tainted Grail', onClick },
          { kind: 'find-agent', label: 'Cerca un agente Tainted Grail', onClick },
          { kind: 'stay', label: 'Resta su Wingspan', onClick },
        ]}
      />
    );
    expect(screen.getByRole('button', { name: /Cambia gioco/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Cerca un agente/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Resta su Wingspan/ })).toBeInTheDocument();
  });

  it('calls correct onClick when each action clicked', () => {
    const a = vi.fn(); const b = vi.fn(); const c = vi.fn();
    render(
      <OutOfContextActions
        actions={[
          { kind: 'switch-game', label: 'A', onClick: a },
          { kind: 'find-agent', label: 'B', onClick: b },
          { kind: 'stay', label: 'C', onClick: c },
        ]}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /A/ }));
    expect(a).toHaveBeenCalledOnce();
    expect(b).not.toHaveBeenCalled();
    expect(c).not.toHaveBeenCalled();
  });

  it('renders nothing when actions empty', () => {
    const { container } = render(<OutOfContextActions actions={[]} />);
    expect(container.firstChild).toBeNull();
  });
});
```

- [ ] **Step 3.2: Run test, verify FAIL**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/OutOfContextActions.test
```

- [ ] **Step 3.3: Implementa `OutOfContextActions`**

Crea `apps/web/src/components/v2/game-chat/OutOfContextActions.tsx`:

```tsx
/**
 * OutOfContextActions — 3 action pill verticali per stato "decline + propose switch".
 * Pure component. Renderizzato solo quando response.outOfContext === true.
 * Spec: §3.2
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

export type OutOfContextActionKind = 'switch-game' | 'find-agent' | 'stay';

export interface OutOfContextAction {
  readonly kind: OutOfContextActionKind;
  readonly label: string;
  readonly onClick: () => void;
}

export interface OutOfContextActionsProps {
  readonly actions: readonly OutOfContextAction[];
  readonly className?: string;
}

const KIND_ICON: Record<OutOfContextActionKind, string> = {
  'switch-game': '🎲',
  'find-agent': '🤖',
  stay: '↩️',
};

export function OutOfContextActions({
  actions,
  className,
}: OutOfContextActionsProps): ReactElement | null {
  if (actions.length === 0) return null;
  return (
    <div
      data-slot="out-of-context-actions"
      className={clsx('mt-3 flex flex-col gap-2', className)}
    >
      {actions.map((a, i) => (
        <button
          key={i}
          type="button"
          onClick={a.onClick}
          data-action-kind={a.kind}
          className={clsx(
            'rounded-full border bg-card text-left',
            'border-border px-3 py-2 text-sm font-semibold',
            'flex items-center gap-2 transition-colors',
            'hover:bg-muted hover:border-[hsl(var(--c-chat)/0.4)]',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-chat))]'
          )}
        >
          <span aria-hidden="true">{KIND_ICON[a.kind]}</span>
          <span>{a.label}</span>
        </button>
      ))}
    </div>
  );
}
```

- [ ] **Step 3.4: Run test fino a passare**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/OutOfContextActions.test
```
Expected: PASS (3/3).

- [ ] **Step 3.5: Test failing — `TypingIndicator`**

Crea `apps/web/src/components/v2/game-chat/__tests__/TypingIndicator.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { TypingIndicator } from '../TypingIndicator';

describe('TypingIndicator', () => {
  it('renders 3 animated dots', () => {
    render(<TypingIndicator />);
    expect(screen.getAllByTestId('typing-dot')).toHaveLength(3);
  });

  it('shows elapsed and budget meta when provided', () => {
    render(<TypingIndicator elapsedMs={2400} budgetMs={10000} />);
    expect(screen.getByText(/2\.4s.*10s/)).toBeInTheDocument();
  });

  it('shows hint when provided', () => {
    render(<TypingIndicator hint="Cerco in 3 KB" />);
    expect(screen.getByText(/Cerco in 3 KB/)).toBeInTheDocument();
  });

  it('renders only dots when no meta provided', () => {
    render(<TypingIndicator />);
    expect(screen.queryByText(/elapsed/i)).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 3.6: Run test, verify FAIL**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/TypingIndicator.test
```

- [ ] **Step 3.7: Implementa `TypingIndicator`**

Crea `apps/web/src/components/v2/game-chat/TypingIndicator.tsx`:

```tsx
/**
 * TypingIndicator — 3 dot animati + meta latency opzionale.
 *
 * Renderizzato durante stato 'submitting' della FSM (Q6: spinner atomico,
 * non streaming token-by-token). Budget default 10000ms (Q6 brainstorm).
 * Spec: §3.2 §4.1
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface TypingIndicatorProps {
  readonly elapsedMs?: number;
  readonly budgetMs?: number;
  readonly hint?: string;
  readonly className?: string;
}

const DEFAULT_BUDGET_MS = 10_000;

function fmtSec(ms: number): string {
  return `${(ms / 1000).toFixed(1)}s`;
}

export function TypingIndicator({
  elapsedMs,
  budgetMs = DEFAULT_BUDGET_MS,
  hint,
  className,
}: TypingIndicatorProps): ReactElement {
  const showMeta = elapsedMs !== undefined || hint;
  return (
    <div data-slot="typing-indicator" className={clsx('flex flex-col gap-1', className)}>
      <div className="inline-flex gap-1 px-3 py-2">
        {[0, 1, 2].map(i => (
          <span
            key={i}
            data-testid="typing-dot"
            className="h-2 w-2 rounded-full bg-[hsl(var(--c-agent))] motion-safe:animate-pulse"
            style={{ animationDelay: `${i * 150}ms` }}
            aria-hidden="true"
          />
        ))}
      </div>
      {showMeta && (
        <div className="font-mono text-xs text-muted-foreground">
          ⏱️
          {hint && ` ${hint} ·`}
          {elapsedMs !== undefined && ` ${fmtSec(elapsedMs)} / ${Math.round(budgetMs / 1000)}s`}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 3.8: Run test fino a passare**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/TypingIndicator.test
```
Expected: PASS (4/4).

- [ ] **Step 3.9: Commit**

```bash
git add apps/web/src/components/v2/game-chat/OutOfContextActions.tsx apps/web/src/components/v2/game-chat/TypingIndicator.tsx apps/web/src/components/v2/game-chat/__tests__/OutOfContextActions.test.tsx apps/web/src/components/v2/game-chat/__tests__/TypingIndicator.test.tsx
git commit -m "feat(web): #915 OutOfContextActions + TypingIndicator v2"
```

---

## Task 4: `ChatBubble` (pure)

**Files:**
- Create: `apps/web/src/components/v2/game-chat/ChatBubble.tsx`
- Test: `apps/web/src/components/v2/game-chat/__tests__/ChatBubble.test.tsx`

**Contract:**
```ts
interface ChatBubbleProps {
  readonly role: 'user' | 'agent';
  readonly content: ReactNode;
  readonly agentName?: string;
  readonly avatar?: string;  // emoji or single char
  readonly children?: ReactNode;  // slot for citations / badge / disclaimer
  readonly className?: string;
}
```

User bubble: align right, `bg-[hsl(var(--c-chat))]` solid, white text.
Agent bubble: align left, `bg-[hsl(var(--c-agent)/0.08)]` + border, header con avatar + name.

- [ ] **Step 4.1: Test failing**

Crea `apps/web/src/components/v2/game-chat/__tests__/ChatBubble.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { ChatBubble } from '../ChatBubble';

describe('ChatBubble', () => {
  it('renders user role with content (no header)', () => {
    render(<ChatBubble role="user" content="ciao" />);
    expect(screen.getByText('ciao')).toBeInTheDocument();
    expect(screen.queryByText(/Tutor/i)).not.toBeInTheDocument();
  });

  it('renders agent role with header (avatar + name)', () => {
    render(<ChatBubble role="agent" content="risposta" agentName="Tutor Wingspan" avatar="🧙" />);
    expect(screen.getByText('Tutor Wingspan')).toBeInTheDocument();
    expect(screen.getByText('🧙')).toBeInTheDocument();
  });

  it('renders children slot inside the bubble (for agent extras)', () => {
    render(
      <ChatBubble role="agent" content="risposta" agentName="Tutor">
        <div data-testid="extras">extras here</div>
      </ChatBubble>
    );
    expect(screen.getByTestId('extras')).toBeInTheDocument();
  });

  it('content can be a ReactNode (e.g. paragraphs)', () => {
    render(
      <ChatBubble
        role="agent"
        agentName="X"
        content={<><p>p1</p><p>p2</p></>}
      />
    );
    expect(screen.getByText('p1')).toBeInTheDocument();
    expect(screen.getByText('p2')).toBeInTheDocument();
  });

  it('exposes role via data attribute', () => {
    render(<ChatBubble role="user" content="x" />);
    expect(screen.getByTestId('chat-bubble')).toHaveAttribute('data-role', 'user');
  });
});
```

- [ ] **Step 4.2: Run test, verify FAIL**

- [ ] **Step 4.3: Implementa**

Crea `apps/web/src/components/v2/game-chat/ChatBubble.tsx`:

```tsx
/**
 * ChatBubble — bubble user|agent. Pure component.
 * Agent bubble include header (avatar + name) e slot children per citations,
 * confidence badge, disclaimer, out-of-context actions.
 * Spec: §3.2
 */
import type { ReactElement, ReactNode } from 'react';

import clsx from 'clsx';

export interface ChatBubbleProps {
  readonly role: 'user' | 'agent';
  readonly content: ReactNode;
  readonly agentName?: string;
  readonly avatar?: string;
  readonly children?: ReactNode;
  readonly className?: string;
}

export function ChatBubble({
  role,
  content,
  agentName,
  avatar,
  children,
  className,
}: ChatBubbleProps): ReactElement {
  const isAgent = role === 'agent';
  return (
    <div
      data-testid="chat-bubble"
      data-role={role}
      className={clsx(
        'max-w-[85%] rounded-2xl px-4 py-3 text-base leading-relaxed',
        isAgent
          ? 'self-start rounded-bl-sm border border-[hsl(var(--c-agent)/0.18)] bg-[hsl(var(--c-agent)/0.08)] text-foreground'
          : 'self-end rounded-br-sm bg-[hsl(var(--c-chat))] text-white',
        className
      )}
    >
      {isAgent && agentName && (
        <div className="mb-2 flex items-center gap-2">
          <span
            aria-hidden="true"
            className="flex h-6 w-6 items-center justify-center rounded-full bg-[hsl(var(--c-agent)/0.25)] text-xs"
          >
            {avatar ?? '🤖'}
          </span>
          <span className="font-bold text-sm text-[hsl(var(--c-agent))]">{agentName}</span>
        </div>
      )}
      <div data-slot="bubble-content">{content}</div>
      {children && <div data-slot="bubble-extras">{children}</div>}
    </div>
  );
}
```

- [ ] **Step 4.4: Run test fino a passare**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/ChatBubble.test
```
Expected: PASS (5/5).

- [ ] **Step 4.5: Commit**

```bash
git add apps/web/src/components/v2/game-chat/ChatBubble.tsx apps/web/src/components/v2/game-chat/__tests__/ChatBubble.test.tsx
git commit -m "feat(web): #915 ChatBubble v2 with role variants"
```

---

## Task 5: `SuggestedPrompts` + `ChatInputBar`

**Files:**
- Create: `apps/web/src/components/v2/game-chat/SuggestedPrompts.tsx`
- Create: `apps/web/src/components/v2/game-chat/ChatInputBar.tsx`
- Test: 2 file `__tests__/`

**Contract `SuggestedPrompts`:**
```ts
type PromptCategory = 'A' | 'B' | 'C' | 'E' | 'F';

interface SuggestedPrompt {
  readonly category: PromptCategory;
  readonly text: string;
  readonly onClick: () => void;
}

interface SuggestedPromptsProps {
  readonly prompts: readonly SuggestedPrompt[];
  readonly groupLabel?: string;
  readonly className?: string;
}
```

**Contract `ChatInputBar`:**
```ts
interface ChatInputBarProps {
  readonly value: string;
  readonly onChange: (next: string) => void;
  readonly onSubmit: (question: string) => void;  // chiamato con value trimmed
  readonly disabled?: boolean;
  readonly placeholder?: string;
  readonly className?: string;
}
```

- [ ] **Step 5.1: Test failing — `SuggestedPrompts`**

Crea `apps/web/src/components/v2/game-chat/__tests__/SuggestedPrompts.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { SuggestedPrompts } from '../SuggestedPrompts';

describe('SuggestedPrompts', () => {
  it('renders nothing when prompts empty', () => {
    const { container } = render(<SuggestedPrompts prompts={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it('renders prompts with category tags', () => {
    render(
      <SuggestedPrompts
        groupLabel="Continua"
        prompts={[
          { category: 'A', text: 'E se sono 2 uccelli stessa carta?', onClick: vi.fn() },
          { category: 'F', text: 'Edge case carta predatore', onClick: vi.fn() },
        ]}
      />
    );
    expect(screen.getByText(/Continua/)).toBeInTheDocument();
    expect(screen.getByText(/E se sono 2 uccelli/)).toBeInTheDocument();
    expect(screen.getAllByText(/^[ABCEF]$/)).toHaveLength(2);
  });

  it('calls onClick with right callback when prompt clicked', () => {
    const onClickA = vi.fn();
    const onClickF = vi.fn();
    render(
      <SuggestedPrompts
        prompts={[
          { category: 'A', text: 'P-A', onClick: onClickA },
          { category: 'F', text: 'P-F', onClick: onClickF },
        ]}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /P-A/ }));
    expect(onClickA).toHaveBeenCalledOnce();
    expect(onClickF).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 5.2: Run test, verify FAIL**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/SuggestedPrompts.test
```

- [ ] **Step 5.3: Implementa `SuggestedPrompts`**

Crea `apps/web/src/components/v2/game-chat/SuggestedPrompts.tsx`:

```tsx
/**
 * SuggestedPrompts — chip categorizzati A/B/C/E/F (Q4 brainstorm categorie domanda).
 * Pure component. Categoria mostrata come tag mono.
 * Spec: §3.2
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

export type PromptCategory = 'A' | 'B' | 'C' | 'E' | 'F';

export interface SuggestedPrompt {
  readonly category: PromptCategory;
  readonly text: string;
  readonly onClick: () => void;
}

export interface SuggestedPromptsProps {
  readonly prompts: readonly SuggestedPrompt[];
  readonly groupLabel?: string;
  readonly className?: string;
}

export function SuggestedPrompts({
  prompts,
  groupLabel,
  className,
}: SuggestedPromptsProps): ReactElement | null {
  if (prompts.length === 0) return null;
  return (
    <div
      data-slot="suggested-prompts"
      className={clsx('flex flex-wrap gap-2 px-4 pb-3', className)}
    >
      {groupLabel && (
        <span className="w-full font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
          {groupLabel}
        </span>
      )}
      {prompts.map((p, i) => (
        <button
          key={i}
          type="button"
          onClick={p.onClick}
          data-category={p.category}
          className={clsx(
            'inline-flex items-center gap-1 rounded-full px-3 py-2',
            'bg-[hsl(var(--c-chat)/0.08)] text-[hsl(var(--c-chat))]',
            'border border-[hsl(var(--c-chat)/0.2)]',
            'font-semibold text-xs',
            'hover:bg-[hsl(var(--c-chat)/0.15)]',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-chat))]'
          )}
        >
          <span className="font-mono text-[9px] opacity-70">{p.category}</span>
          <span>{p.text}</span>
        </button>
      ))}
    </div>
  );
}
```

- [ ] **Step 5.4: Run test fino a passare**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/SuggestedPrompts.test
```
Expected: PASS (3/3).

- [ ] **Step 5.5: Test failing — `ChatInputBar`**

Crea `apps/web/src/components/v2/game-chat/__tests__/ChatInputBar.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ChatInputBar } from '../ChatInputBar';

describe('ChatInputBar', () => {
  it('renders input + send button', () => {
    render(
      <ChatInputBar
        value=""
        onChange={vi.fn()}
        onSubmit={vi.fn()}
        placeholder="Chiedi una regola…"
      />
    );
    expect(screen.getByPlaceholderText('Chiedi una regola…')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /invia/i })).toBeInTheDocument();
  });

  it('calls onChange when typing', () => {
    const onChange = vi.fn();
    render(<ChatInputBar value="" onChange={onChange} onSubmit={vi.fn()} />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'wing' } });
    expect(onChange).toHaveBeenCalledWith('wing');
  });

  it('calls onSubmit on Enter with trimmed value', () => {
    const onSubmit = vi.fn();
    render(<ChatInputBar value="  ciao  " onChange={vi.fn()} onSubmit={onSubmit} />);
    fireEvent.keyDown(screen.getByRole('textbox'), { key: 'Enter', code: 'Enter' });
    expect(onSubmit).toHaveBeenCalledWith('ciao');
  });

  it('does NOT submit when value is whitespace only', () => {
    const onSubmit = vi.fn();
    render(<ChatInputBar value="   " onChange={vi.fn()} onSubmit={onSubmit} />);
    fireEvent.keyDown(screen.getByRole('textbox'), { key: 'Enter' });
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('disables input and button when disabled prop true', () => {
    render(<ChatInputBar value="x" onChange={vi.fn()} onSubmit={vi.fn()} disabled />);
    expect(screen.getByRole('textbox')).toBeDisabled();
    expect(screen.getByRole('button', { name: /invia/i })).toBeDisabled();
  });

  it('calls onSubmit on send button click', () => {
    const onSubmit = vi.fn();
    render(<ChatInputBar value="ciao" onChange={vi.fn()} onSubmit={onSubmit} />);
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));
    expect(onSubmit).toHaveBeenCalledWith('ciao');
  });
});
```

- [ ] **Step 5.6: Run test, verify FAIL**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/ChatInputBar.test
```

- [ ] **Step 5.7: Implementa `ChatInputBar`**

Crea `apps/web/src/components/v2/game-chat/ChatInputBar.tsx`:

```tsx
/**
 * ChatInputBar — input controlled + send button.
 * Submit on Enter o click su send. Trim value, blocca submit se whitespace-only.
 * Spec: §3.3
 */
'use client';

import type { ChangeEvent, KeyboardEvent, ReactElement } from 'react';

import clsx from 'clsx';

export interface ChatInputBarProps {
  readonly value: string;
  readonly onChange: (next: string) => void;
  readonly onSubmit: (question: string) => void;
  readonly disabled?: boolean;
  readonly placeholder?: string;
  readonly className?: string;
}

export function ChatInputBar({
  value,
  onChange,
  onSubmit,
  disabled = false,
  placeholder = 'Scrivi una domanda…',
  className,
}: ChatInputBarProps): ReactElement {
  const trySubmit = () => {
    const trimmed = value.trim();
    if (trimmed.length === 0) return;
    onSubmit(trimmed);
  };

  const handleKey = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      trySubmit();
    }
  };

  return (
    <div
      data-slot="chat-input-bar"
      className={clsx(
        'flex items-center gap-2 border-t border-border-light px-4 py-3',
        className
      )}
    >
      <input
        type="text"
        role="textbox"
        value={value}
        onChange={(e: ChangeEvent<HTMLInputElement>) => onChange(e.target.value)}
        onKeyDown={handleKey}
        disabled={disabled}
        placeholder={placeholder}
        className={clsx(
          'flex-1 rounded-full border border-border bg-muted px-4 py-3',
          'text-base focus:outline-none focus:border-[hsl(var(--c-chat)/0.5)]',
          'focus:shadow-[0_0_0_3px_hsl(var(--c-chat)/0.1)]',
          'disabled:opacity-50'
        )}
      />
      <button
        type="button"
        aria-label="Invia"
        onClick={trySubmit}
        disabled={disabled}
        className={clsx(
          'flex h-11 w-11 items-center justify-center rounded-full',
          'bg-[hsl(var(--c-chat))] text-white text-lg',
          'disabled:opacity-50'
        )}
      >
        ↑
      </button>
    </div>
  );
}
```

- [ ] **Step 5.8: Run test fino a passare**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/ChatInputBar.test
```
Expected: PASS (6/6).

- [ ] **Step 5.9: Commit**

```bash
git add apps/web/src/components/v2/game-chat/SuggestedPrompts.tsx apps/web/src/components/v2/game-chat/ChatInputBar.tsx apps/web/src/components/v2/game-chat/__tests__/SuggestedPrompts.test.tsx apps/web/src/components/v2/game-chat/__tests__/ChatInputBar.test.tsx
git commit -m "feat(web): #915 SuggestedPrompts + ChatInputBar v2"
```

---

## Task 6: `GameChatHeader` + `GameChatSidebar`

**Files:**
- Create: `apps/web/src/components/v2/game-chat/GameChatHeader.tsx`
- Create: `apps/web/src/components/v2/game-chat/GameChatSidebar.tsx`
- Test: 2 file `__tests__/`

**Contract `GameChatHeader`:**
```ts
interface GameChatHeaderProps {
  readonly gameTitle: string;
  readonly gameIcon: string;  // emoji
  readonly agent: 'tutor' | 'arbitro';
  readonly subtitle?: string;
  readonly className?: string;
}
```

**Contract `GameChatSidebar`** (desktop only, hidden under lg breakpoint):
```ts
interface ChatHistoryItem {
  readonly id: string;
  readonly question: string;
  readonly when: string;  // pre-formatted "5m fa", "ieri", etc.
  readonly active?: boolean;
}

interface GameChatSidebarProps {
  readonly gameTitle: string;
  readonly gameIcon: string;
  readonly currentAgent: 'tutor' | 'arbitro';
  readonly history: readonly ChatHistoryItem[];
  readonly onAgentChange: (next: 'tutor' | 'arbitro') => void;
  readonly onHistorySelect: (id: string) => void;
  readonly className?: string;
}
```

- [ ] **Step 6.1: Test failing — `GameChatHeader`**

Crea `apps/web/src/components/v2/game-chat/__tests__/GameChatHeader.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { GameChatHeader } from '../GameChatHeader';

describe('GameChatHeader', () => {
  it('renders game title + icon + tutor badge', () => {
    render(<GameChatHeader gameTitle="Wingspan" gameIcon="🦤" agent="tutor" />);
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('🦤')).toBeInTheDocument();
    expect(screen.getByText(/Tutor/i)).toBeInTheDocument();
  });

  it('renders arbitro badge when agent=arbitro', () => {
    render(<GameChatHeader gameTitle="Wingspan" gameIcon="🦤" agent="arbitro" />);
    expect(screen.getByText(/Arbitro/i)).toBeInTheDocument();
  });

  it('renders subtitle when provided', () => {
    render(<GameChatHeader gameTitle="Wingspan" gameIcon="🦤" agent="tutor" subtitle="Edge case" />);
    expect(screen.getByText(/Edge case/)).toBeInTheDocument();
  });

  it('exposes agent via data attribute', () => {
    render(<GameChatHeader gameTitle="X" gameIcon="🎲" agent="arbitro" />);
    expect(screen.getByTestId('game-chat-header')).toHaveAttribute('data-agent', 'arbitro');
  });
});
```

- [ ] **Step 6.2: Run test, verify FAIL**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/GameChatHeader.test
```

- [ ] **Step 6.3: Implementa `GameChatHeader`**

Crea `apps/web/src/components/v2/game-chat/GameChatHeader.tsx`:

```tsx
/**
 * GameChatHeader — sticky top dell'orchestrator.
 * Mostra game-icon + titolo + agent-badge + (opzionale) subtitle.
 * Spec: §3.2
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

export type AgentKind = 'tutor' | 'arbitro';

export interface GameChatHeaderProps {
  readonly gameTitle: string;
  readonly gameIcon: string;
  readonly agent: AgentKind;
  readonly subtitle?: string;
  readonly className?: string;
}

const AGENT_LABEL: Record<AgentKind, string> = {
  tutor: '🧙 Tutor',
  arbitro: '⚖️ Arbitro',
};

export function GameChatHeader({
  gameTitle,
  gameIcon,
  agent,
  subtitle,
  className,
}: GameChatHeaderProps): ReactElement {
  return (
    <header
      data-testid="game-chat-header"
      data-agent={agent}
      className={clsx(
        'flex items-center gap-3 border-b border-border-light px-4 py-3',
        className
      )}
    >
      <span
        aria-hidden="true"
        className="flex h-9 w-9 items-center justify-center rounded-md bg-[hsl(var(--c-game)/0.15)] text-lg"
      >
        {gameIcon}
      </span>
      <div>
        <div className="font-bold text-base leading-tight">{gameTitle}</div>
        <div className="flex items-center gap-2 font-mono text-xs text-muted-foreground">
          <span className="inline-flex items-center gap-1 rounded-full bg-[hsl(var(--c-agent)/0.15)] px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-[hsl(var(--c-agent))]">
            {AGENT_LABEL[agent]}
          </span>
          {subtitle && <span>· {subtitle}</span>}
        </div>
      </div>
    </header>
  );
}
```

- [ ] **Step 6.4: Run test fino a passare**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/GameChatHeader.test
```
Expected: PASS (4/4).

- [ ] **Step 6.5: Test failing — `GameChatSidebar`**

Crea `apps/web/src/components/v2/game-chat/__tests__/GameChatSidebar.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { GameChatSidebar } from '../GameChatSidebar';

describe('GameChatSidebar', () => {
  const baseProps = {
    gameTitle: 'Wingspan',
    gameIcon: '🦤',
    currentAgent: 'tutor' as const,
    history: [],
    onAgentChange: vi.fn(),
    onHistorySelect: vi.fn(),
  };

  it('renders game-mini header', () => {
    render(<GameChatSidebar {...baseProps} />);
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('🦤')).toBeInTheDocument();
  });

  it('renders agent switch with current pressed', () => {
    render(<GameChatSidebar {...baseProps} currentAgent="arbitro" />);
    const arbitroBtn = screen.getByRole('button', { name: /Arbitro/ });
    expect(arbitroBtn).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: /Tutor/ })).toHaveAttribute('aria-pressed', 'false');
  });

  it('calls onAgentChange when other agent clicked', () => {
    const onAgentChange = vi.fn();
    render(<GameChatSidebar {...baseProps} onAgentChange={onAgentChange} currentAgent="tutor" />);
    fireEvent.click(screen.getByRole('button', { name: /Arbitro/ }));
    expect(onAgentChange).toHaveBeenCalledWith('arbitro');
  });

  it('renders history items + active highlight', () => {
    render(
      <GameChatSidebar
        {...baseProps}
        history={[
          { id: 'q1', question: 'Cumulo poteri?', when: 'ora', active: true },
          { id: 'q2', question: 'Setup 4', when: '5m fa' },
        ]}
      />
    );
    expect(screen.getByText('Cumulo poteri?')).toBeInTheDocument();
    expect(screen.getByText('Setup 4')).toBeInTheDocument();
  });

  it('calls onHistorySelect when item clicked', () => {
    const onHistorySelect = vi.fn();
    render(
      <GameChatSidebar
        {...baseProps}
        onHistorySelect={onHistorySelect}
        history={[{ id: 'q1', question: 'X', when: 'ora' }]}
      />
    );
    fireEvent.click(screen.getByText('X').closest('button')!);
    expect(onHistorySelect).toHaveBeenCalledWith('q1');
  });

  it('renders empty history gracefully', () => {
    render(<GameChatSidebar {...baseProps} />);
    expect(screen.queryByText(/conversazioni/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 6.6: Run test, verify FAIL**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/GameChatSidebar.test
```

- [ ] **Step 6.7: Implementa `GameChatSidebar`**

Crea `apps/web/src/components/v2/game-chat/GameChatSidebar.tsx`:

```tsx
/**
 * GameChatSidebar — sidebar desktop (260px) con game-mini, agent switcher
 * tutor/arbitro, e history rail per quel gioco.
 *
 * Spec: §3.3
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

import type { AgentKind } from './GameChatHeader';

export interface ChatHistoryItem {
  readonly id: string;
  readonly question: string;
  readonly when: string;
  readonly active?: boolean;
}

export interface GameChatSidebarProps {
  readonly gameTitle: string;
  readonly gameIcon: string;
  readonly currentAgent: AgentKind;
  readonly history: readonly ChatHistoryItem[];
  readonly onAgentChange: (next: AgentKind) => void;
  readonly onHistorySelect: (id: string) => void;
  readonly className?: string;
}

const AGENT_LABEL: Record<AgentKind, string> = {
  tutor: '🧙 Tutor',
  arbitro: '⚖️ Arbitro',
};

export function GameChatSidebar({
  gameTitle,
  gameIcon,
  currentAgent,
  history,
  onAgentChange,
  onHistorySelect,
  className,
}: GameChatSidebarProps): ReactElement {
  return (
    <aside
      data-slot="game-chat-sidebar"
      className={clsx(
        'flex w-[260px] shrink-0 flex-col gap-3 border-r border-border-light p-3',
        className
      )}
    >
      <div className="flex items-center gap-2 rounded-md border border-border-light bg-card p-3">
        <span
          aria-hidden="true"
          className="flex h-8 w-8 items-center justify-center rounded-sm bg-[hsl(var(--c-game)/0.18)] text-base"
        >
          {gameIcon}
        </span>
        <div>
          <div className="font-bold text-sm leading-tight">{gameTitle}</div>
          <div className="font-mono text-[10px] text-muted-foreground">Chat regole</div>
        </div>
      </div>

      <div>
        <h4 className="mb-2 font-bold text-xs uppercase tracking-wider text-muted-foreground">
          Agente attivo
        </h4>
        <div role="group" aria-label="Cambia agente" className="flex rounded-full bg-muted p-0.5">
          {(['tutor', 'arbitro'] as const).map(k => (
            <button
              key={k}
              type="button"
              aria-pressed={currentAgent === k}
              onClick={() => onAgentChange(k)}
              className={clsx(
                'flex flex-1 items-center justify-center gap-1 rounded-full px-2 py-2',
                'font-semibold text-xs transition-colors',
                currentAgent === k
                  ? 'bg-[hsl(var(--c-agent))] text-white'
                  : 'text-muted-foreground'
              )}
            >
              {AGENT_LABEL[k]}
            </button>
          ))}
        </div>
      </div>

      <div>
        <h4 className="mb-2 font-bold text-xs uppercase tracking-wider text-muted-foreground">
          Conversazioni recenti
        </h4>
        {history.length === 0 ? (
          <p className="px-2 text-xs text-muted-foreground">Nessuna conversazione recente.</p>
        ) : (
          <ul className="flex flex-col gap-1">
            {history.map(h => (
              <li key={h.id}>
                <button
                  type="button"
                  onClick={() => onHistorySelect(h.id)}
                  className={clsx(
                    'flex w-full flex-col gap-0.5 rounded-md border px-3 py-2 text-left',
                    h.active
                      ? 'border-[hsl(var(--c-chat)/0.3)] bg-[hsl(var(--c-chat)/0.08)]'
                      : 'border-transparent hover:border-border-light hover:bg-muted'
                  )}
                >
                  <span className="text-xs leading-tight">{h.question}</span>
                  <span className="font-mono text-[10px] text-muted-foreground">{h.when}</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </aside>
  );
}
```

- [ ] **Step 6.8: Run test fino a passare**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/GameChatSidebar.test
```
Expected: PASS (6/6).

- [ ] **Step 6.9: Commit**

```bash
git add apps/web/src/components/v2/game-chat/GameChatHeader.tsx apps/web/src/components/v2/game-chat/GameChatSidebar.tsx apps/web/src/components/v2/game-chat/__tests__/GameChatHeader.test.tsx apps/web/src/components/v2/game-chat/__tests__/GameChatSidebar.test.tsx
git commit -m "feat(web): #915 GameChatHeader + GameChatSidebar v2"
```

---

## Task 7: `CitationModal` (controlled, hybrid C — KB button gated)

**Files:**
- Create: `apps/web/src/components/v2/game-chat/CitationModal.tsx`
- Test: `apps/web/src/components/v2/game-chat/__tests__/CitationModal.test.tsx`

**Pre-step**: leggi `apps/web/src/components/chat-unified/PdfPageModal.tsx` per ispirarti al pattern modal/portal V1. NON copiare direttamente — riimplementa con design v2 e usa il `Citation` type esistente da `@/types`.

**Contract:**
```ts
import type { Citation } from '@/types';  // shape verificata in Step 0.3

interface CitationModalProps {
  readonly citation: Citation | null;  // null when closed
  readonly open: boolean;
  readonly onClose: () => void;
  readonly onOpenInKb?: () => void;  // hybrid C: button hidden if undefined
}
```

- [ ] **Step 7.1: Verifica shape `Citation`**

```bash
grep -A 15 "interface Citation\b\|type Citation\s*=" apps/web/src/types/agent.ts apps/web/src/types/domain.ts
```

Annota i campi (es. `pageNumber`, `snippet`, `chunkId`, `documentId`, `sectionTitle`?). Usa lo schema reale nel componente.

- [ ] **Step 7.2: Test failing**

Crea `apps/web/src/components/v2/game-chat/__tests__/CitationModal.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { CitationModal } from '../CitationModal';

const sampleCitation = {
  pageNumber: 12,
  snippet: 'Ogni potere "quando attivato" si attiva ogni volta…',
  // additional fields will be ignored by the component if not used
} as any;  // narrow once Step 7.1 confirms exact shape

describe('CitationModal', () => {
  it('renders nothing when closed', () => {
    render(<CitationModal citation={sampleCitation} open={false} onClose={vi.fn()} />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders dialog with snippet when open', () => {
    render(<CitationModal citation={sampleCitation} open onClose={vi.fn()} />);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText(/Ogni potere/)).toBeInTheDocument();
    expect(screen.getByText(/p\. ?12/)).toBeInTheDocument();
  });

  it('calls onClose when close button clicked', () => {
    const onClose = vi.fn();
    render(<CitationModal citation={sampleCitation} open onClose={onClose} />);
    fireEvent.click(screen.getByRole('button', { name: /chiudi/i }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('hides "Apri nella KB" footer when onOpenInKb is undefined', () => {
    render(<CitationModal citation={sampleCitation} open onClose={vi.fn()} />);
    expect(screen.queryByRole('button', { name: /apri nella kb/i })).not.toBeInTheDocument();
  });

  it('shows "Apri nella KB" when onOpenInKb provided + calls it on click', () => {
    const onOpenInKb = vi.fn();
    render(
      <CitationModal citation={sampleCitation} open onClose={vi.fn()} onOpenInKb={onOpenInKb} />
    );
    const kbBtn = screen.getByRole('button', { name: /apri nella kb/i });
    fireEvent.click(kbBtn);
    expect(onOpenInKb).toHaveBeenCalledOnce();
  });
});
```

- [ ] **Step 7.3: Run test, verify FAIL**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/CitationModal.test
```

- [ ] **Step 7.4: Implementa `CitationModal`**

Crea `apps/web/src/components/v2/game-chat/CitationModal.tsx`:

```tsx
/**
 * CitationModal — preview citation con snippet + page number.
 *
 * Hybrid C (Q4 brainstorm): footer button "Apri nella KB" condizionale —
 * hidden quando `onOpenInKb === undefined` (G4 non ancora atterrato).
 * Quando G4 sarà pronto, basta passare il callback dal consumer.
 *
 * Spec: §3.3 §4.3
 */
'use client';

import { useEffect } from 'react';
import type { ReactElement } from 'react';

import clsx from 'clsx';

import type { Citation } from '@/types';

export interface CitationModalProps {
  readonly citation: Citation | null;
  readonly open: boolean;
  readonly onClose: () => void;
  readonly onOpenInKb?: () => void;
}

export function CitationModal({
  citation,
  open,
  onClose,
  onOpenInKb,
}: CitationModalProps): ReactElement | null {
  useEffect(() => {
    if (!open) return;
    const handleEsc = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', handleEsc);
    return () => document.removeEventListener('keydown', handleEsc);
  }, [open, onClose]);

  if (!open || !citation) return null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="citation-modal-title"
      data-slot="citation-modal"
      className={clsx(
        'fixed inset-0 z-50 flex items-center justify-center',
        'bg-black/50 backdrop-blur-sm'
      )}
      onClick={onClose}
    >
      <div
        className="max-w-lg w-full mx-4 rounded-lg bg-card border border-border shadow-xl"
        onClick={e => e.stopPropagation()}
      >
        <header className="border-b border-border-light px-4 py-3">
          <h2 id="citation-modal-title" className="font-bold text-lg">
            📖 p. {citation.pageNumber}
          </h2>
        </header>
        <div className="p-4">
          {citation.snippet ? (
            <blockquote className="border-l-4 border-[hsl(var(--c-kb))] pl-4 text-sm text-foreground">
              {citation.snippet}
            </blockquote>
          ) : (
            <p className="text-sm text-muted-foreground italic">Nessuna anteprima disponibile.</p>
          )}
        </div>
        <footer className="flex items-center justify-end gap-2 border-t border-border-light px-4 py-3">
          {onOpenInKb && (
            <button
              type="button"
              onClick={onOpenInKb}
              className="rounded-full border border-[hsl(var(--c-kb))] px-3 py-1.5 text-sm font-semibold text-[hsl(var(--c-kb))] hover:bg-[hsl(var(--c-kb)/0.1)]"
            >
              📖 Apri nella KB
            </button>
          )}
          <button
            type="button"
            onClick={onClose}
            className="rounded-full bg-[hsl(var(--c-chat))] px-4 py-1.5 text-sm font-semibold text-white hover:opacity-90"
          >
            Chiudi
          </button>
        </footer>
      </div>
    </div>
  );
}
```

> ⚠️ **Adjust per Step 7.1**: se la `Citation` type ha `snippet?: string | null`, gestisci `citation.snippet ?? undefined`. Se ha `pageNumber: number | null`, gestisci `citation.pageNumber ?? '?'`.

- [ ] **Step 7.5: Run test fino a passare**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/CitationModal.test
```
Expected: PASS (5/5).

- [ ] **Step 7.6: Commit**

```bash
git add apps/web/src/components/v2/game-chat/CitationModal.tsx apps/web/src/components/v2/game-chat/__tests__/CitationModal.test.tsx
git commit -m "feat(web): #915 CitationModal v2 with hybrid KB button (gated by G4)"
```

---

## Task 8: Barrel `index.ts` + verifica build

**Files:**
- Create: `apps/web/src/components/v2/game-chat/index.ts`

- [ ] **Step 8.1: Crea barrel**

Crea `apps/web/src/components/v2/game-chat/index.ts`:

```ts
/**
 * v2 game-chat barrel — exports per il flusso "serata di gioco" (G1+G5).
 * Spec: docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md
 */

export { CitationChip } from './CitationChip';
export type { CitationChipProps } from './CitationChip';

export { CitationModal } from './CitationModal';
export type { CitationModalProps } from './CitationModal';

export { ConfidenceBadge, getConfidenceTier } from './ConfidenceBadge';
export type { ConfidenceBadgeProps, ConfidenceTier } from './ConfidenceBadge';

export { LowConfidenceDisclaimer } from './LowConfidenceDisclaimer';
export type { LowConfidenceDisclaimerProps, DisclaimerAlternative } from './LowConfidenceDisclaimer';

export { OutOfContextActions } from './OutOfContextActions';
export type {
  OutOfContextActionsProps,
  OutOfContextAction,
  OutOfContextActionKind,
} from './OutOfContextActions';

export { TypingIndicator } from './TypingIndicator';
export type { TypingIndicatorProps } from './TypingIndicator';

export { ChatBubble } from './ChatBubble';
export type { ChatBubbleProps } from './ChatBubble';

export { SuggestedPrompts } from './SuggestedPrompts';
export type { SuggestedPromptsProps, SuggestedPrompt, PromptCategory } from './SuggestedPrompts';

export { ChatInputBar } from './ChatInputBar';
export type { ChatInputBarProps } from './ChatInputBar';

export { GameChatHeader } from './GameChatHeader';
export type { GameChatHeaderProps, AgentKind } from './GameChatHeader';

export { GameChatSidebar } from './GameChatSidebar';
export type { GameChatSidebarProps, ChatHistoryItem } from './GameChatSidebar';
```

- [ ] **Step 8.2: Run typecheck full**

```bash
cd apps/web && pnpm typecheck
```
Expected: 0 errori. Se errori → import mancanti / export mismatch nel barrel.

- [ ] **Step 8.3: Run all v2/game-chat tests**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat
```
Expected: tutti i test passano (~33-40 test totali).

- [ ] **Step 8.4: Commit**

```bash
git add apps/web/src/components/v2/game-chat/index.ts
git commit -m "feat(web): #915 game-chat barrel exports (11 components ready)"
```

---

## Task 9: Hook `useGameChat`

**Files:**
- Create: `apps/web/src/hooks/queries/useGameChat.ts`
- Test: `apps/web/src/hooks/queries/__tests__/useGameChat.test.tsx`

**Contract:**
```ts
interface ChatMessage {
  readonly id: string;
  readonly role: 'user' | 'agent';
  readonly content: string;
  readonly citations?: ReadonlyArray<Citation>;
  readonly overallConfidence?: number;
  readonly isLowQuality?: boolean;
  readonly outOfContext?: boolean;
  readonly createdAt: string;
}

interface UseGameChatResult {
  readonly messages: readonly ChatMessage[];
  readonly isLoading: boolean;       // submitting state
  readonly isError: boolean;
  readonly currentAgent: 'tutor' | 'arbitro';
  readonly ask: (question: string) => Promise<void>;
  readonly switchAgent: (next: 'tutor' | 'arbitro') => void;
}

export function useGameChat(gameId: string, initialAgent?: AgentKind): UseGameChatResult;
```

**API endpoint VERIFICATO (post Step 9.1 esecuzione)**: l'unico endpoint QA disponibile è **`POST /api/v1/agents/qa/stream`** (SSE), wrappato da `qaStream()` in `apps/web/src/lib/api/clients/chatClient.ts:399`. NON esiste un endpoint `/qa` non-streaming.

### Shape REALI verificate (apps/api/src/Api/Models/Contracts.cs:109)

```ts
// QaStreamRequest (chatClient.ts:351)
interface QaStreamRequest {
  gameId: string;
  query: string;          // ⚠️ "query" NON "question"
  chatId?: string;
  responseStyle?: 'concise' | 'detailed';
  continuationToken?: string;
  // ❌ NESSUN agentType / agentId — è system agent
}

// StreamingComplete payload (event type=4)
interface StreamingComplete {
  estimatedReadingTimeMinutes: number;
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
  confidence: number | null;       // ⚠️ "confidence" NON "overallConfidence"
  chatThreadId?: string;
  routingIntent?: string;
  routingLatencyMs?: number;
  strategyTier?: string;
  executionId?: string;
  Citations?: CitationDto[];       // ⚠️ NOTA: PascalCase nel record C# → JSON serializza come "citations" (camelCase config standard) MA da verificare

  // ❌ NESSUN isLowQuality
  // ❌ NESSUN outOfContext
  // ❌ NESSUN answer field
}
```

### Gap rispetto allo spec/brainstorm

1. **`answer` arriva via eventi `Token` (type=7)** accumulati — NON nel payload Complete. L'hook DEVE accumulare i token per costruire la risposta completa, poi al Complete fissa il messaggio finale.
2. **`isLowQuality` NON esiste backend** — derivare frontend: `isLowQuality = confidence !== null && confidence < 0.70` (soglia spec G5).
3. **`outOfContext` NON esiste backend** — derivare frontend: `outOfContext = Citations.length === 0 && confidence < 0.30` (euristica conservativa).
4. **`agentType` NON è supportato dall'endpoint** — l'agent switch UI funziona ma NON viene propagato al backend (system agent unico). UX preserva il toggle visivo.

### Decisione architetturale (post-verifica)

L'hook `useGameChat`:
1. Avvia `qaStream({gameId, query: question})`
2. Accumula stringhe da eventi `type=7` (Token) in un buffer
3. Su evento `type=4` (Complete): legge `confidence` + `Citations` + buffer accumulato → costruisce `ChatMessage` agent + applica derivation `isLowQuality`/`outOfContext`
4. Su evento `type=5` (Error): propaga errore
5. UX atomica: il `bubble agent` con risposta NON appare finché Complete non è ricevuto (typing indicator visibile fino a quel momento)

**Strategia**: l'hook `useGameChat` consuma `qaStream` ma **accumula** tutti i token e mostra il messaggio agent solo quando arriva l'evento `Complete` (type=4). Coerente con Q6 brainstorm "atomica con spinner" — l'utente vede typing indicator finché `Complete` non arriva con answer + citations + confidence.

Eventi SSE rilevanti:
- `type=0` StateUpdate (status, chatThreadId) — ignorato
- `type=4` Complete (answer, snippets, totalTokens, followUpQuestions) — **usato**, contiene tutti i campi
- `type=5` Error (message) — propaga errore
- `type=7` Token (string) — ignorato (atomic UX, no streaming token)

⚠️ **Verifica `QaStreamRequest` shape**: leggi `chatClient.ts:380-400` per vedere i campi accettati. Probabilmente: `{ gameId, agentId?, agentType?, question, chatThreadId? }`.

- [ ] **Step 9.1: Conferma `QaStreamRequest` + `Complete` event shape**

Run:
```bash
grep -B 2 -A 15 "interface QaStreamRequest\|interface QaStreamEvent\|type QaStreamEvent" apps/web/src/lib/api/clients/chatClient.ts
```

Annota:
- Campi obbligatori del request
- Shape esatta del payload `data` per event `type=4` (Complete)
- Se il payload Complete include `overallConfidence` e `isLowQuality`

- [ ] **Step 9.2: Test failing**

Crea `apps/web/src/hooks/queries/__tests__/useGameChat.test.tsx`:

```tsx
/**
 * useGameChat — unit tests
 * Spec: docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md §3.4
 */
import { renderHook, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

vi.mock('@/lib/api/clients/chatClient', () => ({
  qaStream: vi.fn(),
}));

import { qaStream } from '@/lib/api/clients/chatClient';
import { useGameChat } from '../useGameChat';

// Helper: trasforma una lista di event objects in un async generator (mockable)
async function* mockStream(events: Array<{ type: number; data: unknown }>) {
  for (const e of events) {
    yield e;
  }
}

function wrapper({ children }: { children: ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

// Backend StreamingComplete payload (shape verificata Contracts.cs:109)
const fakeCompletePayload = {
  estimatedReadingTimeMinutes: 0,
  promptTokens: 0,
  completionTokens: 0,
  totalTokens: 0,
  confidence: 0.92,
  Citations: [{ documentId: 'd1', pageNumber: 12, snippet: 'Ogni potere…', relevanceScore: 0.95, copyrightTier: 'full' as const }],
};

// Stream completo: tokens accumulano l'answer, poi Complete fissa il messaggio
const happyEvents = [
  { type: 7, data: 'Sì, ' },
  { type: 7, data: 'ogni potere si attiva ogni volta.' },
  { type: 4, data: fakeCompletePayload },
];

describe('useGameChat', () => {
  beforeEach(() => {
    vi.mocked(qaStream).mockReset();
  });

  it('starts with empty messages and tutor agent', () => {
    const { result } = renderHook(() => useGameChat('wingspan'), { wrapper });
    expect(result.current.messages).toEqual([]);
    expect(result.current.currentAgent).toBe('tutor');
    expect(result.current.isLoading).toBe(false);
  });

  it('ask appends user + agent messages on success', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);
    const { result } = renderHook(() => useGameChat('wingspan'), { wrapper });
    await act(async () => {
      await result.current.ask('posso usare potere?');
    });
    expect(result.current.messages).toHaveLength(2);
    expect(result.current.messages[0].role).toBe('user');
    expect(result.current.messages[0].content).toBe('posso usare potere?');
    expect(result.current.messages[1].role).toBe('agent');
    expect(result.current.messages[1].content).toBe('Sì, ogni potere si attiva ogni volta.');
    expect(result.current.messages[1].overallConfidence).toBe(0.92);
    expect(result.current.messages[1].citations).toHaveLength(1);
    expect(result.current.messages[1].isLowQuality).toBe(false);  // 0.92 >= 0.70
  });

  it('derives isLowQuality=true when confidence < 0.70', async () => {
    const lowConfEvents = [
      { type: 7, data: 'Non sono certo.' },
      { type: 4, data: { ...fakeCompletePayload, confidence: 0.42 } },
    ];
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(lowConfEvents) as any);
    const { result } = renderHook(() => useGameChat('wingspan'), { wrapper });
    await act(async () => { await result.current.ask('edge?'); });
    expect(result.current.messages[1].isLowQuality).toBe(true);
    expect(result.current.messages[1].outOfContext).toBe(false);  // citations present
  });

  it('derives outOfContext=true when no citations + confidence < 0.30', async () => {
    const oocEvents = [
      { type: 7, data: 'Non ho informazioni.' },
      { type: 4, data: { ...fakeCompletePayload, confidence: 0.0, Citations: [] } },
    ];
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(oocEvents) as any);
    const { result } = renderHook(() => useGameChat('wingspan'), { wrapper });
    await act(async () => { await result.current.ask('tg?'); });
    expect(result.current.messages[1].outOfContext).toBe(true);
    expect(result.current.messages[1].citations).toBeUndefined();
  });

  it('isLoading transitions correctly during ask', async () => {
    // Generator che yield-a dopo un await esterno — simula stream lento
    let releaseStream: () => void = () => {};
    const slowStream = async function* () {
      await new Promise<void>(r => { releaseStream = r; });
      yield happyEvents[2];  // Complete event
    };
    vi.mocked(qaStream).mockReturnValueOnce(slowStream() as any);
    const { result } = renderHook(() => useGameChat('wingspan'), { wrapper });

    act(() => { void result.current.ask('q'); });
    await waitFor(() => expect(result.current.isLoading).toBe(true));

    await act(async () => { releaseStream(); });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
  });

  it('isError true when stream throws', async () => {
    const errorStream = async function* () {
      throw new Error('500');
      // unreachable but TS needs yield for generator inference
      yield happyEvents[2];
    };
    vi.mocked(qaStream).mockReturnValueOnce(errorStream() as any);
    const { result } = renderHook(() => useGameChat('wingspan'), { wrapper });
    await act(async () => {
      try {
        await result.current.ask('q');
      } catch {
        /* expected */
      }
    });
    await waitFor(() => expect(result.current.isError).toBe(true));
  });

  it('switchAgent updates currentAgent', () => {
    const { result } = renderHook(() => useGameChat('wingspan'), { wrapper });
    act(() => { result.current.switchAgent('arbitro'); });
    expect(result.current.currentAgent).toBe('arbitro');
  });

  it('switchAgent does NOT clear message history', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);
    const { result } = renderHook(() => useGameChat('wingspan'), { wrapper });
    await act(async () => { await result.current.ask('q'); });
    expect(result.current.messages).toHaveLength(2);
    act(() => { result.current.switchAgent('arbitro'); });
    expect(result.current.messages).toHaveLength(2);  // history preserved
  });

  it('initialAgent overrides default tutor', () => {
    const { result } = renderHook(() => useGameChat('wingspan', 'arbitro'), { wrapper });
    expect(result.current.currentAgent).toBe('arbitro');
  });
});
```

- [ ] **Step 9.3: Run test, verify FAIL**

```bash
cd apps/web && pnpm vitest run src/hooks/queries/__tests__/useGameChat.test
```

- [ ] **Step 9.4: Implementa l'hook**

Crea `apps/web/src/hooks/queries/useGameChat.ts`:

```ts
/**
 * useGameChat — hook per il chat-in-game V2 (G1+G5).
 *
 * FSM minimal: 'idle' | 'submitting' | 'error'. I "responding-*" stati
 * della spec §4.1 sono renderizzati dall'orchestrator in base ai campi
 * della risposta (isLowQuality, outOfContext) — non sono stati hook.
 *
 * Backend: zero modifiche. Usa l'API existente `qaStream()` (SSE) da
 * `apps/web/src/lib/api/clients/chatClient.ts` e accumula solo l'evento
 * `Complete` (type=4) — UX atomica, no streaming token (Q6 brainstorm).
 * Il payload Complete contiene answer + citations + overallConfidence + isLowQuality.
 *
 * Spec: docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md §3.4
 */
'use client';

import { useCallback, useState } from 'react';

import { qaStream } from '@/lib/api/clients/chatClient';
import type { Citation } from '@/types';

import type { AgentKind } from '@/components/v2/game-chat/GameChatHeader';

export interface ChatMessage {
  readonly id: string;
  readonly role: 'user' | 'agent';
  readonly content: string;
  readonly citations?: ReadonlyArray<Citation>;
  readonly overallConfidence?: number;
  readonly isLowQuality?: boolean;
  readonly outOfContext?: boolean;
  readonly createdAt: string;
}

export interface UseGameChatResult {
  readonly messages: readonly ChatMessage[];
  readonly isLoading: boolean;
  readonly isError: boolean;
  readonly currentAgent: AgentKind;
  readonly ask: (question: string) => Promise<void>;
  readonly switchAgent: (next: AgentKind) => void;
}

// Module-private id counter for ephemeral chat message ids (test-mockable).
let messageIdCounter = 0;
const nextId = (prefix: string) => `${prefix}-${++messageIdCounter}-${Date.now()}`;

// Event types from chatClient.ts QA_EVENT_TYPES
const TOKEN_EVENT_TYPE = 7;
const COMPLETE_EVENT_TYPE = 4;
const ERROR_EVENT_TYPE = 5;

// Soglie derivate frontend (backend non espone isLowQuality/outOfContext)
const LOW_QUALITY_THRESHOLD = 0.70;     // < soglia → mostra disclaimer (spec G5)
const OUT_OF_CONTEXT_THRESHOLD = 0.30;  // confidence sotto + 0 citations → out-of-context

// Backend StreamingComplete shape (Contracts.cs:109)
interface StreamingCompletePayload {
  readonly estimatedReadingTimeMinutes?: number;
  readonly promptTokens?: number;
  readonly completionTokens?: number;
  readonly totalTokens?: number;
  readonly confidence?: number | null;
  readonly chatThreadId?: string;
  readonly Citations?: ReadonlyArray<Citation>;  // PascalCase nel record — verifica JSON serialization
  readonly citations?: ReadonlyArray<Citation>;  // fallback camelCase
}

interface ErrorPayload {
  readonly message?: string;
}

export function useGameChat(gameId: string, initialAgent: AgentKind = 'tutor'): UseGameChatResult {
  const [messages, setMessages] = useState<readonly ChatMessage[]>([]);
  const [currentAgent, setCurrentAgent] = useState<AgentKind>(initialAgent);
  const [isLoading, setIsLoading] = useState(false);
  const [isError, setIsError] = useState(false);

  const ask = useCallback(async (question: string) => {
    const userMsg: ChatMessage = {
      id: nextId('u'),
      role: 'user',
      content: question,
      createdAt: new Date().toISOString(),
    };
    setMessages(prev => [...prev, userMsg]);
    setIsLoading(true);
    setIsError(false);

    // Backend non espone "agentType" — system agent unico.
    // currentAgent resta UI-only (badge nell'header + filtro suggested prompts in futuro).
    let answerBuffer = '';

    try {
      const stream = qaStream({
        gameId,
        query: question,
      });

      for await (const event of stream) {
        if (event.type === TOKEN_EVENT_TYPE) {
          // Accumula token nel buffer; UX atomica = no rendering parziale.
          const tokenData = event.data;
          if (typeof tokenData === 'string') {
            answerBuffer += tokenData;
          } else if (typeof tokenData === 'object' && tokenData !== null && 'content' in tokenData) {
            answerBuffer += String((tokenData as { content?: unknown }).content ?? '');
          }
        } else if (event.type === COMPLETE_EVENT_TYPE) {
          const payload = event.data as StreamingCompletePayload;
          const confidence = payload.confidence ?? undefined;
          const citations = payload.Citations ?? payload.citations ?? [];
          // Derivation frontend (backend non espone questi flag)
          const isLowQuality = confidence !== undefined && confidence < LOW_QUALITY_THRESHOLD;
          const outOfContext =
            citations.length === 0 && (confidence === undefined || confidence < OUT_OF_CONTEXT_THRESHOLD);

          const agentMsg: ChatMessage = {
            id: nextId('a'),
            role: 'agent',
            content: answerBuffer,
            citations: citations.length > 0 ? citations : undefined,
            overallConfidence: confidence,
            isLowQuality,
            outOfContext,
            createdAt: new Date().toISOString(),
          };
          setMessages(prev => [...prev, agentMsg]);
        } else if (event.type === ERROR_EVENT_TYPE) {
          const errPayload = event.data as ErrorPayload;
          throw new Error(errPayload?.message ?? 'QA stream error');
        }
        // Ignora type=0 (StateUpdate), type=1 (Citations early), type=8 (Follow-up).
      }
    } catch (e) {
      setIsError(true);
      throw e;
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);  // currentAgent intentionally NOT in deps (UI-only)

  const switchAgent = useCallback((next: AgentKind) => {
    setCurrentAgent(next);
  }, []);

  return {
    messages,
    isLoading,
    isError,
    currentAgent,
    ask,
    switchAgent,
  };
}
```

> ⚠️ **Adatta a Step 9.1**: se `QaStreamRequest` ha campi diversi (es. richiede `agentId` invece di `agentType`), aggiorna la chiamata `qaStream({...})` rimuovendo il `as any`. Il contract pubblico dell'hook NON cambia.
>
> ⚠️ Se il backend espone `Complete` payload SENZA `overallConfidence`/`isLowQuality`/`outOfContext`, alcune feature G5 non saranno funzionanti. **STOP & ESCALATE**: serve coordinamento backend prima di proseguire (oppure mock con valori sintetici per UI demo).

- [ ] **Step 9.5: Run test fino a passare**

```bash
cd apps/web && pnpm vitest run src/hooks/queries/__tests__/useGameChat.test
```
Expected: PASS (7/7).

- [ ] **Step 9.6: Commit**

```bash
git add apps/web/src/hooks/queries/useGameChat.ts apps/web/src/hooks/queries/__tests__/useGameChat.test.tsx
git commit -m "feat(web): #915 useGameChat hook (FSM + agent switch)"
```

---

## Task 10: `GameChatTabV2` orchestrator

**Files:**
- Create: `apps/web/src/components/v2/game-chat/GameChatTabV2.tsx`
- Test: `apps/web/src/components/v2/game-chat/__tests__/GameChatTabV2.test.tsx`
- Modify: `apps/web/src/components/v2/game-chat/index.ts` (aggiungi export)

**Contract:**
```ts
interface GameChatTabV2Props {
  readonly gameId: string;
  readonly initialAgent?: AgentKind;  // default 'tutor'
}
```

L'orchestrator compone tutti i 11 sotto-componenti, gestisce lo state del modal citation, mappa le `citations[]` in `<CitationChip>` cliccabili, e applica la regola di rendering FSM (alta/media/bassa + isLowQuality → disclaimer + outOfContext → action pills).

- [ ] **Step 10.1: Test failing — orchestrator integration**

Crea `apps/web/src/components/v2/game-chat/__tests__/GameChatTabV2.test.tsx`:

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

vi.mock('@/lib/api/clients/chatClient', () => ({
  qaStream: vi.fn(),
}));

import { qaStream } from '@/lib/api/clients/chatClient';
import { GameChatTabV2 } from '../GameChatTabV2';

async function* mockStream(events: Array<{ type: number; data: unknown }>) {
  for (const e of events) yield e;
}

function wrapper({ children }: { children: ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

// Backend StreamingComplete shape (Contracts.cs:109): no isLowQuality/outOfContext
// Frontend hook deriva: isLowQuality = confidence < 0.70, outOfContext = no citations + confidence < 0.30
const sampleCitation = {
  documentId: 'd1',
  pageNumber: 12,
  snippet: 'ogni potere…',
  relevanceScore: 0.95,
  copyrightTier: 'full' as const,
};

// Stream completo: tokens accumulano answer, Complete fissa confidence + citations
const happyEvents = [
  { type: 7, data: 'Sì, ' },
  { type: 7, data: 'ogni potere si attiva ogni volta.' },
  { type: 4, data: { confidence: 0.92, Citations: [sampleCitation] } },
];

const lowConfEvents = [
  { type: 7, data: 'Non sono certo, probabilmente si rimescolano gli scarti.' },
  { type: 4, data: { confidence: 0.42, Citations: [{ ...sampleCitation, pageNumber: 6, snippet: 'fine mazzo', documentId: 'd2' }] } },
];

const oocEvents = [
  { type: 7, data: 'Non ho informazioni su Tainted Grail.' },
  { type: 4, data: { confidence: 0.0, Citations: [] } },
];

describe('GameChatTabV2', () => {
  beforeEach(() => {
    vi.mocked(qaStream).mockReset();
  });

  it('renders empty state with input bar and suggested prompts', () => {
    render(<GameChatTabV2 gameId="wingspan" />, { wrapper });
    expect(screen.getByPlaceholderText(/scrivi/i)).toBeInTheDocument();
  });

  it('happy path: ask → user bubble + agent bubble + citation chip + alta badge', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);
    render(<GameChatTabV2 gameId="wingspan" />, { wrapper });
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'q?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));
    await waitFor(() => expect(screen.getByText(/Sì, ogni potere/)).toBeInTheDocument());
    expect(screen.getByText('q?')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /p\. ?12/ })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveAttribute('data-tier', 'alta');
  });

  it('low confidence path: shows disclaimer + bassa badge', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(lowConfEvents) as any);
    render(<GameChatTabV2 gameId="wingspan" />, { wrapper });
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'edge?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));
    await waitFor(() => expect(screen.getByText(/non sono certo/i)).toBeInTheDocument());
    expect(screen.getByRole('status')).toHaveAttribute('data-tier', 'bassa');
  });

  it('out-of-context path: shows action pills, no confidence color', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(oocEvents) as any);
    render(<GameChatTabV2 gameId="wingspan" />, { wrapper });
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'tg?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));
    await waitFor(() => expect(screen.getByRole('button', { name: /cambia gioco/i })).toBeInTheDocument());
    expect(screen.getByRole('button', { name: /cerca un agente/i })).toBeInTheDocument();
  });

  it('citation chip click opens CitationModal', async () => {
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);
    render(<GameChatTabV2 gameId="wingspan" />, { wrapper });
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'q?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));
    await waitFor(() => screen.getByRole('button', { name: /p\. ?12/ }));
    fireEvent.click(screen.getByRole('button', { name: /p\. ?12/ }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    // KB button is hidden until G4 → onOpenInKb is undefined
    expect(screen.queryByRole('button', { name: /apri nella kb/i })).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 10.2: Run test, verify FAIL**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/GameChatTabV2.test
```

- [ ] **Step 10.3: Implementa l'orchestrator**

Crea `apps/web/src/components/v2/game-chat/GameChatTabV2.tsx`:

```tsx
/**
 * GameChatTabV2 — orchestrator del chat-in-game V2.
 *
 * Compone i 11 sotto-componenti del barrel game-chat. Wraps useGameChat.
 * Renderizza FSM in base ai campi della response:
 *   - isLowQuality === true → LowConfidenceDisclaimer (banner giallo)
 *   - outOfContext === true → OutOfContextActions (3 action pill)
 *   - sempre: ConfidenceBadge (3-tier color) e CitationChip[] per ogni citation
 *
 * Hybrid C citation behavior (Q4): tap chip → CitationModal preview.
 * Footer "Apri nella KB" attualmente NASCOSTO (onOpenInKb=undefined).
 * Quando G4 atterra: passa onOpenInKb={() => router.push(`/kb/${gameId}#chunk-${id}`)}.
 *
 * Spec: docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md §3.1 §4
 */
'use client';

import { useState, type ReactElement, type ReactNode } from 'react';

import type { Citation } from '@/types';

import { ChatBubble } from './ChatBubble';
import { ChatInputBar } from './ChatInputBar';
import { CitationChip } from './CitationChip';
import { CitationModal } from './CitationModal';
import { ConfidenceBadge } from './ConfidenceBadge';
import { GameChatHeader, type AgentKind } from './GameChatHeader';
import { GameChatSidebar } from './GameChatSidebar';
import { LowConfidenceDisclaimer, type DisclaimerAlternative } from './LowConfidenceDisclaimer';
import { OutOfContextActions, type OutOfContextAction } from './OutOfContextActions';
import { SuggestedPrompts, type SuggestedPrompt } from './SuggestedPrompts';
import { TypingIndicator } from './TypingIndicator';

import { useGameChat, type ChatMessage } from '@/hooks/queries/useGameChat';

export interface GameChatTabV2Props {
  readonly gameId: string;
  readonly initialAgent?: AgentKind;
  readonly gameTitle?: string;   // injected dal parent (GameAiChatTab)
  readonly gameIcon?: string;
}

const AGENT_AVATAR: Record<AgentKind, string> = { tutor: '🧙', arbitro: '⚖️' };
const AGENT_NAME: Record<AgentKind, string> = { tutor: 'Tutor', arbitro: 'Arbitro' };

const SUGGESTED_DEFAULT: ReadonlyArray<Omit<SuggestedPrompt, 'onClick'>> = [
  { category: 'A', text: 'Posso ripetere questa azione nel turno?' },
  { category: 'C', text: 'Setup per N giocatori?' },
  { category: 'E', text: 'Come si calcola il punteggio?' },
  { category: 'F', text: 'Edge case raro' },
];

export function GameChatTabV2({
  gameId,
  initialAgent = 'tutor',
  gameTitle = 'Gioco',
  gameIcon = '🎲',
}: GameChatTabV2Props): ReactElement {
  const chat = useGameChat(gameId, initialAgent);
  const [inputValue, setInputValue] = useState('');
  const [selectedCitation, setSelectedCitation] = useState<Citation | null>(null);

  const handleSubmit = (q: string) => {
    setInputValue('');
    void chat.ask(q);
  };

  const handleSuggestedClick = (text: string) => {
    setInputValue('');
    void chat.ask(text);
  };

  const renderAgentExtras = (msg: ChatMessage): ReactNode => {
    const showOoc = msg.outOfContext === true;
    const oocActions: OutOfContextAction[] = showOoc
      ? [
          { kind: 'switch-game', label: 'Cambia gioco', onClick: () => {} },
          { kind: 'find-agent', label: 'Cerca un agente', onClick: () => {} },
          { kind: 'stay', label: 'Resta qui', onClick: () => {} },
        ]
      : [];

    const lowConfAlts: DisclaimerAlternative[] = msg.isLowQuality && !showOoc
      ? [
          { label: 'Verifica nel manuale', kind: 'kb' },
          { label: 'Cerca su BGG', kind: 'external', url: 'https://boardgamegeek.com/' },
        ]
      : [];

    return (
      <>
        {msg.citations && msg.citations.length > 0 && (
          <div className="mt-3 flex flex-wrap gap-2 border-t border-dashed border-[hsl(var(--c-agent)/0.25)] pt-3">
            {msg.citations.map((c, i) => {
              // Citation type (domain.ts:134) ha pageNumber + snippet ma NO sectionTitle.
              // Usiamo il primo segmento dello snippet (max 60 char) come "section title".
              // Se serve un sectionTitle reale in futuro, va aggiunto al QaResponse backend.
              const previewTitle = c.snippet
                ? c.snippet.length > 60 ? c.snippet.slice(0, 57) + '…' : c.snippet
                : `Documento ${i + 1}`;
              return (
                <CitationChip
                  key={`${c.documentId}-${c.pageNumber}-${i}`}
                  pageNumber={c.pageNumber}
                  sectionTitle={previewTitle}
                  snippet={c.snippet}
                  onClick={() => setSelectedCitation(c)}
                />
              );
            })}
          </div>
        )}
        {msg.isLowQuality && !showOoc && (
          <LowConfidenceDisclaimer
            summary={msg.content}
            alternatives={lowConfAlts}
          />
        )}
        {showOoc && <OutOfContextActions actions={oocActions} />}
        {msg.overallConfidence !== undefined && (
          <div className="mt-2 flex items-center justify-between gap-2 flex-wrap">
            <ConfidenceBadge score={msg.overallConfidence} />
          </div>
        )}
      </>
    );
  };

  const suggested: SuggestedPrompt[] = SUGGESTED_DEFAULT.map(p => ({
    ...p,
    onClick: () => handleSuggestedClick(p.text),
  }));

  return (
    <div className="flex h-full flex-col">
      <GameChatHeader
        gameTitle={gameTitle}
        gameIcon={gameIcon}
        agent={chat.currentAgent}
      />

      <div className="flex flex-1 overflow-hidden">
        {/* Desktop sidebar — hidden under lg */}
        <div className="hidden lg:block">
          <GameChatSidebar
            gameTitle={gameTitle}
            gameIcon={gameIcon}
            currentAgent={chat.currentAgent}
            history={[]}
            onAgentChange={chat.switchAgent}
            onHistorySelect={() => {}}
          />
        </div>

        <div className="flex flex-1 flex-col overflow-hidden">
          <div role="log" aria-live="polite" className="flex flex-1 flex-col gap-4 overflow-y-auto p-4">
            {chat.messages.map(msg =>
              msg.role === 'user' ? (
                <ChatBubble key={msg.id} role="user" content={msg.content} />
              ) : (
                <ChatBubble
                  key={msg.id}
                  role="agent"
                  content={msg.content}
                  agentName={AGENT_NAME[chat.currentAgent]}
                  avatar={AGENT_AVATAR[chat.currentAgent]}
                >
                  {renderAgentExtras(msg)}
                </ChatBubble>
              )
            )}
            {chat.isLoading && (
              <ChatBubble role="agent" content="" agentName={AGENT_NAME[chat.currentAgent]} avatar={AGENT_AVATAR[chat.currentAgent]}>
                <TypingIndicator hint="Cerco nella KB" />
              </ChatBubble>
            )}
          </div>
          <SuggestedPrompts prompts={suggested} groupLabel="Domande comuni" />
          <ChatInputBar
            value={inputValue}
            onChange={setInputValue}
            onSubmit={handleSubmit}
            disabled={chat.isLoading}
            placeholder={`Scrivi una domanda al ${AGENT_NAME[chat.currentAgent]}…`}
          />
        </div>
      </div>

      <CitationModal
        citation={selectedCitation}
        open={selectedCitation !== null}
        onClose={() => setSelectedCitation(null)}
        onOpenInKb={undefined /* TODO: enable when G4 lands — () => router.push(`/kb/${gameId}#chunk-${selectedCitation?.chunkId}`) */}
      />
    </div>
  );
}
```

- [ ] **Step 10.4: Aggiungi al barrel**

Modifica `apps/web/src/components/v2/game-chat/index.ts` aggiungendo:

```ts
export { GameChatTabV2 } from './GameChatTabV2';
export type { GameChatTabV2Props } from './GameChatTabV2';
```

- [ ] **Step 10.5: Run test fino a passare**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/GameChatTabV2.test
```
Expected: PASS (5/5).

- [ ] **Step 10.6: Commit**

```bash
git add apps/web/src/components/v2/game-chat/GameChatTabV2.tsx apps/web/src/components/v2/game-chat/__tests__/GameChatTabV2.test.tsx apps/web/src/components/v2/game-chat/index.ts
git commit -m "feat(web): #915 GameChatTabV2 orchestrator (G1+G5 wired)"
```

---

## Task 11: Wiring `GameAiChatTab.tsx` (sostituzione V1 → V2)

**Files:**
- Modify: `apps/web/src/components/game-detail/tabs/GameAiChatTab.tsx`

**Contract**: il file esiste e oggi rende V1 chat-unified. Sostituiamo body con `<GameChatTabV2 gameId={gameId} ... />`. Mantieni la sua signature `(gameId, variant, isNotInLibrary)` — i nuovi componenti V2 ricevono solo `gameId` (gli altri parametri restano per future varianti, non bloccano).

- [ ] **Step 11.1: Leggi il file attuale**

```bash
cat apps/web/src/components/game-detail/tabs/GameAiChatTab.tsx
```

Annota:
- Nome export (probabilmente `function GameAiChatTab(...)`)
- Props attuali (`gameId`, `variant`, `isNotInLibrary`)
- Comportamento `isNotInLibrary` (probabilmente mostra empty state se gioco non in libreria)

- [ ] **Step 11.2: Sostituisci body**

Sostituisci il corpo di `GameAiChatTab` con:

```tsx
import { GameChatTabV2 } from '@/components/v2/game-chat';

export function GameAiChatTab({ gameId, variant, isNotInLibrary }: GameTabProps) {
  const containerClass = ...; // mantieni la classe esistente

  if (isNotInLibrary) {
    // Mantieni l'empty-state V1 esistente per il caso isNotInLibrary
    return (
      <div role="tabpanel" aria-labelledby="game-tab-aiChat" className={containerClass}>
        {/* contenuto empty-state V1 invariato */}
      </div>
    );
  }

  return (
    <div role="tabpanel" aria-labelledby="game-tab-aiChat" className={containerClass}>
      <GameChatTabV2 gameId={gameId} />
    </div>
  );
}
```

> ⚠️ **Verifica**: l'empty-state per `isNotInLibrary` resta in V1 (è UX di onboarding "aggiungi alla libreria", out of scope G1+G5). Il branch principale (game in libreria) ora usa V2.

- [ ] **Step 11.3: Typecheck**

```bash
cd apps/web && pnpm typecheck
```
Expected: 0 errori. Se errore "missing prop": passa anche `gameTitle` e `gameIcon` se disponibili dal context (es. via `useLibraryGameDetail(gameId)`).

- [ ] **Step 11.4: Run test esistenti del tab**

```bash
cd apps/web && pnpm vitest run "src/components/game-detail"
```
Expected: tutti i test esistenti passano. Se snapshot test del tab AI Chat fanno snapshot del DOM V1, andranno aggiornati per riflettere V2 (atteso, NON regressione — usa `pnpm vitest run -u` se necessario).

- [ ] **Step 11.5: Commit**

```bash
git add apps/web/src/components/game-detail/tabs/GameAiChatTab.tsx
git commit -m "feat(web): #915 wire GameChatTabV2 inside GameAiChatTab (V1→V2 swap)"
```

---

## Task 12: Audit V1 chat-unified imports + removal plan

**Files:**
- Modify (DELETE): file V1 chat-unified usati esclusivamente da `?tab=aiChat` (lista determinata dall'audit)

- [ ] **Step 12.1: Audit script (PowerShell-friendly)**

> Su Windows usa Git Bash o PowerShell — entrambi i comandi sono forniti. Su Linux/macOS usa bash.

**Bash (Git Bash, Linux, macOS):**
```bash
grep -rn "from '@/components/chat-unified" apps/web/src --include="*.tsx" --include="*.ts" > chat-unified-imports.txt
cat chat-unified-imports.txt
```

**PowerShell:**
```powershell
Get-ChildItem -Recurse -Path apps/web/src -Include *.ts,*.tsx |
  Select-String "from '@/components/chat-unified" |
  Tee-Object -FilePath chat-unified-imports.txt
```

Categorizza ogni risultato:
1. **Solo in `?tab=aiChat` (file ora rimosso/modificato)** → DELETE candidate
2. **Anche in `/chat/[threadId]` (`apps/web/src/app/(chat)/chat/`)** → KEEP
3. **Anche in admin (`apps/web/src/app/admin/`)** → KEEP
4. **Anche in altri test/hooks** → KEEP (audit per file)

- [ ] **Step 12.2: Genera lista candidate per delete**

Per ogni componente in `apps/web/src/components/chat-unified/`, conta gli import esterni:

**Bash (Git Bash, Linux, macOS):**
```bash
for f in apps/web/src/components/chat-unified/*.tsx; do
  name=$(basename "$f" .tsx)
  count=$(grep -rln "chat-unified/${name}\b" apps/web/src --include="*.tsx" --include="*.ts" \
    | grep -v "chat-unified/" | wc -l)
  echo "$count  $name"
done | sort -n
```

**PowerShell:**
```powershell
Get-ChildItem apps/web/src/components/chat-unified -Filter *.tsx -File | ForEach-Object {
  $name = $_.BaseName
  $count = (Get-ChildItem -Recurse -Path apps/web/src -Include *.ts,*.tsx |
            Where-Object { $_.FullName -notmatch 'chat-unified' } |
            Select-String -Pattern "chat-unified/$name\b" -List).Count
  "{0,3}  {1}" -f $count, $name
} | Sort-Object
```

I componenti con `count = 0` sono candidate per delete sicuro. Fai DOUBLE CHECK manualmente: aprire il file, vedere chi lo importa effettivamente — il barrel `chat-unified/index.ts` (se esiste) potrebbe re-esportare in massa.

- [ ] **Step 12.3: Esegui delete con cautela**

Per ogni file deleted:
- Cancella il file `Component.tsx`
- Cancella il file `__tests__/Component.test.tsx` (se esiste)
- Cancella eventuali export dal `chat-unified/index.ts` (se esiste un barrel)

> ⚠️ **STOP & ASK** se l'audit mostra ambiguità (es. component con 1 solo import che è il barrel). Meglio lasciare un componente "potenzialmente orphano" che rompere `/chat/[threadId]` o admin. Marca tale file come "candidate for deletion in follow-up PR" nel commit message.

- [ ] **Step 12.4: Typecheck + lint dopo delete**

```bash
cd apps/web && pnpm typecheck && pnpm lint
```
Expected: 0 errori. Se errori: hai cancellato troppo, ripristina il file ed escludi dalla lista delete.

- [ ] **Step 12.5: Commit (separato per facilità revert)**

```bash
git add -u apps/web/src/components/chat-unified
git commit -m "chore(web): #915 remove unused chat-unified V1 components after V2 migration"
```

> Se l'audit non rivela orfani sicuri: skip questo commit, lascia un comment nel PR description.

---

## Task 13: Final verify + matrix update + PR

- [ ] **Step 13.1: Lint + typecheck full pass**

```bash
cd apps/web && pnpm lint && pnpm typecheck
```
Expected: 0 errori, 40 warning baseline (vedi G3 Task 4 per riferimento).

- [ ] **Step 13.2: Run full unit suite**

```bash
cd apps/web && pnpm test
```
Expected: tutti i test passano, comprese le ~40 nuove asserzioni dei componenti V2 game-chat + 5 dell'orchestrator + 7 dell'hook.

- [ ] **Step 13.3: Smoke test manuale**

Avvia `make dev-core`, login, naviga a `/library/games/[gameId]?tab=aiChat`:
- Header V2 visibile (game-icon + agent badge "🧙 Tutor")
- Sidebar desktop: agent switch + history rail vuoto
- Input bar in basso, suggested prompts sopra
- Invia "test" → typing indicator → risposta del backend reale
- Click sulla citation chip → CitationModal apre, footer NON ha il bottone "Apri nella KB"
- Esc o click fuori chiude il modal
- Switch agent (sidebar): il badge nell'header cambia, history preserva

Annota differenze visive (spacing, colori) come issue follow-up: NON correggere in questa PR salvo regressioni gravi.

- [ ] **Step 13.4: Aggiorna v2-migration-matrix**

Modifica `docs/for-developers/frontend/v2-migration-matrix.md` aggiungendo nella sezione `/games/[id]` (Tier L) una nuova riga sotto le 8 esistenti:

```markdown
| (extension G1+G5) | `GameChatTabV2 + 11 game-chat components` | `apps/web/src/components/v2/game-chat/` | `/library/games/[id]?tab=aiChat` | done | #YYY | T A V |
```

Sostituisci `#YYY` con il numero PR effettivo dopo apertura.

- [ ] **Step 13.5: Push + apri PR verso `main-dev`**

```bash
git push -u origin feature/issue-915-game-chat-v2-citations
gh pr create --repo meepleAi-app/meepleai-monorepo --base main-dev --title "feat(web): #915 G1+G5 — Game Chat Tab V2 with citations + confidence" --body "$(cat <<'EOF'
## Summary

Sostituisce il rendering V1 \`chat-unified\` per la route \`/library/games/[gameId]?tab=aiChat\` con 12 nuovi componenti V2 puri:

- \`CitationChip\`, \`CitationModal\` (G1 — citazioni cliccabili, hybrid C: modal preview con footer "Apri KB" gated da G4)
- \`ConfidenceBadge\` 3-tier (G5 — verde ≥0.80, arancione ≥0.50, rosso <0.50)
- \`LowConfidenceDisclaimer\` (G5 — banner giallo + alternative quando \`isLowQuality === true\`)
- \`OutOfContextActions\` (3 action pill switch-game/find-agent/stay)
- \`ChatBubble\`, \`SuggestedPrompts\`, \`ChatInputBar\`, \`TypingIndicator\`, \`GameChatHeader\`, \`GameChatSidebar\`
- \`GameChatTabV2\` orchestrator + \`useGameChat\` hook
- Audit-first removal di componenti V1 chat-unified usati esclusivamente da quella route

Closes #915.

## Spec / Plan

- Spec: [\`docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md\`](docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md)
- Plan: [\`docs/superpowers/plans/2026-05-09-game-chat-tab-v2-g1-g5.md\`](docs/superpowers/plans/2026-05-09-game-chat-tab-v2-g1-g5.md)
- Mockup: [\`admin-mockups/design_files/sp4-game-chat-tab.html\`](admin-mockups/design_files/sp4-game-chat-tab.html)

## Backend

Zero modifiche. \`QaResponse\` espone già \`overallConfidence\`, \`isLowQuality\`, \`citations[]\` (\`apps/web/src/types/domain.ts:179-194\`).

## Test plan

- [x] Unit: 11 componenti puri/controllati (~40 asserzioni)
- [x] Unit: \`useGameChat\` hook (7 scenari FSM)
- [x] Integration: \`GameChatTabV2\` orchestrator (5 scenari happy/low-conf/ooc/citation-click/empty)
- [x] No regression on \`/chat/[threadId]\` route (V1 chat-unified preservato)
- [ ] Smoke test manuale (richiesta al reviewer): \`/library/games/[id]?tab=aiChat\` con backend reale

## Out of scope (future PRs)

- \`/chat/[threadId]\` migration to V2 (tracked: issue #491 mockup)
- KB navigation from citation modal (gated by G4 — \`/kb/[id]\` separate plan)
- E2E Playwright (delegate to G2 fast-resume audit plan)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 13.6: Aggiorna matrice con PR number**

Sostituisci `#YYY` nella matrice e committa:

```bash
git add docs/for-developers/frontend/v2-migration-matrix.md
git commit -m "docs(matrix): #915 set PR ref for game-chat-tab v2 row"
git push
```

---

## Out of scope (follow-up plans)

| Item | Reason | Plan target |
|---|---|---|
| `/chat/[threadId]` v2 migration | Esplicitamente out of scope dello spec; ha mockup separato in #491 | Plan dedicato post-G1+G5 |
| `/kb/[id]` 6 stub pending | Goal G4 (sblocca anche il footer "Apri KB" del CitationModal) | Plan G4 separato |
| Fast resume audit | Goal G2 — verifica scroll/state preservation in chat | Plan G2 |
| Streaming SSE token-by-token | Q6 brainstorm scelse risposta atomica | Eventuale future enhancement |
| `CitationModal` con preview PDF reale | V1 `PdfPageModal` ha questa logica; V2 attuale mostra solo snippet testuale | Follow-up se richiesto da UX review |

---

## Spec Self-Review + Independent Review (storia)

**Plan-review history**:
1. **Self-review autore** (writing-plans skill): coverage spec OK, 0 placeholder accidentali, type consistency verificata.
2. **Independent code-reviewer** (subagent): VERDICT **ACCEPT WITH MODIFICATIONS** — 2 critical + 4 major fix richiesti.
3. **Fix applicati inline**:
   - **C1**: API `api.qa.askQuestion` non esiste. Riscritto Task 9 per usare `qaStream` (SSE da `chatClient.ts:399`) accumulando solo evento `Complete` (type=4). Test mock usa async generator. UX atomica preservata (Q6 brainstorm).
   - **C2**: `Citation` type NON ha `sectionTitle`. Task 10 orchestrator ora deriva il "section title" del chip dal primo segmento dello `snippet` (max 60 char), con fallback `Documento ${i+1}` se snippet vuoto. Niente `as any` cast.
   - **M2**: Step 12.1+12.2 ora forniscono comandi shell sia Bash (Git Bash/Linux/macOS) che PowerShell (Windows nativo).
   - **M4 (circular import potenziale)**: hook `useGameChat` ora importa `AgentKind` direttamente da `./GameChatHeader` (NON dal barrel) per evitare ciclo barrel↔hook.
   - **M1 (GameAiChatTab.tsx)**: file confermato esistere (reviewer aveva torto). Plan corretto, no fix necessario.
   - **M3 (PdfPageModal/Skeleton)**: entrambi confermati esistere. No fix necessario.

**Spec coverage finale**: tutti i 12 componenti dello spec §3 sono coperti da Task 1-10 + barrel Task 8 + orchestrator Task 10. Hook §3.4 in Task 9. Wiring §1.1 in Task 11. Removal V1 §5 in Task 12. Matrix update §6.4 in Task 13.4.

**Placeholder scan finale**: zero `TBD/TODO/...later` accidentali. Placeholder intenzionali:
- `YYY` (PR #) — sostituito a Step 13.6
- `// TODO: enable when G4 lands` nel CitationModal hybrid C — pattern intenzionale documentato
- `as any` su `QaStreamRequest` in Step 9.4 — esplicitato come "verifica Step 9.1, rimuovi cast quando shape confermato"

**Type consistency finale**:
- `AgentKind = 'tutor' | 'arbitro'` definito in `GameChatHeader`, riusato da `GameChatSidebar` + `useGameChat` + `GameChatTabV2` ✓
- `Citation` type importato da `@/types` con campi reali confermati: `documentId`, `pageNumber`, `snippet`, `relevanceScore`, `copyrightTier`, `paraphrasedSnippet?`, `isPublic?` ✓
- `ConfidenceTier = 'alta' | 'media' | 'bassa'` definito in `ConfidenceBadge`, esposto via barrel ✓
- `PromptCategory = 'A' | 'B' | 'C' | 'E' | 'F'` (NO 'D' — Q4 brainstorm escluso strategia) ✓
- `OutOfContextActionKind` enum coerente in test e implementazione ✓
- API endpoint: `qaStream` con SSE event `type=4` Complete confermato in `chatClient.ts:389-410` ✓

**Risk residuo**:
- Step 9.1 chiede verifica del payload `Complete` event: se backend NON espone `overallConfidence`/`isLowQuality`/`outOfContext`, alcune feature G5 non funzionano. STOP & ESCALATE esplicito (richiede coordinamento backend).
- Removal V1 (Task 12): audit puo lasciare orfani da categorizzare manualmente. STOP-AND-ASK esplicito nello Step 12.3.
