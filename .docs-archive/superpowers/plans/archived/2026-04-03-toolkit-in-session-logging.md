# Toolkit Phase 2 — In-Session Tool Logging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wiring delle azioni dei custom toolkit tool (dado, timer, carte) al log di sessione backend via `POST /api/v1/game-sessions/{sessionId}/events`, e sostituzione del placeholder card con `CardDeckTool`.

**Architecture:** Un hook fire-and-forget `useSessionToolLog` incapsula la chiamata REST al `SessionTracking` BC esistente (`AddSessionEventCommand`, EventType=`ToolAction`). Il componente `_content.tsx` usa il hook e passa `onRoll`/`onAction` ai tool già renderizzati. Il placeholder card viene sostituito con `CardDeckTool` dalle utilities Phase 1.

**Tech Stack:** Next.js 16 App Router, React 19, TypeScript, Vitest + @testing-library/react.

**Riferimenti:**
- Endpoint backend esistente: `POST /api/v1/game-sessions/{sessionId}/events` (`AddSessionEventCommand`)
- Tool card esistente: `apps/web/src/components/toolkit/CardDeckTool.tsx`
- In-session page: `apps/web/src/app/(authenticated)/toolkit/[sessionId]/_content.tsx`
- Hook pattern: `apps/web/src/lib/domain-hooks/useCounterTool.ts` (raw fetch + `NEXT_PUBLIC_API_BASE`)

---

## File Map

### Nuovi file
```
apps/web/src/lib/domain-hooks/useSessionToolLog.ts
apps/web/src/lib/domain-hooks/__tests__/useSessionToolLog.test.ts
```

### File modificati
```
apps/web/src/app/(authenticated)/toolkit/[sessionId]/_content.tsx
  — import useSessionToolLog, CardDeckTool
  — wire onRoll su ToolkitDiceRoller
  — wire onAction su ToolkitTimer
  — sostituire CustomToolPlaceholder card con CardDeckTool + onAction
```

---

## Task 1: Hook `useSessionToolLog`

**Files:**
- Create: `apps/web/src/lib/domain-hooks/useSessionToolLog.ts`
- Create: `apps/web/src/lib/domain-hooks/__tests__/useSessionToolLog.test.ts`

### Contesto per il subagent

Il codebase usa hook React che chiamano `fetch` direttamente con `process.env.NEXT_PUBLIC_API_BASE ?? ''`. Vedere `useCounterTool.ts` e `useTurnOrder.ts` come pattern di riferimento.

Il backend endpoint è `POST /api/v1/game-sessions/{sessionId}/events` con body:
```json
{
  "eventType": "ToolAction",
  "payload": "{\"toolType\":\"dice\",\"action\":\"roll\",\"result\":\"D6 → 4\"}",
  "source": "toolkit"
}
```

Il hook è **fire-and-forget**: non ha stato, non blocca la UI, ignora silenziosamente gli errori di rete.

- [ ] **Step 1: Scrivere il test**

```typescript
// apps/web/src/lib/domain-hooks/__tests__/useSessionToolLog.test.ts

import { act, renderHook } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { useSessionToolLog } from '../useSessionToolLog';

describe('useSessionToolLog', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('POSTs a ToolAction event with correct body', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true } as Response);
    vi.stubGlobal('fetch', fetchMock);

    const { result } = renderHook(() => useSessionToolLog('sess-abc'));

    act(() => {
      result.current.logToolAction('dice', 'roll', 'D6 → 4');
    });

    // fire-and-forget: wait for microtask
    await Promise.resolve();

    expect(fetchMock).toHaveBeenCalledOnce();
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain('/api/v1/game-sessions/sess-abc/events');
    expect(init.method).toBe('POST');

    const body = JSON.parse(init.body as string) as Record<string, unknown>;
    expect(body.eventType).toBe('ToolAction');
    expect(body.source).toBe('toolkit');

    const payload = JSON.parse(body.payload as string) as Record<string, unknown>;
    expect(payload.toolType).toBe('dice');
    expect(payload.action).toBe('roll');
    expect(payload.result).toBe('D6 → 4');
  });

  it('does not throw when fetch rejects', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('network')));
    const { result } = renderHook(() => useSessionToolLog('sess-abc'));

    // Should not throw or cause unhandled rejection
    expect(() =>
      act(() => {
        result.current.logToolAction('timer', 'start', '60');
      })
    ).not.toThrow();

    await Promise.resolve();
  });

  it('encodes sessionId in URL', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true } as Response);
    vi.stubGlobal('fetch', fetchMock);

    const { result } = renderHook(() => useSessionToolLog('id with spaces'));

    act(() => {
      result.current.logToolAction('card', 'draw', 'As');
    });

    await Promise.resolve();

    const [url] = fetchMock.mock.calls[0] as [string];
    expect(url).toContain(encodeURIComponent('id with spaces'));
  });

  it('returns stable logToolAction reference across rerenders', () => {
    const { result, rerender } = renderHook(() => useSessionToolLog('sess-abc'));
    const first = result.current.logToolAction;
    rerender();
    expect(result.current.logToolAction).toBe(first);
  });
});
```

