# Phase 5: Game Night — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a unified mobile-first game night experience: 3-step creation wizard, single-page play mode with 4 tabs, quick tools, score numpad, session summary, and guest view.

**Architecture:** Create `SessionWizardMobile` (replaces both sessions/new and game-nights/new), `PlayModeMobile` (replaces 5+ sub-pages with 4-tab single page), and `GuestSessionView` (public). Reuse existing tools (DiceRoller, CounterTool, CoinFlip, etc.), SSE hooks (useSessionSync, useSessionStream), Zustand stores (sessionStore, liveSessionStore), and API clients (liveSessions).

**Tech Stack:** Next.js 16, React 19, Tailwind CSS 4, Zustand, SSE streaming, qrcode.react, Phase 1 components (SessionBottomNav, BottomSheet, GlassCard, GradientButton)

**Spec:** `docs/superpowers/specs/2026-03-28-user-pages-redesign-design.md` — Section 5

---

## File Structure

### New Files

| File | Responsibility |
|------|---------------|
| `src/app/(authenticated)/sessions/new/session-wizard-mobile.tsx` | 3-step wizard: choose game → invite players → start |
| `src/app/(authenticated)/sessions/live/[sessionId]/play-mode-mobile.tsx` | Single-page play mode with 4 tabs |
| `src/app/(public)/join/[code]/page.tsx` | Guest view (public, no auth required) |
| `src/components/session/QuickToolBar.tsx` | Horizontal scrollable tool icons |
| `src/components/session/QuickToolBar.test.tsx` | Tests |
| `src/components/session/ScoreNumpad.tsx` | One-hand score input numpad |
| `src/components/session/ScoreNumpad.test.tsx` | Tests |
| `src/components/session/SessionSummaryCard.tsx` | End-of-session ranking and stats |
| `src/components/session/QrInviteSheet.tsx` | Bottom sheet with QR code + share link |

### Modified Files

| File | Changes |
|------|---------|
| `src/app/(authenticated)/sessions/new/page.tsx` | Switch to SessionWizardMobile on mobile |
| `src/app/(authenticated)/sessions/live/[sessionId]/page.tsx` | Switch to PlayModeMobile on mobile |

### All paths relative to `apps/web/`

---

## Task 1: Create QuickToolBar and ScoreNumpad

**Files:**
- Create: `src/components/session/QuickToolBar.tsx` + test
- Create: `src/components/session/ScoreNumpad.tsx` + test

- [ ] **Step 1: Write QuickToolBar test**

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { QuickToolBar } from './QuickToolBar';

describe('QuickToolBar', () => {
  it('renders all tool icons', () => {
    render(<QuickToolBar activeTool={null} onSelectTool={() => {}} />);
    expect(screen.getByLabelText(/dadi/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/moneta/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/timer/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/contatore/i)).toBeInTheDocument();
  });

  it('highlights active tool', () => {
    render(<QuickToolBar activeTool="dice" onSelectTool={() => {}} />);
    const diceBtn = screen.getByLabelText(/dadi/i);
    expect(diceBtn).toHaveClass('bg-amber-500/20');
  });

  it('calls onSelectTool when clicked', () => {
    const onSelect = vi.fn();
    render(<QuickToolBar activeTool={null} onSelectTool={onSelect} />);
    fireEvent.click(screen.getByLabelText(/dadi/i));
    expect(onSelect).toHaveBeenCalledWith('dice');
  });
});
```

- [ ] **Step 2: Write QuickToolBar**

```tsx
'use client';

import React from 'react';
import { cn } from '@/lib/utils';
import { Dice5, Circle, Timer, Hash, Layers } from 'lucide-react';

export type ToolId = 'dice' | 'coin' | 'timer' | 'counter' | 'cards';

interface ToolDef {
  id: ToolId;
  label: string;
  icon: React.ElementType;
}

const tools: ToolDef[] = [
  { id: 'dice', label: 'Dadi', icon: Dice5 },
  { id: 'coin', label: 'Moneta', icon: Circle },
  { id: 'timer', label: 'Timer', icon: Timer },
  { id: 'counter', label: 'Contatore', icon: Hash },
  { id: 'cards', label: 'Carte', icon: Layers },
];

