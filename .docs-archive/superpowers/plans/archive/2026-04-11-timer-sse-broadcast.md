# Timer SSE Broadcast Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Completare il "Phase 2" del Timer in-session: il backend pubblica eventi SSE (`session:timer`) quando il timer viene avviato/messo in pausa/ripreso/resettato, e il frontend `Timer.tsx` sincronizza il display tramite SSE quando è in modalità sessione.

**Architecture:** I 4 handler di timer (`StartTimerCommandHandler`, `PauseTimerCommandHandler`, `ResumeTimerCommandHandler`, `ResetTimerCommandHandler`) in `RandomToolHandlers.cs` ricevono `ISessionBroadcastService` e pubblicano i domain event già definiti in `RandomToolEvents.cs`. `SseEventTypeMapper` mappa questi eventi su `"session:timer"`. Il frontend aggiunge `session:timer` a `useSessionStream`, e `Timer.tsx` accetta `sessionId?: string` per entrare in modalità SSE: `useLocalTimer` guida il display in standalone, SSE sovrascrive in-session.

**Tech Stack:** .NET 9 (MediatR, xUnit, Moq), Next.js 16 / React 19 (Vitest, Testing Library)

---

## File Structure

| File | Azione | Responsabilità |
|------|--------|----------------|
| `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/RandomToolHandlers.cs` | Modify | Inject `ISessionBroadcastService`, publish timer domain events |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Services/SseEventTypeMapper.cs` | Modify | Mappa timer events → `"session:timer"` |
| `apps/web/src/lib/domain-hooks/useSessionStream.ts` | Modify | Aggiunge tipo `'session:timer'`, payload `TimerEventPayload`, callback `onTimerEvent` |
| `apps/web/src/components/toolkit/Timer.tsx` | Modify | Accetta `sessionId?: string`, consuma SSE in-session via `useSessionStream` |
| `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Handlers/RandomToolTimerBroadcastTests.cs` | Create | Test che verifica la pubblicazione SSE sui 4 handler |
| `apps/web/src/components/toolkit/__tests__/Timer.sse.test.tsx` | Create | Test SSE in-session su Timer.tsx |

---

### Task 1: Backend — pubblica eventi SSE dai timer handler

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/RandomToolHandlers.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Handlers/RandomToolTimerBroadcastTests.cs`

- [ ] **Step 1: Scrivi i test fallenti**

Crea `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Handlers/RandomToolTimerBroadcastTests.cs`:

