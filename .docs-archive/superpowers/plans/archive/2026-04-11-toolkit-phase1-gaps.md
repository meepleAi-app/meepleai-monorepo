# Toolkit Phase 1 — Gap Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Chiudere i 2 gap reali della spec `toolkit-game-utilities.md` Phase 1: (1) Timer mancante nella standalone play page, (2) suono + vibrazione mancante nel componente Timer.

**Architecture:** Tutti i fix sono frontend-only. Il componente `Timer.tsx` viene esteso con `AudioContext` + `navigator.vibrate()`. La `toolkit/play/page.tsx` viene aggiornata per renderizzare i timer da `DEFAULT_TOOLKIT`.

**Tech Stack:** React 19, Next.js 16, Vitest, TypeScript

---

## Review Open Items (stato pre-implementazione)

| OI | Stato | Note |
|----|-------|------|
| OI-4 Schema localStorage `StandaloneToolLog` | ✅ DONE | `ToolLogEntry` + `toolkit-log.ts` + tests |
| OI-5 TimerTool offline fallback | ✅ DEFERRED Phase 2 | Comment in `Timer.tsx` corretto |
| OI-6 `POST /api/v1/sessions/{id}/tool-events` | ✅ DONE | `POST /game-sessions/{id}/events` in `SessionPlayerActionsEndpoints.cs` + `useSessionToolLog` |
| OI-7 Player dropdown Phase 1 | ✅ DONE | Free-text "Chi gioca?" in `play/page.tsx` |

**Gap reali:**
1. `apps/web/src/app/(authenticated)/toolkit/play/page.tsx` non renderizza i timer di `DEFAULT_TOOLKIT`
2. `apps/web/src/components/toolkit/Timer.tsx` — spec dice "Suono + vibrazione a 10s dal termine" ma solo colore rosso attuale

---

## File Map

| File | Azione | Responsabilità |
|------|--------|----------------|
| `apps/web/src/components/toolkit/Timer.tsx` | Modifica | Aggiunge suono (AudioContext) + vibrazione (navigator.vibrate) a 10s |
| `apps/web/src/app/(authenticated)/toolkit/play/page.tsx` | Modifica | Aggiunge sezione Timer con DEFAULT_TOOLKIT.timers |
| `apps/web/src/components/toolkit/__tests__/Timer.test.tsx` | Crea | Unit test suono + vibrazione |

---

## Task 1: Aggiungere suono + vibrazione al Timer

**Files:**
- Modify: `apps/web/src/components/toolkit/Timer.tsx`
- Create: `apps/web/src/components/toolkit/__tests__/Timer.test.tsx`

- [ ] **Step 1: Scrivi il test che fallisce**

Crea `apps/web/src/components/toolkit/__tests__/Timer.test.tsx`:

```tsx
import { render, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { Timer } from '../Timer';

// Stub AudioContext
const mockOscillatorStart = vi.fn();
const mockOscillatorStop = vi.fn();
const mockOscillatorConnect = vi.fn();
const mockGainDisconnect = vi.fn();
const mockGainConnect = vi.fn();
const mockContextClose = vi.fn();

function createAudioContextMock() {
  const gainNode = {
    gain: { setValueAtTime: vi.fn(), linearRampToValueAtTime: vi.fn() },
    connect: mockGainConnect,
    disconnect: mockGainDisconnect,
  };
  const oscillator = {
    type: 'sine',
    frequency: { setValueAtTime: vi.fn() },
    connect: mockOscillatorConnect,
    start: mockOscillatorStart,
    stop: mockOscillatorStop,
  };
  return {
    createOscillator: vi.fn().mockReturnValue(oscillator),
    createGain: vi.fn().mockReturnValue(gainNode),
    destination: {},
    currentTime: 0,
    close: mockContextClose,
  };
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('AudioContext', vi.fn().mockImplementation(createAudioContextMock));
  vi.stubGlobal('navigator', { vibrate: vi.fn() });
});

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe('Timer — suono + vibrazione a 10s', () => {
  it('chiama navigator.vibrate quando il countdown raggiunge 10s', () => {
    // 12s timer: avanza a 10s remaining → deve vibrare
    const { getByRole } = render(
      <Timer name="Test" defaultSeconds={12} type="countdown" />
    );
    // Avvia il timer
    act(() => { getByRole('button', { name: /avvia/i }).click(); });
    // Avanza di 2 secondi → rimangono 10s
    act(() => { vi.advanceTimersByTime(2000); });
    expect(navigator.vibrate).toHaveBeenCalledWith([200, 100, 200]);
  });

  it('NON vibra se il tipo è countup', () => {
    const { getByRole } = render(
      <Timer name="Test" defaultSeconds={0} type="countup" />
    );
    act(() => { getByRole('button', { name: /avvia/i }).click(); });
    act(() => { vi.advanceTimersByTime(5000); });
    expect(navigator.vibrate).not.toHaveBeenCalled();
  });

  it('NON vibra una seconda volta se già vibrato', () => {
    const { getByRole } = render(
      <Timer name="Test" defaultSeconds={12} type="countdown" />
    );
    act(() => { getByRole('button', { name: /avvia/i }).click(); });
    act(() => { vi.advanceTimersByTime(2000); }); // → 10s
    act(() => { vi.advanceTimersByTime(1000); }); // → 9s
    // vibrate deve essere chiamato UNA SOLA VOLTA
    expect(navigator.vibrate).toHaveBeenCalledTimes(1);
  });

  it('ripristina alertFired dopo reset', () => {
    const { getByRole } = render(
      <Timer name="Test" defaultSeconds={12} type="countdown" />
    );
    act(() => { getByRole('button', { name: /avvia/i }).click(); });
    act(() => { vi.advanceTimersByTime(2000); }); // → 10s, vibra
    act(() => { getByRole('button', { name: /reset/i }).click(); });
    act(() => { getByRole('button', { name: /avvia/i }).click(); });
    act(() => { vi.advanceTimersByTime(2000); }); // → 10s di nuovo, vibra una seconda volta
    expect(navigator.vibrate).toHaveBeenCalledTimes(2);
  });
});
```

- [ ] **Step 2: Verifica che il test fallisca**

```bash
cd apps/web && pnpm test --run src/components/toolkit/__tests__/Timer.test.tsx
```
Atteso: 3 FAIL (navigator.vibrate non esiste/non viene chiamato)

- [ ] **Step 3: Implementa suono + vibrazione in Timer.tsx**

Sostituisci completamente `apps/web/src/components/toolkit/Timer.tsx`:

```tsx
'use client';

import { useState, useEffect, useRef, useCallback } from 'react';

import { Button } from '@/components/ui/primitives/button';

interface TimerProps {
  name: string;
  defaultSeconds: number;
  type: 'countdown' | 'countup' | 'turn';
  onAction?: (action: string, seconds: number) => void;
}

const WARNING_THRESHOLD = 10;

// ── Alert: suono + vibrazione ───────────────────────────────────────

function playAlertSound(): void {
  try {
    const ctx = new AudioContext();
    const oscillator = ctx.createOscillator();
    const gain = ctx.createGain();
    oscillator.connect(gain);
    gain.connect(ctx.destination);
    oscillator.type = 'sine';
    oscillator.frequency.setValueAtTime(880, ctx.currentTime);
    gain.gain.setValueAtTime(0.3, ctx.currentTime);
    gain.gain.linearRampToValueAtTime(0, ctx.currentTime + 0.4);
    oscillator.start(ctx.currentTime);
    oscillator.stop(ctx.currentTime + 0.4);
    // Close context after sound to release resources
    setTimeout(() => void ctx.close(), 600);
  } catch {
    // AudioContext unavailable (e.g. JSDOM) — silent fallback
  }
}

function triggerVibration(): void {
  try {
    navigator.vibrate?.([200, 100, 200]);
  } catch {
    // vibration API unavailable — silent fallback
  }
}

// ── Local timer strategy ────────────────────────────────────────────

function useLocalTimer(defaultSeconds: number) {
  const [seconds, setSeconds] = useState(defaultSeconds);
  const [running, setRunning] = useState(false);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const start = useCallback(() => {
    setRunning(true);
  }, []);

  const pause = useCallback(() => {
    setRunning(false);
  }, []);

  const reset = useCallback(() => {
    setRunning(false);
    setSeconds(defaultSeconds);
  }, [defaultSeconds]);

  useEffect(() => {
    if (!running) {
      if (intervalRef.current) clearInterval(intervalRef.current);
      return;
    }
    intervalRef.current = setInterval(() => {
      setSeconds(s => Math.max(0, s - 1));
    }, 1000);
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [running]);

  // Auto-stop at 0 (countdown)
  useEffect(() => {
    if (seconds === 0 && running) {
      setRunning(false);
    }
  }, [seconds, running]);

  return { seconds, running, start, pause, reset };
}

// ── Component ───────────────────────────────────────────────────────

export function Timer({ name, defaultSeconds, type, onAction }: TimerProps) {
  // Phase 1: always use local timer (standalone and in-session).
  // Phase 2: when in-session, SSE timer_tick events will drive the display.
  const { seconds, running, start, pause, reset } = useLocalTimer(defaultSeconds);

  // Track whether the 10s alert has already fired for this run
  const alertFiredRef = useRef(false);

  const isWarning = type === 'countdown' && seconds <= WARNING_THRESHOLD && seconds > 0;
  const isExpired = type === 'countdown' && seconds === 0;

  // Fire sound + vibration once when countdown crosses the warning threshold
  useEffect(() => {
    if (type !== 'countdown') return;
    if (!running) return;
    if (seconds === WARNING_THRESHOLD && !alertFiredRef.current) {
      alertFiredRef.current = true;
      playAlertSound();
      triggerVibration();
    }
  }, [seconds, running, type]);

  const handleStart = () => {
    start();
    onAction?.('start', seconds);
  };

  const handlePause = () => {
    pause();
    onAction?.('pause', seconds);
  };

  const handleReset = () => {
    reset();
    alertFiredRef.current = false; // allow alert to fire again on next run
    onAction?.('reset', defaultSeconds);
  };

  const formatTime = (s: number) => {
    const m = Math.floor(s / 60);
    const sec = s % 60;
    return `${String(m).padStart(2, '0')}:${String(sec).padStart(2, '0')}`;
  };

  return (
    <div className="space-y-3 rounded-lg border border-slate-200 bg-white p-4">
      <div className="text-center text-sm font-semibold text-slate-700">{name}</div>
      <div
        className={`text-center text-5xl font-bold tabular-nums transition-colors ${
          isWarning ? 'text-red-500' : isExpired ? 'text-slate-300' : 'text-slate-800'
        }`}
        data-testid="timer-display"
      >
        {formatTime(seconds)}
      </div>
      {isExpired && (
        <p className="text-center text-xs font-medium text-red-500">⏰ Tempo scaduto!</p>
      )}
      <div className="flex gap-2">
        {!running ? (
          <Button onClick={handleStart} className="flex-1" aria-label="Avvia timer">
            ▶ Avvia
          </Button>
        ) : (
          <Button
            onClick={handlePause}
            variant="outline"
            className="flex-1"
            aria-label="Pausa timer"
          >
            ⏸ Pausa
          </Button>
        )}
        <Button variant="outline" onClick={handleReset} aria-label="Reset timer">
          ↩ Reset
        </Button>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Verifica che i test passino**

```bash
cd apps/web && pnpm test --run src/components/toolkit/__tests__/Timer.test.tsx
```
Atteso: 4 PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/toolkit/Timer.tsx \
        apps/web/src/components/toolkit/__tests__/Timer.test.tsx
git commit -m "feat(toolkit): add sound + vibration alert at 10s warning threshold"
```

