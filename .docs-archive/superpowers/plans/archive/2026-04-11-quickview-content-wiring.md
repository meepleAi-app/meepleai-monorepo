# QuickView Content Wiring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire `RulesContent.tsx` e `FaqContent.tsx` (attualmente placeholder) all'endpoint esistente `GET /api/v1/shared-games/{gameId}/analysis`, con caching localStorage tramite `rules-cache.ts`.

**Architecture:** Il client `sharedGamesClient.getGameAnalysis(gameId)` esiste già e restituisce `RulebookAnalysisDto[]`. I dati vengono prima cercati in cache (`getCachedAnalyses`), poi fetchati e memorizzati (`cacheRulebookAnalyses`). I componenti sono read-only e non richiedono store Zustand: usano `useState`+`useEffect` locali.

**Tech Stack:** React 19, Next.js 16 App Router, Vitest + Testing Library, `sharedGamesClient` (fetch), `rules-cache.ts` (localStorage)

---

## File Structure

| File | Azione | Responsabilità |
|------|--------|----------------|
| `apps/web/src/components/layout/QuickView/RulesContent.tsx` | Modify | Fetch + cache + display summary/mechanics/phases/victory |
| `apps/web/src/components/layout/QuickView/FaqContent.tsx` | Modify | Fetch + cache + display generatedFaqs/commonQuestions |
| `apps/web/src/components/layout/QuickView/__tests__/RulesContent.test.tsx` | Create | Test fetch, cache-hit, loading, empty state, error |
| `apps/web/src/components/layout/QuickView/__tests__/FaqContent.test.tsx` | Create | Test fetch, cache-hit, loading, empty state |

---

### Task 1: Wire RulesContent alle API

**Files:**
- Modify: `apps/web/src/components/layout/QuickView/RulesContent.tsx`
- Create: `apps/web/src/components/layout/QuickView/__tests__/RulesContent.test.tsx`

- [ ] **Step 1: Scrivi il test fallente**

Crea `apps/web/src/components/layout/QuickView/__tests__/RulesContent.test.tsx`:

```tsx
import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock rules-cache
vi.mock('@/lib/game-night/rules-cache', () => ({
  getCachedAnalyses: vi.fn().mockReturnValue(null),
  cacheRulebookAnalyses: vi.fn(),
}));

// Mock sharedGamesClient
vi.mock('@/lib/api/clients/sharedGamesClient', () => ({
  createSharedGamesClient: vi.fn(() => ({
    getGameAnalysis: vi.fn().mockResolvedValue([
      {
        id: 'a1',
        sharedGameId: 'g1',
        gameTitle: 'Catan',
        summary: 'Commercia e costruisci per vincere.',
        keyMechanics: ['Scambio', 'Costruzione'],
        victoryConditions: { primary: 'Primi 10 punti', alternatives: [], isPointBased: true, targetPoints: 10 },
        resources: [],
        gamePhases: [{ name: 'Setup', description: 'Posiziona esagoni', order: 1, isOptional: false }],
        commonQuestions: [],
        generatedFaqs: [],
        confidenceScore: 0.9,
        version: '1',
        isActive: true,
        source: 'LLM',
        analyzedAt: '2026-01-01T00:00:00Z',
        createdBy: 'user1',
        keyConcepts: [],
        completionStatus: 'Complete',
      },
    ]),
  })),
}));

// Mock httpClient dependency
vi.mock('@/lib/api/core/httpClient', () => ({
  createHttpClient: vi.fn(() => ({})),
}));

import { RulesContent } from '../RulesContent';

describe('RulesContent', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('mostra placeholder quando gameId è null', () => {
    render(<RulesContent gameId={null} />);
    expect(screen.getByText(/seleziona un gioco/i)).toBeInTheDocument();
  });

  it('mostra loading spinner durante fetch', () => {
    render(<RulesContent gameId="g1" />);
    expect(screen.getByTestId('rules-loading')).toBeInTheDocument();
  });

  it('mostra il summary dopo il fetch', async () => {
    render(<RulesContent gameId="g1" />);
    await waitFor(() => {
      expect(screen.getByText('Commercia e costruisci per vincere.')).toBeInTheDocument();
    });
  });

  it('mostra le meccaniche chiave', async () => {
    render(<RulesContent gameId="g1" />);
    await waitFor(() => {
      expect(screen.getByText('Scambio')).toBeInTheDocument();
      expect(screen.getByText('Costruzione')).toBeInTheDocument();
    });
  });

  it('mostra le fasi di gioco', async () => {
    render(<RulesContent gameId="g1" />);
    await waitFor(() => {
      expect(screen.getByText('Setup')).toBeInTheDocument();
    });
  });

  it('mostra le condizioni di vittoria', async () => {
    render(<RulesContent gameId="g1" />);
    await waitFor(() => {
      expect(screen.getByText(/primi 10 punti/i)).toBeInTheDocument();
    });
  });

  it('usa la cache se disponibile senza fetching', async () => {
    const { getCachedAnalyses } = await import('@/lib/game-night/rules-cache');
    vi.mocked(getCachedAnalyses).mockReturnValue({
      analyses: [
        {
          id: 'a1',
          sharedGameId: 'g1',
          gameTitle: 'Catan',
          summary: 'Da cache.',
          keyMechanics: [],
          victoryConditions: null,
          resources: [],
          gamePhases: [],
          commonQuestions: [],
          generatedFaqs: [],
          confidenceScore: 0.9,
          version: '1',
          isActive: true,
          source: 'LLM',
          analyzedAt: '2026-01-01T00:00:00Z',
          createdBy: 'user1',
          keyConcepts: [],
          completionStatus: 'Complete',
        } as any,
      ],
      cachedAt: new Date().toISOString(),
      expiresAt: new Date(Date.now() + 3600000).toISOString(),
      gameTitle: 'Catan',
    });

    render(<RulesContent gameId="g1" />);
    await waitFor(() => {
      expect(screen.getByText('Da cache.')).toBeInTheDocument();
    });
  });

  it('mostra messaggio di errore se il fetch fallisce', async () => {
    const { createSharedGamesClient } = await import('@/lib/api/clients/sharedGamesClient');
    vi.mocked(createSharedGamesClient).mockReturnValue({
      getGameAnalysis: vi.fn().mockRejectedValue(new Error('Network error')),
    } as any);

    render(<RulesContent gameId="g1" />);
    await waitFor(() => {
      expect(screen.getByText(/non disponibile/i)).toBeInTheDocument();
    });
  });
});
```