```csharp
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class RandomToolTimerBroadcastTests
{
    private readonly TimerStateManager _timerManager = new();
    private readonly Mock<ISessionBroadcastService> _broadcastMock = new();
    private readonly Mock<ILogger<StartTimerCommandHandler>> _startLogMock = new();
    private readonly Mock<ILogger<PauseTimerCommandHandler>> _pauseLogMock = new();
    private readonly Mock<ILogger<ResumeTimerCommandHandler>> _resumeLogMock = new();
    private readonly Mock<ILogger<ResetTimerCommandHandler>> _resetLogMock = new();

    [Fact]
    public async Task StartTimer_PublishesTimerStartedEvent()
    {
        // Arrange
        var handler = new StartTimerCommandHandler(
            _timerManager, _broadcastMock.Object, _startLogMock.Object);
        var sessionId = Guid.NewGuid();
        var cmd = new StartTimerCommand(sessionId, Guid.NewGuid(), "Alice", 60);

        // Act
        await handler.Handle(cmd, CancellationToken.None);

        // Assert
        _broadcastMock.Verify(b => b.PublishAsync(
            sessionId,
            It.IsAny<TimerStartedEvent>(),
            It.Is<EventVisibility>(v => v.IsPublic),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PauseTimer_PublishesTimerPausedEvent()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _timerManager.CreateTimer(sessionId, 60, Guid.NewGuid(), "Alice");

        var handler = new PauseTimerCommandHandler(
            _timerManager, _broadcastMock.Object, _pauseLogMock.Object);
        var cmd = new PauseTimerCommand(sessionId, Guid.NewGuid());

        // Act
        await handler.Handle(cmd, CancellationToken.None);

        // Assert
        _broadcastMock.Verify(b => b.PublishAsync(
            sessionId,
            It.IsAny<TimerPausedEvent>(),
            It.Is<EventVisibility>(v => v.IsPublic),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResumeTimer_PublishesTimerResumedEvent()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var timer = _timerManager.CreateTimer(sessionId, 60, Guid.NewGuid(), "Alice");
        timer.Status = "paused";

        var handler = new ResumeTimerCommandHandler(
            _timerManager, _broadcastMock.Object, _resumeLogMock.Object);
        var cmd = new ResumeTimerCommand(sessionId, Guid.NewGuid());

        // Act
        await handler.Handle(cmd, CancellationToken.None);

        // Assert
        _broadcastMock.Verify(b => b.PublishAsync(
            sessionId,
            It.IsAny<TimerResumedEvent>(),
            It.Is<EventVisibility>(v => v.IsPublic),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetTimer_PublishesTimerResetEvent()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _timerManager.CreateTimer(sessionId, 60, Guid.NewGuid(), "Alice");

        var handler = new ResetTimerCommandHandler(
            _timerManager, _broadcastMock.Object, _resetLogMock.Object);
        var cmd = new ResetTimerCommand(sessionId, Guid.NewGuid());

        // Act
        await handler.Handle(cmd, CancellationToken.None);

        // Assert
        _broadcastMock.Verify(b => b.PublishAsync(
            sessionId,
            It.IsAny<TimerResetEvent>(),
            It.Is<EventVisibility>(v => v.IsPublic),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Esegui i test per verificare il fallimento**

```bash
cd D:/Repositories/meepleai-monorepo-backend
dotnet test tests/Api.Tests/ \
  --filter "FullyQualifiedName~RandomToolTimerBroadcastTests" \
  --no-build 2>&1 | tail -20
```

Expected: errori di compilazione — i costruttori non accettano `ISessionBroadcastService`.

- [ ] **Step 3: Modifica RandomToolHandlers.cs — aggiungi ISessionBroadcastService**

Leggi il file corrente per conoscere i numeri di riga esatti, poi applica queste modifiche ai 4 handler.

**`StartTimerCommandHandler`** — sostituisci dal costruttore al metodo `Handle`:

```csharp
public sealed class StartTimerCommandHandler : IRequestHandler<StartTimerCommand, StartTimerResponse>
{
    private readonly TimerStateManager _timerManager;
    private readonly ISessionBroadcastService _broadcast;
    private readonly ILogger<StartTimerCommandHandler> _logger;

    public StartTimerCommandHandler(
        TimerStateManager timerManager,
        ISessionBroadcastService broadcast,
        ILogger<StartTimerCommandHandler> logger)
    {
        _timerManager = timerManager;
        _broadcast = broadcast;
        _logger = logger;
    }

    public async Task<StartTimerResponse> Handle(StartTimerCommand request, CancellationToken cancellationToken)
    {
        var timer = _timerManager.CreateTimer(
            request.SessionId,
            request.DurationSeconds,
            request.ParticipantId,
            request.ParticipantName);

        _logger.LogInformation(
            "Timer started for session {SessionId}: {Duration}s by {ParticipantName}",
            request.SessionId,
            request.DurationSeconds,
            request.ParticipantName);

        await _broadcast.PublishAsync(
            request.SessionId,
            new TimerStartedEvent(
                request.SessionId,
                timer.TimerId,
                timer.DurationSeconds,
                request.ParticipantId,
                request.ParticipantName,
                timer.StartedAt!.Value),
            EventVisibility.Public,
            cancellationToken).ConfigureAwait(false);

        return new StartTimerResponse(
            request.SessionId,
            timer.TimerId,
            timer.DurationSeconds,
            timer.StartedAt!.Value);
    }
}
```

**`PauseTimerCommandHandler`** — sostituisci:

```csharp
public sealed class PauseTimerCommandHandler : IRequestHandler<PauseTimerCommand, TimerStatusResponse>
{
    private readonly TimerStateManager _timerManager;
    private readonly ISessionBroadcastService _broadcast;
    private readonly ILogger<PauseTimerCommandHandler> _logger;