- [ ] **Step 2: Eseguire il test per verificare che fallisca**

```bash
cd apps/web
pnpm test src/lib/domain-hooks/__tests__/useSessionToolLog.test.ts
```

Expected: FAIL — `Cannot find module '../useSessionToolLog'`

- [ ] **Step 3: Scrivere l'implementazione minima**

```typescript
// apps/web/src/lib/domain-hooks/useSessionToolLog.ts

/**
 * useSessionToolLog — Fire-and-forget session tool event logger.
 *
 * Sends ToolAction events to the SessionTracking BC via:
 *   POST /api/v1/game-sessions/{sessionId}/events
 *
 * Error handling: silent — network failures are logged to console only.
 * No state, no blocking, no retry.
 *
 * Phase 2 toolkit logging (companion to Phase 1 standalone toolkit).
 */

import { useCallback } from 'react';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE ?? '';

export interface UseSessionToolLogResult {
  /** Fire-and-forget: log a tool action to the session diary. */
  logToolAction: (toolType: string, action: string, result: string) => void;
}

export function useSessionToolLog(sessionId: string): UseSessionToolLogResult {
  const logToolAction = useCallback(
    (toolType: string, action: string, result: string) => {
      void fetch(
        `${API_BASE}/api/v1/game-sessions/${encodeURIComponent(sessionId)}/events`,
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
          body: JSON.stringify({
            eventType: 'ToolAction',
            payload: JSON.stringify({ toolType, action, result }),
            source: 'toolkit',
          }),
        }
      ).catch((err: unknown) => {
        // Fire-and-forget: swallow network errors silently
        console.warn('[useSessionToolLog] failed to log tool action', err);
      });
    },
    [sessionId]
  );

  return { logToolAction };
}
```

- [ ] **Step 4: Eseguire il test per verificare che passi**

```bash
cd apps/web
pnpm test src/lib/domain-hooks/__tests__/useSessionToolLog.test.ts
```

Expected: PASS — 4 tests passed

- [ ] **Step 5: Typecheck**

```bash
cd apps/web
pnpm typecheck
```

Expected: no errors

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/lib/domain-hooks/useSessionToolLog.ts \
        apps/web/src/lib/domain-hooks/__tests__/useSessionToolLog.test.ts
git commit -m "feat(toolkit): add useSessionToolLog fire-and-forget hook"
```

---

## Task 2: Wiring in `_content.tsx` + card tool reale

**Files:**
- Modify: `apps/web/src/app/(authenticated)/toolkit/[sessionId]/_content.tsx`

### Contesto per il subagent

`_content.tsx` gestisce la pagina in-session del toolkit. Usa già:
- `ToolkitDiceRoller` (alias di `DiceRoller` da `@/components/toolkit/DiceRoller`) — renderizzato per `custom-dice-*` tools
- `ToolkitTimer` (alias di `Timer` da `@/components/toolkit/Timer`) — renderizzato per `custom-timer-*` tools
- `CustomToolPlaceholder` per card tools (`custom-card-*`) — da sostituire con `CardDeckTool`

**Interfacce già esistenti** (Phase 1):

```typescript
// DiceRoller props (apps/web/src/components/toolkit/DiceRoller.tsx)
export interface DiceRollResult {
  faces: string[];  // risultati per dado
  total: string;    // somma numerica o join(' | ') per custom
}
interface DiceRollerProps {
  config: DiceConfig;
  actorLabel?: string;
  onRoll?: (result: DiceRollResult) => void;
}

// Timer props (apps/web/src/components/toolkit/Timer.tsx)
interface TimerProps {
  name: string;
  defaultSeconds: number;
  type: 'countdown' | 'countup' | 'turn';
  onAction?: (action: string, seconds: number) => void;
}

// CardDeckTool props (apps/web/src/components/toolkit/CardDeckTool.tsx)
interface CardDeckToolProps {
  deckId: string;
  name: string;
  cards: string[];           // facce del mazzo (["1","2",...,"N"] per mazzo generico)
  reshuffleOnEmpty: boolean;
  onAction?: (action: string, result: string) => void;
}
```

**Mappatura `CardToolDto` → `CardDeckTool`:**
```typescript
// CardToolDto (apps/web/src/lib/types/gameToolkit.ts)
interface CardToolDto {
  name: string;
  deckType: string;
  cardCount: number;
  shuffleable: boolean;
  allowDraw: boolean;
  allowDiscard: boolean;
  allowPeek: boolean;
  allowReturnToDeck: boolean;
}

