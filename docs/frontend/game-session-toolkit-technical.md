# Game Session Toolkit - Technical Implementation Guide

**Audience**: Frontend Developers
**Last Updated**: 2026-01-30

---

## Architecture Overview

### State Management Flow

```
User Action
  ↓
sessionStore (Zustand)
  ↓
API Call (optimistic update)
  ↓
Backend SSE Event
  ↓
useSessionSync hook
  ↓
sessionStore.addScoreFromSSE()
  ↓
UI Update (real-time)
```

### Component Hierarchy

```
Page Component (Active Session)
├── SessionHeader
│   ├── Session metadata
│   ├── Action buttons (Pause/Resume/Finalize)
│   └── Connection status indicator
├── ParticipantCard (list)
│   └── Player info with avatar and score
├── Scoreboard
│   ├── Score table
│   ├── Round breakdown
│   └── Real-time animations
└── ScoreInput (sticky bottom)
    ├── Participant selector
    ├── Round/Category dropdowns
    ├── Score value input
    └── Submit button with sync status
```

---

## State Management

### sessionStore Structure

```typescript
interface SessionStore {
  // State
  activeSession: Session | null;
  scoreboard: ScoreboardData | null;
  participants: Participant[];
  isLoading: boolean;
  error: string | null;

  // CRUD Actions
  createSession: (request: CreateSessionRequest) => Promise<void>;
  joinSession: (code: string) => Promise<void>;
  loadSession: (sessionId: string) => Promise<void>;

  // Score Management
  updateScore: (request: UpdateScoreRequest) => Promise<void>;
  // ^ Implements optimistic UI with rollback on error

  // Lifecycle
  pauseSession: () => Promise<void>;
  resumeSession: () => Promise<void>;
  finalizeSession: (request: FinalizeSessionRequest) => Promise<void>;

  // SSE Integration
  addScoreFromSSE: (scoreEntry: ScoreEntry) => void;

  // Utility
  reset: () => void;
}
```

### Optimistic UI Pattern

```typescript
// 1. Create temporary score entry
const optimisticScore: ScoreEntry = {
  id: `temp-${Date.now()}`,
  participantId,
  scoreValue,
  timestamp: new Date(),
  createdBy: 'current-user',
};

// 2. Add to state immediately (UI updates)
set(state => ({
  scoreboard: {
    ...state.scoreboard,
    scores: [...state.scoreboard.scores, optimisticScore],
  },
}));

// 3. Send to backend
const actualScore = await fetch('/api/v1/sessions/{id}/scores', ...);

// 4a. Replace optimistic with actual on success
set(state => ({
  scores: state.scores.map(s =>
    s.id === optimisticScore.id ? actualScore : s
  ),
}));

// 4b. Revert on error
set(state => ({
  scores: state.scores.filter(s => s.id !== optimisticScore.id),
  error: err.message,
}));
```

---

## SSE Integration

### useSessionSync Hook

**Purpose**: Manage SSE connection for real-time session updates

**Features**:
- EventSource connection to `/api/v1/sessions/{id}/stream`
- Auto-reconnect with exponential backoff (1s, 2s, 4s, 8s, 16s max)
- Connection status tracking
- Event type handlers

**Usage**:

```typescript
const { scores, isConnected, error } = useSessionSync({
  sessionId: 'abc123',
  onScoreUpdate: (scoreEntry) => {
    addScoreFromSSE(scoreEntry);
    toast.success(`${participant.name}: +${scoreEntry.scoreValue}`);
  },
  onPaused: () => toast.info('Session paused'),
  onResumed: () => toast.info('Session resumed'),
  onFinalized: () => {
    toast.success('Session finalized');
    router.push('/toolkit');
  },
  onError: (err) => toast.error(err.message),
});
```

### Event Flow

```
Backend publishes event
  ↓
EventSource receives (browser)
  ↓
useSessionSync.handleEvent()
  ↓
Switch on event.type:
  - ScoreUpdatedEvent → onScoreUpdate(scoreEntry)
  - SessionPausedEvent → onPaused()
  - SessionResumedEvent → onResumed()
  - SessionFinalizedEvent → onFinalized()
  ↓
sessionStore.addScoreFromSSE(scoreEntry)
  ↓
React re-renders with new score
```

---

## Game Templates System

### Template Lookup Strategy

```typescript
// 1. Direct slug match
getGameTemplate('7-wonders') // ✅ Exact match

// 2. Fuzzy name matching
getGameTemplateByName('Seven Wonders') // ✅ Normalizes to '7-wonders'
getGameTemplateByName('Ticket to Ride: Europe') // ✅ Matches 'ticket-to-ride'

// 3. Partial matching
getGameTemplateByName('Wonders') // ✅ Contains check
```

### Template Application

**Game-Specific Sessions**:
1. User navigates to `/library/games/{gameId}/toolkit`
2. Fetch game details: `GET /api/v1/games/{gameId}`
3. Lookup template: `getGameTemplateByName(game.name)`
4. Display template preview (categories, rounds, rules)
5. On "Start Session": Pass `gameId` + `sessionType: 'GameSpecific'`
6. Active session loads template: `ScoreInput` pre-fills categories/rounds

**Fallback Behavior**:
- No template found → Generic session with empty categories/rounds
- User can manually add categories during session

---

## Testing Strategy

### Unit Test Coverage

**useSessionSync Hook** (5 tests):
- Initialization with default state
- EventSource creation with correct URL
- Callback prop acceptance
- Custom API base URL support
- Cleanup on unmount