    public PauseTimerCommandHandler(
        TimerStateManager timerManager,
        ISessionBroadcastService broadcast,
        ILogger<PauseTimerCommandHandler> logger)
    {
        _timerManager = timerManager;
        _broadcast = broadcast;
        _logger = logger;
    }

    public async Task<TimerStatusResponse> Handle(PauseTimerCommand request, CancellationToken cancellationToken)
    {
        var timer = _timerManager.GetTimer(request.SessionId);
        if (timer == null || !string.Equals(timer.Status, "running", StringComparison.Ordinal))
            throw new InvalidOperationException("No running timer found for this session");

        timer.RemainingSeconds = _timerManager.CalculateRemainingSeconds(timer);
        timer.Status = "paused";
        timer.PausedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Timer paused for session {SessionId}: {Remaining}s remaining",
            request.SessionId,
            timer.RemainingSeconds);

        await _broadcast.PublishAsync(
            request.SessionId,
            new TimerPausedEvent(request.SessionId, timer.TimerId, timer.RemainingSeconds, DateTime.UtcNow),
            EventVisibility.Public,
            cancellationToken).ConfigureAwait(false);

        return new TimerStatusResponse(request.SessionId, timer.TimerId, timer.Status, timer.RemainingSeconds, DateTime.UtcNow);
    }
}
```

**`ResumeTimerCommandHandler`** — sostituisci:

```csharp
public sealed class ResumeTimerCommandHandler : IRequestHandler<ResumeTimerCommand, TimerStatusResponse>
{
    private readonly TimerStateManager _timerManager;
    private readonly ISessionBroadcastService _broadcast;
    private readonly ILogger<ResumeTimerCommandHandler> _logger;

    public ResumeTimerCommandHandler(
        TimerStateManager timerManager,
        ISessionBroadcastService broadcast,
        ILogger<ResumeTimerCommandHandler> logger)
    {
        _timerManager = timerManager;
        _broadcast = broadcast;
        _logger = logger;
    }

    public async Task<TimerStatusResponse> Handle(ResumeTimerCommand request, CancellationToken cancellationToken)
    {
        var timer = _timerManager.GetTimer(request.SessionId);
        if (timer == null || !string.Equals(timer.Status, "paused", StringComparison.Ordinal))
            throw new InvalidOperationException("No paused timer found for this session");

        timer.Status = "running";
        timer.LastTickAt = DateTime.UtcNow;
        timer.PausedAt = null;

        _logger.LogInformation(
            "Timer resumed for session {SessionId}: {Remaining}s remaining",
            request.SessionId,
            timer.RemainingSeconds);

        await _broadcast.PublishAsync(
            request.SessionId,
            new TimerResumedEvent(request.SessionId, timer.TimerId, timer.RemainingSeconds, DateTime.UtcNow),
            EventVisibility.Public,
            cancellationToken).ConfigureAwait(false);

        return new TimerStatusResponse(request.SessionId, timer.TimerId, timer.Status, timer.RemainingSeconds, DateTime.UtcNow);
    }
}
```

**`ResetTimerCommandHandler`** — leggi la fine del file (offset 198) e sostituisci l'intero handler:

```csharp
public sealed class ResetTimerCommandHandler : IRequestHandler<ResetTimerCommand, TimerResetResponse>
{
    private readonly TimerStateManager _timerManager;
    private readonly ISessionBroadcastService _broadcast;
    private readonly ILogger<ResetTimerCommandHandler> _logger;

    public ResetTimerCommandHandler(
        TimerStateManager timerManager,
        ISessionBroadcastService broadcast,
        ILogger<ResetTimerCommandHandler> logger)
    {
        _timerManager = timerManager;
        _broadcast = broadcast;
        _logger = logger;
    }