- [ ] **Step 2: Esegui il test per verificare il fallimento**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/web
pnpm test --run src/components/layout/QuickView/__tests__/RulesContent.test.tsx
```

Expected: FAIL — `RulesContent` renders solo il placeholder, non fa fetch.

- [ ] **Step 3: Implementa RulesContent**

Sostituisci integralmente `apps/web/src/components/layout/QuickView/RulesContent.tsx`:

```tsx
'use client';

import { useState, useEffect } from 'react';

import { BookOpen, Trophy, Zap, Layers } from 'lucide-react';

import { createHttpClient } from '@/lib/api/core/httpClient';
import { createSharedGamesClient } from '@/lib/api/clients/sharedGamesClient';
import { getCachedAnalyses, cacheRulebookAnalyses } from '@/lib/game-night/rules-cache';
import type { RulebookAnalysisDto } from '@/lib/api/schemas/shared-games.schemas';

interface RulesContentProps {
  gameId: string | null;
}

export function RulesContent({ gameId }: RulesContentProps) {
  const [analysis, setAnalysis] = useState<RulebookAnalysisDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);

  useEffect(() => {
    if (!gameId) return;

    // Try cache first
    const cached = getCachedAnalyses(gameId);
    if (cached && cached.analyses.length > 0) {
      setAnalysis(cached.analyses[0]);
      return;
    }

    // Fetch from API
    setLoading(true);
    setError(false);

    const client = createSharedGamesClient({ httpClient: createHttpClient() });
    client
      .getGameAnalysis(gameId)
      .then(analyses => {
        if (analyses.length > 0) {
          cacheRulebookAnalyses(gameId, analyses);
          setAnalysis(analyses[0]);
        }
      })
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }, [gameId]);

  if (!gameId) {
    return (
      <div data-testid="rules-content" className="flex flex-col h-full">
        <div className="flex flex-col items-center justify-center py-8 text-center">
          <BookOpen className="h-8 w-8 text-muted-foreground/40 mb-3" />
          <p className="text-sm text-muted-foreground">Seleziona un gioco per vederne le regole</p>
        </div>
      </div>
    );
  }

  if (loading) {
    return (
      <div data-testid="rules-content" className="flex flex-col h-full">
        <div data-testid="rules-loading" className="flex items-center justify-center py-8">
          <div className="h-5 w-5 animate-spin rounded-full border-2 border-primary border-t-transparent" />
        </div>
      </div>
    );
  }

  if (error || !analysis) {
    return (
      <div data-testid="rules-content" className="flex flex-col h-full">
        <div className="flex flex-col items-center justify-center py-8 text-center">
          <BookOpen className="h-8 w-8 text-muted-foreground/40 mb-3" />
          <p className="text-sm text-muted-foreground">
            Regole non disponibili per questo gioco.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div data-testid="rules-content" className="space-y-4 text-sm">
      {/* Summary */}
      <section>
        <p className="text-muted-foreground leading-relaxed">{analysis.summary}</p>
      </section>

      {/* Victory conditions */}
      {analysis.victoryConditions && (
        <section>
          <div className="flex items-center gap-1.5 mb-1.5 font-semibold text-foreground">
            <Trophy className="h-3.5 w-3.5 text-amber-500" />
            <span>Come si vince</span>
          </div>
          <p className="text-muted-foreground">{analysis.victoryConditions.primary}</p>
        </section>
      )}

      {/* Key mechanics */}
      {analysis.keyMechanics.length > 0 && (
        <section>
          <div className="flex items-center gap-1.5 mb-1.5 font-semibold text-foreground">
            <Zap className="h-3.5 w-3.5 text-blue-500" />
            <span>Meccaniche</span>
          </div>
          <div className="flex flex-wrap gap-1">
            {analysis.keyMechanics.map(m => (
              <span
                key={m}
                className="px-2 py-0.5 rounded-full bg-muted text-muted-foreground text-xs"
              >
                {m}
              </span>
            ))}
          </div>
        </section>
      )}

      {/* Game phases */}
      {analysis.gamePhases.length > 0 && (
        <section>
          <div className="flex items-center gap-1.5 mb-1.5 font-semibold text-foreground">
            <Layers className="h-3.5 w-3.5 text-purple-500" />
            <span>Fasi di gioco</span>
          </div>
          <ol className="space-y-1">
            {analysis.gamePhases
              .sort((a, b) => a.order - b.order)
              .map(phase => (
                <li key={phase.name} className="text-muted-foreground">
                  <span className="font-medium text-foreground">{phase.name}</span>
                  {' — '}
                  {phase.description}
                  {phase.isOptional && (
                    <span className="ml-1 text-xs text-muted-foreground">(opzionale)</span>
                  )}
                </li>
              ))}
          </ol>
        </section>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Esegui i test**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/web
pnpm test --run src/components/layout/QuickView/__tests__/RulesContent.test.tsx
```

Expected: tutti i test PASS.

- [ ] **Step 5: Commit**

```bash
git -C D:/Repositories/meepleai-monorepo-backend add \
  apps/web/src/components/layout/QuickView/RulesContent.tsx \
  apps/web/src/components/layout/QuickView/__tests__/RulesContent.test.tsx
git -C D:/Repositories/meepleai-monorepo-backend commit -m "feat(quickview): wire RulesContent to shared-games analysis API with localStorage cache"
```

---

### Task 2: Wire FaqContent alle API

**Files:**
- Modify: `apps/web/src/components/layout/QuickView/FaqContent.tsx`
- Create: `apps/web/src/components/layout/QuickView/__tests__/FaqContent.test.tsx`

- [ ] **Step 1: Scrivi il test fallente**

Crea `apps/web/src/components/layout/QuickView/__tests__/FaqContent.test.tsx`:

```tsx
import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/lib/game-night/rules-cache', () => ({
  getCachedAnalyses: vi.fn().mockReturnValue(null),
  cacheRulebookAnalyses: vi.fn(),
}));

vi.mock('@/lib/api/clients/sharedGamesClient', () => ({
  createSharedGamesClient: vi.fn(() => ({
    getGameAnalysis: vi.fn().mockResolvedValue([
      {
        id: 'a1',
        sharedGameId: 'g1',
        gameTitle: 'Catan',
        summary: 'Test',
        keyMechanics: [],
        victoryConditions: null,
        resources: [],
        gamePhases: [],
        commonQuestions: ['Posso costruire durante il turno altrui?'],
        generatedFaqs: [
          {
            question: 'Come funziona il ladrone?',
            answer: 'Il ladrone blocca la produzione.',
            sourceSection: 'Regole base',
            confidence: 0.95,
            tags: ['ladrone'],
          },
        ],
        confidenceScore: 0.9,
        version: '1',
        isActive: true,
        source: 'LLM',
        analyzedAt: '2026-01-01T00:00:00Z',
        createdBy: 'user1',
        keyConcepts: [],
        completionStatus: 'Complete',
      },
    ]),
  })),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  createHttpClient: vi.fn(() => ({})),
}));

import { FaqContent } from '../FaqContent';

describe('FaqContent', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('mostra placeholder quando gameId è null', () => {
    render(<FaqContent gameId={null} />);
    expect(screen.getByText(/seleziona un gioco/i)).toBeInTheDocument();
  });

  it('mostra loading spinner durante fetch', () => {
    render(<FaqContent gameId="g1" />);
    expect(screen.getByTestId('faq-loading')).toBeInTheDocument();
  });

  it('mostra le FAQ generate', async () => {
    render(<FaqContent gameId="g1" />);
    await waitFor(() => {
      expect(screen.getByText('Come funziona il ladrone?')).toBeInTheDocument();
      expect(screen.getByText('Il ladrone blocca la produzione.')).toBeInTheDocument();
    });
  });

  it('mostra le commonQuestions come FAQ semplici', async () => {
    render(<FaqContent gameId="g1" />);
    await waitFor(() => {
      expect(screen.getByText('Posso costruire durante il turno altrui?')).toBeInTheDocument();
    });
  });

  it('usa la cache se disponibile', async () => {
    const { getCachedAnalyses } = await import('@/lib/game-night/rules-cache');
    vi.mocked(getCachedAnalyses).mockReturnValue({
      analyses: [
        {
          id: 'a1',
          sharedGameId: 'g1',
          gameTitle: 'Catan',
          summary: 'Test',
          keyMechanics: [],
          victoryConditions: null,
          resources: [],
          gamePhases: [],
          commonQuestions: [],
          generatedFaqs: [
            {
              question: 'Da cache?',
              answer: 'Sì.',
              sourceSection: 'X',
              confidence: 1,
              tags: [],
            },
          ],
          confidenceScore: 0.9,
          version: '1',
          isActive: true,
          source: 'LLM',
          analyzedAt: '2026-01-01T00:00:00Z',
          createdBy: 'user1',
          keyConcepts: [],
          completionStatus: 'Complete',
        } as any,
      ],
      cachedAt: new Date().toISOString(),
      expiresAt: new Date(Date.now() + 3600000).toISOString(),
      gameTitle: 'Catan',
    });

    render(<FaqContent gameId="g1" />);
    await waitFor(() => {
      expect(screen.getByText('Da cache?')).toBeInTheDocument();
    });
  });
});
```

- [ ] **Step 2: Esegui il test per verificare il fallimento**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/web
pnpm test --run src/components/layout/QuickView/__tests__/FaqContent.test.tsx
```