---

## Task 2: Aggiungere sezione Timer alla standalone play page

**Files:**
- Modify: `apps/web/src/app/(authenticated)/toolkit/play/page.tsx`

- [ ] **Step 1: Aggiorna la play page aggiungendo i timer**

La play page deve importare `Timer` da `@/components/toolkit/Timer` e renderizzare i timer di `DEFAULT_TOOLKIT`. Modifica il file in modo da aggiungere una sezione "Timer" tra "Dadi" e "Contatori".

La logica `addLog` per timer deve chiamarsi al `onAction` del componente.

Sostituisci completamente `apps/web/src/app/(authenticated)/toolkit/play/page.tsx`:

```tsx
'use client';

import { useState, useEffect } from 'react';

import { CounterTool } from '@/components/toolkit/CounterTool';
import { DiceRoller } from '@/components/toolkit/DiceRoller';
import { Randomizer } from '@/components/toolkit/Randomizer';
import { Timer } from '@/components/toolkit/Timer';
import { DEFAULT_TOOLKIT } from '@/lib/config/default-toolkit';
import type { ToolLogEntry } from '@/lib/types/standalone-toolkit';
import { appendToolLog, generateLogId, clearOldEntries } from '@/lib/utils/toolkit-log';

export default function ToolkitPlayPage() {
  const [log, setLog] = useState<ToolLogEntry[]>([]);
  const [actorLabel, setActorLabel] = useState('');

  useEffect(() => {
    clearOldEntries();
  }, []);

  const addLog = (entry: Omit<ToolLogEntry, 'id' | 'timestamp'>) => {
    const full: ToolLogEntry = {
      ...entry,
      id: generateLogId(),
      timestamp: new Date().toISOString(),
      actorLabel: actorLabel || undefined,
    };
    appendToolLog(full);
    setLog(prev => [...prev.slice(-49), full]);
  };

  return (
    <div className="mx-auto max-w-2xl space-y-6 p-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold text-slate-800">Toolkit</h1>
        <input
          type="text"
          value={actorLabel}
          onChange={e => setActorLabel(e.target.value)}
          placeholder="Chi gioca?"
          className="rounded-md border border-slate-200 px-3 py-1.5 text-sm placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-amber-400"
          maxLength={30}
        />
      </div>

      {/* Dadi */}
      <section>
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-400">Dadi</h2>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          {DEFAULT_TOOLKIT.dice.map(config => (
            <DiceRoller
              key={config.name}
              config={config}
              actorLabel={actorLabel}
              onRoll={result =>
                addLog({
                  toolType: 'dice',
                  action: 'roll',
                  result: `${config.name} → ${result.total}`,
                })
              }
            />
          ))}
        </div>
      </section>

      {/* Timer */}
      <section>
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-400">
          Timer
        </h2>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {DEFAULT_TOOLKIT.timers.map(config => (
            <Timer
              key={config.name}
              name={config.name}
              defaultSeconds={config.defaultSeconds}
              type={config.type}
              onAction={(action, seconds) =>
                addLog({
                  toolType: 'timer',
                  action,
                  result: `${config.name}: ${action} @ ${seconds}s`,
                })
              }
            />
          ))}
        </div>
      </section>

      {/* Contatori */}
      <section>
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-400">
          Contatori
        </h2>
        <div className="grid grid-cols-2 gap-3">
          {DEFAULT_TOOLKIT.counters.map(config => (
            <CounterTool
              key={config.id}
              id={config.id}
              name={config.name}
              initialValue={config.initialValue}
              min={config.min}
              max={config.max}
              onAction={(action, value) =>
                addLog({ toolType: 'counter', action, result: `${config.name}: ${value}` })
              }
            />
          ))}
        </div>
      </section>

      {/* Randomizzatore */}
      <section>
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-400">
          Randomizzatore
        </h2>
        <Randomizer
          onAction={extracted =>
            addLog({ toolType: 'randomizer', action: 'extract', result: extracted })
          }
        />
      </section>

      {/* Log */}
      {log.length > 0 && (
        <section>
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-400">
            Cronologia
          </h2>
          <div className="space-y-1 rounded-lg border border-slate-100 bg-slate-50 p-3">
            {[...log].reverse().map(entry => (
              <div key={entry.id} className="flex items-center gap-2 text-xs text-slate-600">
                <span className="text-slate-300">
                  {new Date(entry.timestamp).toLocaleTimeString('it-IT', {
                    hour: '2-digit',
                    minute: '2-digit',
                  })}
                </span>
                {entry.actorLabel && (
                  <span className="font-medium text-slate-500">{entry.actorLabel}</span>
                )}
                <span>{entry.result}</span>
              </div>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Esegui build typecheck per verificare la correttezza**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep -E "error|Error" | head -20
```
Atteso: 0 errori