    public async Task<TimerResetResponse> Handle(ResetTimerCommand request, CancellationToken cancellationToken)
    {
        _timerManager.RemoveTimer(request.SessionId);

        _logger.LogInformation("Timer reset for session {SessionId}", request.SessionId);

        await _broadcast.PublishAsync(
            request.SessionId,
            new TimerResetEvent(request.SessionId, Guid.Empty, DateTime.UtcNow),
            EventVisibility.Public,
            cancellationToken).ConfigureAwait(false);

        return new TimerResetResponse(request.SessionId, DateTime.UtcNow);
    }
}
```

Aggiungi anche il using mancante in cima al file se non già presente:

```csharp
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Services;
```

- [ ] **Step 4: Esegui i test**

```bash
cd D:/Repositories/meepleai-monorepo-backend
dotnet test tests/Api.Tests/ \
  --filter "FullyQualifiedName~RandomToolTimerBroadcastTests" 2>&1 | tail -20
```

Expected: 4 test PASS.

- [ ] **Step 5: Verifica build completo**

```bash
cd D:/Repositories/meepleai-monorepo-backend
dotnet build apps/api/src/Api/ 2>&1 | tail -20
```

Expected: Build succeeded, 0 errori. (Nota: DI deve ancora essere aggiornato — lo fa il runtime in automatico tramite reflection/MediatR DI, ma se necessario il DI di `ISessionBroadcastService` è già registrato in `SessionTrackingServiceExtensions.cs`.)

- [ ] **Step 6: Commit**

```bash
git -C D:/Repositories/meepleai-monorepo-backend add \
  apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/RandomToolHandlers.cs \
  apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Handlers/RandomToolTimerBroadcastTests.cs
git -C D:/Repositories/meepleai-monorepo-backend commit -m "feat(session-timer): publish SSE events from timer handlers via ISessionBroadcastService"
```

---

### Task 2: Backend — registra timer events in SseEventTypeMapper

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Services/SseEventTypeMapper.cs`

- [ ] **Step 1: Aggiungi le 5 voci nella EventTypeMap**

Il file `SseEventTypeMapper.cs` ha una `Dictionary<Type, string>` chiamata `EventTypeMap`. Aggiungi prima della chiusura del blocco (prima della riga `}`):

```csharp
        // Timer → session:timer (Phase 2: Timer SSE)
        [typeof(TimerStartedEvent)] = "session:timer",
        [typeof(TimerPausedEvent)] = "session:timer",
        [typeof(TimerResumedEvent)] = "session:timer",
        [typeof(TimerCompletedEvent)] = "session:timer",
        [typeof(TimerResetEvent)] = "session:timer",
```

- [ ] **Step 2: Build**

```bash
cd D:/Repositories/meepleai-monorepo-backend
dotnet build apps/api/src/Api/ 2>&1 | grep -E "error|warning|succeeded"
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git -C D:/Repositories/meepleai-monorepo-backend add \
  apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Services/SseEventTypeMapper.cs
git -C D:/Repositories/meepleai-monorepo-backend commit -m "feat(sse): map timer domain events to session:timer SSE event type"
```

---

### Task 3: Frontend — aggiungi session:timer a useSessionStream

**Files:**
- Modify: `apps/web/src/lib/domain-hooks/useSessionStream.ts`

- [ ] **Step 1: Aggiungi tipo e payload**

In `useSessionStream.ts`:

1. Aggiungi `'session:timer'` all'union type `SessionSSEEventType` (riga ~19):

```typescript
export type SessionSSEEventType =
  | 'session:state'
  | 'session:player'
  | 'session:score'
  | 'session:conflict'
  | 'session:toolkit'
  | 'session:timer'
  | 'session:chat';
```

2. Aggiungi il payload type dopo `SessionStatePayload` (circa riga ~80):

```typescript
export interface TimerEventPayload {
  sessionId: string;
  timerId: string;
  action: 'started' | 'paused' | 'resumed' | 'reset' | 'completed';
  remainingSeconds: number;
  durationSeconds?: number;
  startedAt?: string;
  timestamp: string;
}
```

3. Aggiungi `onTimerEvent` alle opzioni (in `UseSessionStreamOptions`, circa riga ~91):