Expected: FAIL.

- [ ] **Step 3: Implementa FaqContent**

Sostituisci integralmente `apps/web/src/components/layout/QuickView/FaqContent.tsx`:

```tsx
'use client';

import { useState, useEffect } from 'react';

import { HelpCircle, ChevronDown, ChevronRight } from 'lucide-react';

import { createHttpClient } from '@/lib/api/core/httpClient';
import { createSharedGamesClient } from '@/lib/api/clients/sharedGamesClient';
import { getCachedAnalyses, cacheRulebookAnalyses } from '@/lib/game-night/rules-cache';
import type { RulebookAnalysisDto } from '@/lib/api/schemas/shared-games.schemas';

interface FaqContentProps {
  gameId: string | null;
}

interface FaqItem {
  question: string;
  answer: string;
}

export function FaqContent({ gameId }: FaqContentProps) {
  const [faqs, setFaqs] = useState<FaqItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [openIndex, setOpenIndex] = useState<number | null>(null);

  useEffect(() => {
    if (!gameId) return;

    // Try cache first
    const cached = getCachedAnalyses(gameId);
    if (cached && cached.analyses.length > 0) {
      setFaqs(buildFaqList(cached.analyses[0]));
      return;
    }

    setLoading(true);
    const client = createSharedGamesClient({ httpClient: createHttpClient() });
    client
      .getGameAnalysis(gameId)
      .then(analyses => {
        if (analyses.length > 0) {
          cacheRulebookAnalyses(gameId, analyses);
          setFaqs(buildFaqList(analyses[0]));
        }
      })
      .catch(() => {
        // Silently fail — empty list shown
      })
      .finally(() => setLoading(false));
  }, [gameId]);

  if (!gameId) {
    return (
      <div data-testid="faq-content" className="flex flex-col h-full">
        <div className="flex flex-col items-center justify-center py-8 text-center">
          <HelpCircle className="h-8 w-8 text-muted-foreground/40 mb-3" />
          <p className="text-sm text-muted-foreground">Seleziona un gioco per vederne le FAQ</p>
        </div>
      </div>
    );
  }

  if (loading) {
    return (
      <div data-testid="faq-content" className="flex flex-col h-full">
        <div data-testid="faq-loading" className="flex items-center justify-center py-8">
          <div className="h-5 w-5 animate-spin rounded-full border-2 border-primary border-t-transparent" />
        </div>
      </div>
    );
  }

  if (faqs.length === 0) {
    return (
      <div data-testid="faq-content" className="flex flex-col h-full">
        <div className="flex flex-col items-center justify-center py-8 text-center">
          <HelpCircle className="h-8 w-8 text-muted-foreground/40 mb-3" />
          <p className="text-sm text-muted-foreground">
            Nessuna FAQ disponibile per questo gioco.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div data-testid="faq-content" className="space-y-1 text-sm">
      {faqs.map((faq, i) => (
        <div key={i} className="border-b border-border last:border-0">
          <button
            onClick={() => setOpenIndex(openIndex === i ? null : i)}
            className="w-full flex items-start gap-2 py-2.5 text-left"
          >
            {openIndex === i ? (
              <ChevronDown className="h-4 w-4 mt-0.5 shrink-0 text-primary" />
            ) : (
              <ChevronRight className="h-4 w-4 mt-0.5 shrink-0 text-muted-foreground" />
            )}
            <span className="font-medium text-foreground">{faq.question}</span>
          </button>
          {openIndex === i && (
            <p className="ml-6 pb-2.5 text-muted-foreground leading-relaxed">{faq.answer}</p>
          )}
        </div>
      ))}
    </div>
  );
}

function buildFaqList(analysis: RulebookAnalysisDto): FaqItem[] {
  const items: FaqItem[] = [];

  // Prefer structured GeneratedFaqs
  for (const faq of analysis.generatedFaqs ?? []) {
    items.push({ question: faq.question, answer: faq.answer });
  }

  // Fall back to commonQuestions (no answer available)
  for (const q of analysis.commonQuestions ?? []) {
    if (!items.some(f => f.question === q)) {
      items.push({ question: q, answer: '—' });
    }
  }

  return items;
}
```