- [ ] **Step 3: Esegui i test esistenti per verifica regressione**

```bash
cd apps/web && pnpm test --run src/components/toolkit/ 2>&1 | tail -20
```
Atteso: tutti PASS

- [ ] **Step 4: Commit**

```bash
git add "apps/web/src/app/(authenticated)/toolkit/play/page.tsx"
git commit -m "feat(toolkit): add Timer section to standalone play page"
```

---

## Task 3: Verifica finale e PR

- [ ] **Step 1: Esegui tutti i test frontend**

```bash
cd apps/web && pnpm test --run 2>&1 | tail -10
```
Atteso: tutti PASS (o solo pre-existing failures)

- [ ] **Step 2: Crea PR verso main-dev**

```bash
git push -u origin feature/toolkit-phase1-gaps
gh pr create \
  --base main-dev \
  --title "feat(toolkit): close Phase 1 gaps — timer in play page + sound/vibration alert" \
  --body "$(cat <<'EOF'
## Summary

- Add Timer section to standalone play page (`toolkit/play/page.tsx`) — renders `DEFAULT_TOOLKIT.timers` using the existing `Timer` component
- Add sound (AudioContext) + vibration (`navigator.vibrate`) alert to `Timer.tsx` when countdown reaches 10s warning threshold — matches spec requirement from `toolkit-game-utilities.md §4.2`

## What was already done (OI review)

| OI | Status |
|----|--------|
| OI-4 localStorage schema | ✅ Already implemented |
| OI-5 Timer offline fallback | ✅ Correctly deferred to Phase 2 |
| OI-6 POST /game-sessions/{id}/events | ✅ Already implemented |
| OI-7 Player free-text input | ✅ Already implemented |

## Test plan

- [ ] `pnpm test --run src/components/toolkit/__tests__/Timer.test.tsx` — 4 new tests pass
- [ ] `pnpm test --run src/components/toolkit/` — no regressions
- [ ] Manual: open `/toolkit/play`, verify Timer cards appear with "Timer" and "Timer turno"
- [ ] Manual: start countdown, wait to 10s — verify color change + vibration on mobile

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: Aggiorna issue/spec**

Aggiorna `docs/specs/toolkit-game-utilities.md` — marca tutti gli OI come CHIUSI.

---

## Self-Review

**Copertura spec:** Timer standalone (§4.2 REQ-TIMER-01) ✅, suono+vibrazione (§9.3) ✅, play page standalone (§3.1) ✅

**Placeholder scan:** nessuno trovato

**Type consistency:** `Timer` props (`name`, `defaultSeconds`, `type`, `onAction`) consistenti tra Task 1 e Task 2