// Mapping:
deckId = `session-${sessionId}-card-${cardIdx}`
name = dto.name
cards = Array.from({ length: dto.cardCount }, (_, i) => String(i + 1))
reshuffleOnEmpty = dto.allowReturnToDeck
```

- [ ] **Step 1: Leggere il file corrente**

```bash
cat apps/web/src/app/\(authenticated\)/toolkit/\[sessionId\]/_content.tsx
```

Nota le righe che gestiscono `custom-dice-*`, `custom-timer-*`, `custom-card-*`.

- [ ] **Step 2: Aggiungere i 3 import mancanti**

Nel blocco degli import, dopo `import { ToolkitDiceRoller } ...` e `import { ToolkitTimer } ...`:

```typescript
import { CardDeckTool } from '@/components/toolkit';
import { useSessionToolLog } from '@/lib/domain-hooks/useSessionToolLog';
```

- [ ] **Step 3: Inizializzare il hook nel corpo del componente**

Dopo la riga `const sessionId = params?.sessionId as string;` e dopo il guard `if (!sessionId)`:

```typescript
const { logToolAction } = useSessionToolLog(sessionId);
```

- [ ] **Step 4: Aggiungere `onRoll` al dado custom (riga ~384)**

Trovare il blocco `if (activeTool.startsWith('custom-dice-'))` e aggiungere `onRoll`:

```typescript
// Prima (attuale):
return <ToolkitDiceRoller config={diceConfig} />;

// Dopo:
return (
  <ToolkitDiceRoller
    config={diceConfig}
    onRoll={r =>
      logToolAction('dice', 'roll', `${diceDto.name}: [${r.faces.join(', ')}] = ${r.total}`)
    }
  />
);
```

- [ ] **Step 5: Aggiungere `onAction` al timer custom (riga ~406)**

Trovare il blocco `if (activeTool.startsWith('custom-timer-'))` e aggiungere `onAction`:

```typescript
// Prima (attuale):
return (
  <ToolkitTimer
    name={timerDto.name}
    defaultSeconds={timerDto.durationSeconds}
    type={timerType}
  />
);

// Dopo:
return (
  <ToolkitTimer
    name={timerDto.name}
    defaultSeconds={timerDto.durationSeconds}
    type={timerType}
    onAction={(action, seconds) =>
      logToolAction('timer', action, `${timerDto.name}: ${action} @ ${seconds}s`)
    }
  />
);
```

- [ ] **Step 6: Sostituire il placeholder card con `CardDeckTool`**

Trovare il blocco che renderizza `custom-card-*`. Attualmente usa `CustomToolPlaceholder`.

Aggiungere PRIMA del blocco `const customTool = customTools.find(...)`:

```typescript
// Card tools → CardDeckTool
if (activeTool.startsWith('custom-card-')) {
  const cardIdx = parseInt(activeTool.replace('custom-card-', ''), 10);
  const cardDto = toolkit.cardTools[cardIdx];
  if (cardDto) {
    const cards = Array.from({ length: cardDto.cardCount }, (_, i) => String(i + 1));
    return (
      <CardDeckTool
        deckId={`session-${sessionId}-card-${cardIdx}`}
        name={cardDto.name}
        cards={cards}
        reshuffleOnEmpty={cardDto.allowReturnToDeck}
        onAction={(action, result) =>
          logToolAction('card', action, `${cardDto.name}: ${result}`)
        }
      />
    );
  }
}
```

- [ ] **Step 7: Typecheck**

```bash
cd apps/web
pnpm typecheck
```

Expected: no errors

- [ ] **Step 8: Test toolkit esistenti**

```bash
cd apps/web
pnpm test src/components/toolkit/
```

Expected: 118 tests passed (stessi di Phase 1 — nessuna regressione)

- [ ] **Step 9: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/toolkit/\[sessionId\]/_content.tsx
git commit -m "feat(toolkit): wire in-session tool logging + CardDeckTool for custom card tools"
```

---

## Acceptance Criteria

| Criterio | Verifica |
|----------|---------|
| Hook fire-and-forget | 4 test passano in `useSessionToolLog.test.ts` |
| No TypeScript errors | `pnpm typecheck` clean |
| Custom dado → log | `onRoll` passa al hook (verificato con test hook) |
| Custom timer → log | `onAction` passa al hook |
| Card tool reale | `CardDeckTool` renderizzato invece di placeholder |
| No regressioni | 118+ test toolkit Phase 1 passano |

---

## Note tecniche

- **Fire-and-forget**: `logToolAction` non restituisce Promise, non gestisce loading/error state. Se il backend è down, l'utente non vede nulla — il log è best-effort.
- **`CardToolDto.cardCount` senza facce**: il backend non espone le facce delle carte nel DTO. `cards` viene generato come `["1", "2", ..., "N"]`. Per facce custom (es. Pandemic) serve un futuro BE update.
- **`reshuffleOnEmpty` ↔ `allowReturnToDeck`**: mapping semanticamente approssimativo. Se `allowReturnToDeck` è `false`, il mazzo si ferma quando vuoto.
- **`deckId` unico**: `session-${sessionId}-card-${cardIdx}` garantisce che lo stato Zustand non collida tra sessioni o tra card diversi.