- [ ] **Step 4: Esegui i test**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/web
pnpm test --run src/components/layout/QuickView/__tests__/FaqContent.test.tsx
```

Expected: tutti i test PASS.

- [ ] **Step 5: Esegui suite completa QuickView**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/web
pnpm test --run src/components/layout/QuickView/
```

Expected: tutti PASS (nessuna regressione su QuickView.test.tsx e AIQuickViewContent.test.tsx).

- [ ] **Step 6: Commit**

```bash
git -C D:/Repositories/meepleai-monorepo-backend add \
  apps/web/src/components/layout/QuickView/FaqContent.tsx \
  apps/web/src/components/layout/QuickView/__tests__/FaqContent.test.tsx
git -C D:/Repositories/meepleai-monorepo-backend commit -m "feat(quickview): wire FaqContent to shared-games analysis API with collapsible FAQ list"
```

---

### Task 3: PR e chiusura

- [ ] **Step 1: Esegui typecheck e lint**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/web
pnpm typecheck && pnpm lint
```

Expected: nessun errore.

- [ ] **Step 2: Crea PR**

```bash
git -C D:/Repositories/meepleai-monorepo-backend push -u origin HEAD
gh pr create \
  --title "feat(quickview): wire RulesContent and FaqContent to shared-games analysis API" \
  --base main-dev \
  --body "$(cat <<'EOF'