```typescript
export interface UseSessionStreamOptions {
  onPlayerJoined?: (payload: PlayerJoinedPayload) => void;
  onPlayerKicked?: (payload: PlayerKickedPayload) => void;
  onPlayerReady?: (payload: PlayerReadyPayload) => void;
  onRoleChanged?: (payload: RoleChangedPayload) => void;
  onScoreUpdated?: (payload: ScoreUpdatedPayload) => void;
  onSessionStateChanged?: (payload: SessionStatePayload) => void;
  onToolkitEvent?: (payload: unknown) => void;
  onTimerEvent?: (payload: TimerEventPayload) => void;
  onChatMessage?: (payload: unknown) => void;
  onError?: (error: Error) => void;
  enabled?: boolean;
  maxReconnectAttempts?: number;
}
```

4. Aggiungi il case nello switch `handleMessage` (dopo `case 'session:toolkit'`):

```typescript
        case 'session:timer':
          cbs.onTimerEvent?.(parsed.data as TimerEventPayload);
          break;
```

5. Aggiungi l'addEventListener per `session:timer` dopo gli altri `addEventListener` (circa riga ~200):

```typescript
      es.addEventListener('session:timer', handleMessage as EventListener);
```

- [ ] **Step 2: Verifica typecheck**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/web
pnpm typecheck 2>&1 | tail -20
```

Expected: 0 errori.

- [ ] **Step 3: Esegui test esistenti di useSessionStream**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/web
pnpm test --run src/components/session/__tests__/useSessionStream.test.ts
```

Expected: PASS (nessuna regressione).

- [ ] **Step 4: Commit**

```bash
git -C D:/Repositories/meepleai-monorepo-backend add \
  apps/web/src/lib/domain-hooks/useSessionStream.ts
git -C D:/Repositories/meepleai-monorepo-backend commit -m "feat(sse): add session:timer event type and onTimerEvent callback to useSessionStream"
```

---

### Task 4: Frontend — wire Timer.tsx in modalità SSE in-session

**Files:**
- Modify: `apps/web/src/components/toolkit/Timer.tsx`
- Create: `apps/web/src/components/toolkit/__tests__/Timer.sse.test.tsx`

- [ ] **Step 1: Scrivi il test fallente**

Crea `apps/web/src/components/toolkit/__tests__/Timer.sse.test.tsx`:

```tsx
import { render, act, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// Mock useSessionStream
const mockOnTimerEvent = vi.fn();
let capturedOnTimerEvent: ((payload: unknown) => void) | undefined;

vi.mock('@/lib/domain-hooks/useSessionStream', () => ({
  useSessionStream: vi.fn().mockImplementation((_sessionId: string, opts: { onTimerEvent?: (p: unknown) => void }) => {
    capturedOnTimerEvent = opts.onTimerEvent;
    return { connectionStatus: 'connected', reconnectCount: 0, disconnect: vi.fn(), reconnect: vi.fn() };
  }),
}));

import { Timer } from '../Timer';

beforeEach(() => {
  vi.useFakeTimers();
  capturedOnTimerEvent = undefined;
});

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe('Timer — modalità SSE in-session', () => {
  it('non usa useSessionStream quando sessionId non è fornito', async () => {
    const { useSessionStream } = await import('@/lib/domain-hooks/useSessionStream');
    render(<Timer name="T" defaultSeconds={60} type="countdown" />);
    expect(useSessionStream).not.toHaveBeenCalled();
  });

  it('usa useSessionStream quando sessionId è fornito', async () => {
    const { useSessionStream } = await import('@/lib/domain-hooks/useSessionStream');
    render(<Timer name="T" defaultSeconds={60} type="countdown" sessionId="s1" />);
    expect(useSessionStream).toHaveBeenCalledWith('s1', expect.objectContaining({ onTimerEvent: expect.any(Function) }));
  });

  it('SSE paused event aggiorna display e ferma il timer locale', async () => {
    render(<Timer name="T" defaultSeconds={60} type="countdown" sessionId="s1" />);
    // Simula evento SSE paused con 45s rimanenti
    act(() => {
      capturedOnTimerEvent?.({
        sessionId: 's1',
        timerId: 't1',
        action: 'paused',
        remainingSeconds: 45,
        timestamp: new Date().toISOString(),
      });
    });
    expect(screen.getByTestId('timer-display')).toHaveTextContent('00:45');
  });

  it('SSE started event aggiorna il display', async () => {
    render(<Timer name="T" defaultSeconds={60} type="countdown" sessionId="s1" />);
    act(() => {
      capturedOnTimerEvent?.({
        sessionId: 's1',
        timerId: 't1',
        action: 'started',
        remainingSeconds: 60,
        durationSeconds: 60,
        startedAt: new Date().toISOString(),
        timestamp: new Date().toISOString(),
      });
    });
    expect(screen.getByTestId('timer-display')).toHaveTextContent('01:00');
  });

  it('SSE reset event riporta al valore di default', async () => {
    render(<Timer name="T" defaultSeconds={90} type="countdown" sessionId="s1" />);
    act(() => {
      capturedOnTimerEvent?.({
        sessionId: 's1',
        timerId: 't1',
        action: 'reset',
        remainingSeconds: 0,
        timestamp: new Date().toISOString(),
      });
    });
    expect(screen.getByTestId('timer-display')).toHaveTextContent('01:30');
  });
});
```