export interface QuickToolBarProps {
  activeTool: ToolId | null;
  onSelectTool: (tool: ToolId) => void;
}

export function QuickToolBar({ activeTool, onSelectTool }: QuickToolBarProps) {
  return (
    <div className="flex gap-2 overflow-x-auto px-4 py-2 scrollbar-none">
      {tools.map((tool) => {
        const Icon = tool.icon;
        const active = activeTool === tool.id;
        return (
          <button
            key={tool.id}
            type="button"
            aria-label={tool.label}
            onClick={() => onSelectTool(active ? null! : tool.id)}
            className={cn(
              'flex shrink-0 flex-col items-center gap-1 rounded-lg px-3 py-2 transition-colors',
              active
                ? 'bg-amber-500/20 text-amber-400 border border-amber-500/30'
                : 'bg-white/5 text-[var(--gaming-text-secondary)] border border-transparent'
            )}
          >
            <Icon className="h-5 w-5" />
            <span className="text-[10px] font-medium">{tool.label}</span>
          </button>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 3: Write ScoreNumpad test**

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ScoreNumpad } from './ScoreNumpad';

describe('ScoreNumpad', () => {
  it('renders number buttons 0-9', () => {
    render(<ScoreNumpad playerName="Marco" onSubmit={() => {}} onClose={() => {}} />);
    for (let i = 0; i <= 9; i++) {
      expect(screen.getByRole('button', { name: String(i) })).toBeInTheDocument();
    }
  });

  it('displays player name', () => {
    render(<ScoreNumpad playerName="Sara" onSubmit={() => {}} onClose={() => {}} />);
    expect(screen.getByText('Sara')).toBeInTheDocument();
  });

  it('builds number from button presses', () => {
    const onSubmit = vi.fn();
    render(<ScoreNumpad playerName="Marco" onSubmit={onSubmit} onClose={() => {}} />);
    fireEvent.click(screen.getByRole('button', { name: '4' }));
    fireEvent.click(screen.getByRole('button', { name: '2' }));
    fireEvent.click(screen.getByRole('button', { name: /conferma/i }));
    expect(onSubmit).toHaveBeenCalledWith(42);
  });
});
```

- [ ] **Step 4: Write ScoreNumpad**

```tsx
'use client';

import React, { useState } from 'react';
import { cn } from '@/lib/utils';
import { Delete, Check } from 'lucide-react';

export interface ScoreNumpadProps {
  playerName: string;
  currentScore?: number;
  onSubmit: (value: number) => void;
  onClose: () => void;
}

export function ScoreNumpad({ playerName, currentScore, onSubmit, onClose }: ScoreNumpadProps) {
  const [input, setInput] = useState('');

  const handleDigit = (digit: string) => setInput((prev) => prev + digit);
  const handleDelete = () => setInput((prev) => prev.slice(0, -1));
  const handleSubmit = () => {
    const value = parseInt(input || '0', 10);
    onSubmit(value);
    onClose();
  };

  const digits = ['1', '2', '3', '4', '5', '6', '7', '8', '9'];

  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="text-center">
        <p className="text-sm font-medium text-[var(--gaming-text-secondary)]">{playerName}</p>
        <p className="text-3xl font-bold text-[var(--gaming-text-primary)] tabular-nums">
          {input || '0'}
        </p>
        {currentScore !== undefined && (
          <p className="text-xs text-[var(--gaming-text-secondary)]">
            Attuale: {currentScore}
          </p>
        )}
      </div>

      <div className="grid grid-cols-3 gap-2">
        {digits.map((d) => (
          <button
            key={d}
            type="button"
            onClick={() => handleDigit(d)}
            className={cn(
              'flex h-14 items-center justify-center rounded-lg text-xl font-medium',
              'bg-white/5 text-[var(--gaming-text-primary)]',
              'active:bg-white/10 transition-colors'
            )}
          >
            {d}
          </button>
        ))}
        <button
          type="button"
          onClick={handleDelete}
          aria-label="Cancella"
          className="flex h-14 items-center justify-center rounded-lg bg-white/5 text-[var(--gaming-text-secondary)] active:bg-white/10"
        >
          <Delete className="h-5 w-5" />
        </button>
        <button
          type="button"
          onClick={() => handleDigit('0')}
          className="flex h-14 items-center justify-center rounded-lg bg-white/5 text-xl font-medium text-[var(--gaming-text-primary)] active:bg-white/10"
        >
          0
        </button>
        <button
          type="button"
          onClick={handleSubmit}
          aria-label="Conferma"
          className="flex h-14 items-center justify-center rounded-lg gradient-primary"
        >
          <Check className="h-5 w-5" />
        </button>
      </div>
    </div>
  );
}
```

- [ ] **Step 5: Run tests, verify, commit**

```bash
cd apps/web && pnpm vitest run src/components/session/QuickToolBar.test.tsx src/components/session/ScoreNumpad.test.tsx
npx tsc --noEmit
git add apps/web/src/components/session/QuickToolBar* apps/web/src/components/session/ScoreNumpad*
git commit -m "feat(session): add QuickToolBar and ScoreNumpad"
```

---

## Task 2: Create QrInviteSheet and SessionSummaryCard

**Files:**
- Create: `src/components/session/QrInviteSheet.tsx`
- Create: `src/components/session/SessionSummaryCard.tsx`

- [ ] **Step 1: Check if qrcode.react is in package.json**

Read `apps/web/package.json` and search for `qrcode`. The existing `InviteModal` uses `qrcode.react` (QRCodeSVG).

- [ ] **Step 2: Create QrInviteSheet**

```tsx
'use client';

import React from 'react';
import { BottomSheet } from '@/components/ui/overlays/BottomSheet';
import { Copy, Check } from 'lucide-react';
import { QRCodeSVG } from 'qrcode.react';

export interface QrInviteSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  sessionCode: string;
  shareLink: string;
}

export function QrInviteSheet({ open, onOpenChange, sessionCode, shareLink }: QrInviteSheetProps) {
  const [copied, setCopied] = React.useState(false);

  const handleCopy = async () => {
    await navigator.clipboard.writeText(shareLink);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <BottomSheet open={open} onOpenChange={onOpenChange} title="Invita Giocatori">
      <div className="flex flex-col items-center gap-4">
        <div className="rounded-xl bg-white p-4">
          <QRCodeSVG value={shareLink} size={180} />
        </div>

        <div className="text-center">
          <p className="text-xs text-[var(--gaming-text-secondary)]">Codice sessione</p>
          <p className="font-mono text-2xl font-bold tracking-widest text-amber-400">
            {sessionCode}
          </p>
        </div>

        <button
          type="button"
          onClick={handleCopy}
          className="flex items-center gap-2 rounded-lg bg-white/5 px-4 py-2 text-sm text-[var(--gaming-text-secondary)] transition-colors hover:bg-white/10"
        >
          {copied ? <Check className="h-4 w-4 text-green-400" /> : <Copy className="h-4 w-4" />}
          {copied ? 'Copiato!' : 'Copia Link'}
        </button>
      </div>
    </BottomSheet>
  );
}
```

- [ ] **Step 3: Create SessionSummaryCard**

```tsx
'use client';

import React from 'react';
import { GlassCard } from '@/components/ui/surfaces/GlassCard';
import { GradientButton } from '@/components/ui/buttons/GradientButton';
import { Trophy, Clock, Dice5, RotateCcw } from 'lucide-react';
import Link from 'next/link';

interface PlayerResult {
  name: string;
  score: number;
  color: string;
  rank: number;
}

export interface SessionSummaryCardProps {
  gameName: string;
  players: PlayerResult[];
  durationMinutes: number;
  onPlayAgain?: () => void;
}

const medals = ['🥇', '🥈', '🥉'];

export function SessionSummaryCard({ gameName, players, durationMinutes, onPlayAgain }: SessionSummaryCardProps) {
  const sorted = [...players].sort((a, b) => a.rank - b.rank);

  return (
    <div className="flex flex-col gap-4 px-4 py-6">
      <div className="text-center">
        <Trophy className="mx-auto mb-2 h-8 w-8 text-amber-400" />
        <h2 className="text-lg font-bold text-[var(--gaming-text-primary)]">Partita Terminata!</h2>
        <p className="text-sm text-[var(--gaming-text-secondary)]">{gameName}</p>
      </div>

      <GlassCard className="p-4">
        <div className="flex flex-col gap-3">
          {sorted.map((player) => (
            <div key={player.name} className="flex items-center gap-3">
              <span className="w-6 text-center text-lg">
                {player.rank <= 3 ? medals[player.rank - 1] : `${player.rank}.`}
              </span>
              <div
                className="h-8 w-8 shrink-0 rounded-full"
                style={{ backgroundColor: player.color }}
              />
              <span className="flex-1 text-sm font-medium text-[var(--gaming-text-primary)]">
                {player.name}
              </span>
              <span className="tabular-nums text-sm font-bold text-amber-400">
                {player.score}
              </span>
            </div>
          ))}
        </div>
      </GlassCard>

      <div className="flex items-center justify-center gap-4 text-xs text-[var(--gaming-text-secondary)]">
        <span className="flex items-center gap-1">
          <Clock className="h-3.5 w-3.5" />
          {durationMinutes} min
        </span>
      </div>

      <div className="flex flex-col gap-2">
        {onPlayAgain && (
          <GradientButton fullWidth onClick={onPlayAgain}>
            <RotateCcw className="h-4 w-4" />
            Gioca Ancora
          </GradientButton>
        )}
        <Link
          href="/dashboard"
          className="py-2 text-center text-sm text-[var(--gaming-text-secondary)] hover:text-[var(--gaming-text-primary)]"
        >
          Torna alla Home
        </Link>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Verify, commit**

```bash
npx tsc --noEmit
git add apps/web/src/components/session/QrInviteSheet.tsx apps/web/src/components/session/SessionSummaryCard.tsx
git commit -m "feat(session): add QrInviteSheet and SessionSummaryCard"
```

---

## Task 3: Create SessionWizardMobile

**Files:**
- Create: `src/app/(authenticated)/sessions/new/session-wizard-mobile.tsx`
- Modify: `src/app/(authenticated)/sessions/new/page.tsx`

- [ ] **Step 1: Read existing wizard and API**

Read:
- `src/components/game-night/GameNightWizard.tsx` — understand existing 3-step flow
- `src/components/game-night/PlayerSetup.tsx` — understand player management
- `src/lib/api/clients/liveSessionsClient.ts` — createSession, addPlayer signatures
- `src/app/(authenticated)/sessions/new/page.tsx` — current page structure

- [ ] **Step 2: Create session-wizard-mobile.tsx**

A 3-step swipeable wizard:

**Step 1 — Choose Game:** Grid of library games (MeepleCard compact), search to filter. Uses `useLibrary` hook. Multi-select support for multi-game nights.

**Step 2 — Invite Players:** Add players (name + color). QR code invite sheet. Recent players quick-add.

**Step 3 — Ready:** Summary + "Inizia a Giocare" GradientButton.

On start: calls `api.liveSessions.createSession()` → adds players → navigates to `/sessions/live/{sessionId}`.

Use `GlassCard` for step containers, progress dots at top, swipe between steps.

The component should:
- Import MeepleCard, GlassCard, GradientButton, MobileHeader, QrInviteSheet
- Import useLibrary for game selection
- Import api.liveSessions for session creation
- Manage local state for: currentStep, selectedGames, players, sessionCode

IMPORTANT: Read the actual `createSession` and `addPlayer` request types from the schemas. Use correct field names.

- [ ] **Step 3: Update page.tsx to render SessionWizardMobile on mobile**

- [ ] **Step 4: Verify, commit**

```bash
npx tsc --noEmit
git add apps/web/src/app/\(authenticated\)/sessions/new/
git commit -m "feat(session): add 3-step SessionWizardMobile"
```

---

## Task 4: Create PlayModeMobile

**Files:**
- Create: `src/app/(authenticated)/sessions/live/[sessionId]/play-mode-mobile.tsx`
- Modify: `src/app/(authenticated)/sessions/live/[sessionId]/page.tsx`

This is the most complex component — a single page with 4 tabs managed by `SessionBottomNav`.

- [ ] **Step 1: Read existing live session components and hooks**

Read:
- `src/lib/stores/sessionStore.ts` — session store actions
- `src/lib/stores/live-session-store.ts` — live session state
- `src/lib/domain-hooks/useSessionSync.ts` — SSE connection
- `src/components/session/DiceRoller.tsx` — props interface
- `src/components/session/ActivityFeed.tsx` — props/usage
- `src/components/game-night/LiveScoreboard.tsx` — props interface

- [ ] **Step 2: Create play-mode-mobile.tsx**

Props: `sessionId: string`

**Structure:**
1. Load session via sessionStore.loadSession(sessionId)
2. Connect SSE via useSessionSync
3. SessionBottomNav controls activeTab state
4. Render tab content based on activeTab:

**Tab: Gioco** (default)
- Game timer (elapsed time from store)
- QuickToolBar → opens BottomSheet with selected tool (DiceRoller, CoinFlip, CounterTool, etc.)
- ActivityFeed below tools
- FAB "Chiedi Regola" → link to `/chat?gameId=...`

**Tab: Punteggi**
- LiveScoreboard (restyled with glass)
- Tap on player score → BottomSheet with ScoreNumpad
- Use `sessionStore.recordScore()` on submit

**Tab: Chiedi**
- Embed ChatMobile (from Phase 4) with session context
- Or simple link to `/chat?gameId=...` for v1

**Tab: Giocatori**
- Player list from session store
- Current turn indicator
- "Aggiungi giocatore" button → addPlayer API

**End session:** "Termina Partita" in header menu → confirmation → SessionSummaryCard

IMPORTANT: Read actual store interfaces and SSE hooks to use correct field names and methods.

- [ ] **Step 3: Update page.tsx to render PlayModeMobile on mobile**

- [ ] **Step 4: Verify, commit**

```bash
npx tsc --noEmit
git add apps/web/src/app/\(authenticated\)/sessions/live/
git commit -m "feat(session): add PlayModeMobile with 4-tab layout"
```

---

## Task 5: Create Guest View

**Files:**
- Create: `src/app/(public)/join/[code]/page.tsx` (or update existing route)

- [ ] **Step 1: Check existing join route**

Search for existing `/join/` route: `find src/app -path "*/join*" -name "page.tsx"`

- [ ] **Step 2: Create or update guest view page**

A public page (no auth required) that:
1. Gets session by code via `api.liveSessions.getByCode(code)`
2. Shows read-only view: game name, live scoreboard, activity feed
3. No auth required — minimal landing page
4. Gaming styling with GlassCard

- [ ] **Step 3: Verify, commit**

```bash
npx tsc --noEmit
git add apps/web/src/app/
git commit -m "feat(session): add guest view for QR code joiners"
```

---

## Task 6: Integration Verification

- [ ] **Step 1: Run all new tests**

```bash
cd apps/web && pnpm vitest run src/components/session/QuickToolBar.test.tsx src/components/session/ScoreNumpad.test.tsx
```

- [ ] **Step 2: Typecheck + lint**

```bash
cd apps/web && npx tsc --noEmit && pnpm lint
```

---

## Summary

| Task | Component | Tests | Status |
|------|-----------|-------|--------|
| 1 | QuickToolBar + ScoreNumpad | 6 | ☐ |
| 2 | QrInviteSheet + SessionSummaryCard | 0 (visual) | ☐ |
| 3 | SessionWizardMobile + page wiring | 0 (integration) | ☐ |
| 4 | PlayModeMobile + page wiring | 0 (integration) | ☐ |
| 5 | Guest View | 0 (integration) | ☐ |
| 6 | Integration verification | — | ☐ |

**Total new tests: 6**
**Total new files: 9**
**Total modified files: 2**

**Key reuse**: `DiceRoller`, `CounterTool`, `CoinFlip`, `CardDeck`, `CountdownTimer`, `LiveScoreboard`, `PlayerSetup`, `ActivityFeed`, `useSessionSync`, `useSessionStream`, `sessionStore`, `liveSessionStore`, `api.liveSessions`, `QRCodeSVG`, `SessionBottomNav`, `BottomSheet`, `GlassCard`, `GradientButton`, `MobileHeader`, `ChatMobile`