**sessionStore** (10 tests):
- Create session success/error
- Join session by code
- Optimistic score update
- Optimistic rollback on error
- Pause/Resume/Finalize lifecycle

**game-templates** (17 tests):
- Template structure validation
- Slug lookup (exact, case-insensitive)
- Fuzzy name matching
- Player count validation
- Emoji icon validation

### E2E Test Scenarios

**toolkit-create-session.spec.ts**:
- Landing page display
- Create session flow
- Join by code validation
- Participant management
- Mobile responsive
- Dark mode

**toolkit-realtime-sync.spec.ts**:
- 2-browser context SSE sync
- Connection status indicator
- Pause/Resume lifecycle
- Finalize and redirect
- Optimistic UI verification

**game-toolkit-flow.spec.ts**:
- Library card toolkit button
- Template preview display
- Session creation with template
- Category pre-fill validation
- Player count validation

**session-history.spec.ts**:
- History page display
- Filter interactions
- Empty state navigation
- Modal interactions (planned Phase 2)

---

## Performance Considerations

### SSE Connection Management

**Max Reconnects**: 5 attempts
**Backoff Strategy**: Exponential (1s → 30s cap)
**Memory**: EventSource cleanup on unmount prevents leaks

### Optimistic UI

**Benefits**:
- Instant feedback (0ms perceived latency)
- Works during network instability
- Graceful degradation

**Trade-offs**:
- Potential flash if server rejects
- Requires rollback logic
- Slightly more complex state management

### Component Rendering

**Scoreboard**:
- Virtualization not needed (typical sessions: 2-10 players)
- Real-time animations use CSS transforms (GPU-accelerated)
- Score updates trigger minimal re-renders (React key optimization)

**Session History**:
- Pagination: 20 items/page (prevents DOM bloat)
- Lazy loading planned for Phase 2

---

## Security Considerations

### Session Code Generation

**Format**: 6-character uppercase alphanumeric
**Entropy**: 36^6 = 2,176,782,336 combinations
**Collision Risk**: Negligible for typical usage
**Backend Validation**: Required for join operations

### Score Manipulation

**Protection**:
- All score updates via authenticated endpoints
- Server validates session ownership
- Optimistic UI reverts on 401/403 errors
- SSE events broadcast to session participants only

### XSS Prevention

**Participant Names**: Sanitized by backend
**Session Notes**: Not yet implemented (planned sanitization)
**Avatar Colors**: Validated hex format on backend

---

## Accessibility (WCAG 2.1 AA)

### Keyboard Navigation

- Tab order: Filters → Session cards → Action buttons
- Enter/Space: Activate buttons and links
- Escape: Close modals

### Screen Readers

- ARIA labels on all interactive elements
- Session status announced
- Score updates announced (via toast)
- Modal dialogs properly labeled

### Color Contrast

- All text meets 4.5:1 ratio
- Status badges use semantic colors with sufficient contrast
- Dark mode: Enhanced contrast ratios

---

## Troubleshooting

### SSE Not Connecting

**Symptoms**: `isConnected` stays false, no real-time updates

**Debugging**:
1. Check browser console for EventSource errors
2. Verify backend `/stream` endpoint responds
3. Check CORS configuration
4. Verify session ID is valid

**Solution**:
```typescript
// Check connection status
const { isConnected, error } = useSessionSync({ sessionId });
console.log('Connected:', isConnected, 'Error:', error);

// Manual reconnection
useEffect(() => {
  if (!isConnected && !error) {
    // Will auto-reconnect
  }
}, [isConnected, error]);
```

### Optimistic UI Shows Wrong Score

**Symptoms**: Score flashes different value after submission

**Cause**: Backend validation adjusted the score value

**Solution**: This is expected behavior - backend is source of truth

### Template Not Loading

**Symptoms**: Generic session loads instead of game template

**Debugging**:
```typescript
const template = getGameTemplateByName(gameName);
console.log('Template found:', template);
```

**Solution**: Add game to `GAME_TEMPLATES` or adjust fuzzy matching logic

---

## Code Review Checklist

Before submitting GST-related PRs:

- [ ] TypeCheck passes: `pnpm typecheck`
- [ ] Lint passes: `pnpm lint`
- [ ] Unit tests added: 85%+ coverage
- [ ] E2E tests for critical flows
- [ ] Mobile responsive tested
- [ ] Dark mode verified
- [ ] SSE cleanup in useEffect return
- [ ] Optimistic UI with rollback
- [ ] No unused imports/variables
- [ ] ARIA labels on interactive elements

---

## Related Files

**Core Implementation**:
- `apps/web/src/lib/hooks/useSessionSync.ts` - SSE hook
- `apps/web/src/lib/stores/sessionStore.ts` - State management
- `apps/web/src/lib/config/game-templates.ts` - Templates
- `apps/web/src/components/session/` - Reusable components

**Routes**:
- `apps/web/src/app/(authenticated)/toolkit/` - Generic toolkit routes
- `apps/web/src/app/(authenticated)/library/games/[gameId]/toolkit/` - Game-specific routes

**Tests**:
- `apps/web/src/lib/hooks/__tests__/useSessionSync.test.ts`
- `apps/web/src/lib/stores/__tests__/sessionStore.test.ts`
- `apps/web/src/lib/config/__tests__/game-templates.test.ts`
- `apps/web/__tests__/e2e/toolkit-*.spec.ts`

---

**Version**: 1.0.0
**Epic**: EPIC-GST-001
**Issues**: #3163, #3164, #3165