- [ ] **Step 2: Esegui il test per verificare il fallimento**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/web
pnpm test --run src/components/toolkit/__tests__/Timer.sse.test.tsx
```

Expected: FAIL — `Timer` non accetta `sessionId` e non chiama `useSessionStream`.

- [ ] **Step 3: Modifica Timer.tsx**

Sostituisci integralmente `apps/web/src/components/toolkit/Timer.tsx`:

```tsx
'use client';

import { useState, useEffect, useRef, useCallback } from 'react';

import { Button } from '@/components/ui/primitives/button';
import { useSessionStream, type TimerEventPayload } from '@/lib/domain-hooks/useSessionStream';

interface TimerProps {
  name: string;
  defaultSeconds: number;
  type: 'countdown' | 'countup' | 'turn';
  sessionId?: string;
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
    setTimeout(() => void ctx.close(), 600);
  } catch {
    // AudioContext non disponibile (es. JSDOM) — fallback silenzioso
  }
}

function triggerVibration(): void {
  try {
    navigator.vibrate?.([200, 100, 200]);
  } catch {
    // API vibrazione non disponibile — fallback silenzioso
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

  const syncSeconds = useCallback((s: number) => {
    setSeconds(s);
  }, []);

  const syncRunning = useCallback((r: boolean) => {
    setRunning(r);
  }, []);

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

  return { seconds, running, start, pause, reset, syncSeconds, syncRunning };
}

// ── Component ───────────────────────────────────────────────────────

export function Timer({ name, defaultSeconds, type, sessionId, onAction }: TimerProps) {
  const { seconds, running, start, pause, reset, syncSeconds, syncRunning } =
    useLocalTimer(defaultSeconds);

  // Guard: alert fires at most once per timer run
  const alertFiredRef = useRef(false);

  const isWarning = type === 'countdown' && seconds <= WARNING_THRESHOLD && seconds > 0;
  const isExpired = type === 'countdown' && seconds === 0;

  // Phase 2: when in-session, SSE timer events synchronize the display
  const handleTimerEvent = useCallback(
    (payload: TimerEventPayload) => {
      switch (payload.action) {
        case 'started':
          syncSeconds(payload.remainingSeconds);
          syncRunning(true);
          alertFiredRef.current = false;
          break;
        case 'paused':
          syncSeconds(payload.remainingSeconds);
          syncRunning(false);
          break;
        case 'resumed':
          syncSeconds(payload.remainingSeconds);
          syncRunning(true);
          break;
        case 'reset':
        case 'completed':
          syncSeconds(defaultSeconds);
          syncRunning(false);
          alertFiredRef.current = false;
          break;
      }
    },
    [syncSeconds, syncRunning, defaultSeconds]
  );

  useSessionStream(sessionId ?? null, {
    enabled: !!sessionId,
    onTimerEvent: sessionId ? handleTimerEvent : undefined,
  });

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
    alertFiredRef.current = false;
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

- [ ] **Step 4: Esegui i test SSE**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/web
pnpm test --run src/components/toolkit/__tests__/Timer.sse.test.tsx
```

Expected: 4 test PASS.

- [ ] **Step 5: Esegui i test originali Timer (nessuna regressione)**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/web
pnpm test --run src/components/toolkit/__tests__/Timer.test.tsx
```

Expected: 4 test PASS.

- [ ] **Step 6: Verifica typecheck**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/web
pnpm typecheck 2>&1 | tail -10
```

Expected: 0 errori.

- [ ] **Step 7: Commit**

```bash
git -C D:/Repositories/meepleai-monorepo-backend add \
  apps/web/src/components/toolkit/Timer.tsx \
  apps/web/src/components/toolkit/__tests__/Timer.sse.test.tsx
git -C D:/Repositories/meepleai-monorepo-backend commit -m "feat(timer): Phase 2 SSE sync — Timer.tsx consumes session:timer events when sessionId provided"
```

---

### Task 5: PR e chiusura

- [ ] **Step 1: Esegui suite completa**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/web
pnpm test --run src/components/toolkit/
pnpm lint
```

Expected: tutti PASS.

- [ ] **Step 2: Esegui test backend**

```bash
cd D:/Repositories/meepleai-monorepo-backend
dotnet test tests/Api.Tests/ \
  --filter "BoundedContext=SessionTracking" 2>&1 | tail -10
```

Expected: tutti PASS.

- [ ] **Step 3: Crea PR**

```bash
git -C D:/Repositories/meepleai-monorepo-backend push -u origin HEAD
gh pr create \
  --title "feat(timer-sse): Phase 2 — broadcast timer events via SSE and sync Timer.tsx in-session" \
  --base main-dev \
  --body "$(cat <<'EOF'
## Summary
- **Backend**: `StartTimerCommandHandler`, `PauseTimerCommandHandler`, `ResumeTimerCommandHandler`, `ResetTimerCommandHandler` now inject `ISessionBroadcastService` and publish `TimerStartedEvent`/`TimerPausedEvent`/`TimerResumedEvent`/`TimerResetEvent` after each action
- **Backend**: `SseEventTypeMapper` registers all 5 timer event types → `"session:timer"`  
- **Frontend**: `useSessionStream` adds `'session:timer'` event type, `TimerEventPayload` interface, and `onTimerEvent` callback
- **Frontend**: `Timer.tsx` accepts optional `sessionId?: string`; when provided, subscribes to SSE and syncs display from server events (Phase 2 complete)

## Test plan
- [ ] `RandomToolTimerBroadcastTests`: 4 unit tests — one per action
- [ ] `Timer.sse.test.tsx`: 4 tests — no sessionId, started, paused, reset SSE
- [ ] `Timer.test.tsx`: 4 existing tests still pass (no regression)
- [ ] `useSessionStream` existing tests pass
- [ ] `pnpm typecheck && pnpm lint` passes
- [ ] `dotnet test --filter BoundedContext=SessionTracking` passes

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Self-Review

**Spec coverage:**
- ✅ Backend pubblica eventi SSE su start/pause/resume/reset
- ✅ `SseEventTypeMapper` mappa `session:timer`
- ✅ Frontend `useSessionStream` riceve `session:timer`
- ✅ `Timer.tsx` si sincronizza da SSE quando `sessionId` è fornito
- ✅ Modalità standalone invariata (nessun `sessionId` → locale puro)
- ✅ Alert suono/vibrazione invariati

**Placeholder scan:** Nessun TODO/TBD.

**Type consistency:** `TimerEventPayload` definito in `useSessionStream.ts` e importato in `Timer.tsx`. `TimerResetResponse` deve esistere in `RandomToolCommands.cs` — se non esiste aggiungere:
```csharp
public sealed record TimerResetResponse(Guid SessionId, DateTime ResetAt);
```
Verifica con `grep -r "TimerResetResponse" apps/api/src/` prima di iniziare Task 1.