## Summary
- `RulesContent.tsx`: fetches `GET /api/v1/shared-games/{gameId}/analysis`, caches in localStorage via `rules-cache.ts`, displays summary / key mechanics / game phases / victory conditions
- `FaqContent.tsx`: same fetch, displays `generatedFaqs` as accordion + `commonQuestions` as fallback
- Both components: cache-first strategy (no refetch if valid cache exists), spinner during load, graceful empty/error states

## Test plan
- [ ] `RulesContent.test.tsx`: 7 tests — placeholder, loading, summary, mechanics, phases, victory, cache-hit, error
- [ ] `FaqContent.test.tsx`: 5 tests — placeholder, loading, generatedFaqs, commonQuestions, cache-hit
- [ ] `pnpm typecheck && pnpm lint` passes
- [ ] `pnpm test --run src/components/layout/QuickView/` all green

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Self-Review

**Spec coverage:**
- ✅ RulesContent fetches real data (summary, mechanics, phases, victory) 
- ✅ FaqContent shows generatedFaqs + commonQuestions fallback
- ✅ Cache-first with rules-cache.ts
- ✅ Loading states
- ✅ Empty/error states

**Placeholder scan:** Nessun TODO/TBD/placeholder.

**Type consistency:** `RulebookAnalysisDto` usato coerentemente in entrambi i file; `buildFaqList` definito nello stesso file dove è usato.
