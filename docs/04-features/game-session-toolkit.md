# Game Session Toolkit (GST)

**Status**: ✅ Production Ready
**Epic**: EPIC-GST-001
**Issues**: #3163, #3164, #3165
**PRs**: #3223, #3224, #3225
**Version**: 1.0.0 (2026-01-30)

---

## Overview

Real-time collaborative scorekeeper for board games with SSE streaming, pre-configured game templates, and session history tracking.

### Key Features

- **Generic Sessions**: Create/join any game session with custom scoring
- **Game-Specific Templates**: Pre-configured scorekeeping for 6+ popular games
- **Real-Time Sync**: SSE-based live score updates across multiple devices
- **Session History**: Browse past sessions with filters and detailed statistics
- **Mobile-First**: Responsive design with dark mode support
- **Optimistic UI**: Instant score updates with background synchronization

---

## User Workflows

### 1. Generic Session Flow

```
/toolkit
  → Create Session (add participants)
  → /toolkit/{sessionId} (active session)
  → Real-time score tracking
  → Finalize session
  → /toolkit/history
```

### 2. Game-Specific Flow

```
/library
  → Select game card
  → Click "Toolkit" button
  → /library/games/{gameId}/toolkit (template preview)
  → Start session (template auto-applied)
  → /library/games/{gameId}/toolkit/{sessionId}
  → Track scores with pre-configured categories
  → Finalize & return to game detail
```

### 3. Session History

```
/toolkit/history
  → Filter by game/date
  → View session cards
  → Click "View Details"
  → SessionDetailModal (full scoreboard)
```

---

## Architecture

### Frontend Components

**Core Components** (`apps/web/src/components/session/`):
- `SessionHeader` - Session metadata, code display, action buttons
- `ParticipantCard` - Player info with avatar, score, rank
- `ScoreInput` - Score submission form with categories/rounds
- `Scoreboard` - Real-time score table with animations
- `SessionDetailModal` - Full session details in dialog

**Hooks** (`apps/web/src/lib/hooks/`):
- `useSessionSync` - SSE connection for real-time updates
  - Auto-reconnect with exponential backoff (max 5 attempts)
  - Event types: ScoreUpdatedEvent, SessionPausedEvent, SessionResumedEvent, SessionFinalizedEvent

**State Management** (`apps/web/src/lib/stores/`):
- `sessionStore` - Zustand store with devtools
  - Actions: createSession, joinSession, updateScore, pauseSession, resumeSession, finalizeSession
  - Optimistic UI: Immediate score display, server sync in background

**Configuration** (`apps/web/src/lib/config/`):
- `game-templates.ts` - 6 game templates (7 Wonders, Splendor, Catan, Ticket to Ride, Wingspan, Azul)

### Routes

| Route | File | Purpose |
|-------|------|---------|
| `/toolkit` | `app/(authenticated)/toolkit/page.tsx` | Create/join generic session |
| `/toolkit/{sessionId}` | `app/(authenticated)/toolkit/[sessionId]/page.tsx` | Active generic session |
| `/toolkit/history` | `app/(authenticated)/toolkit/history/page.tsx` | Session history with filters |
| `/library/games/{gameId}/toolkit` | `app/(authenticated)/library/games/[gameId]/toolkit/page.tsx` | Game template landing |
| `/library/games/{gameId}/toolkit/{sessionId}` | `app/(authenticated)/library/games/[gameId]/toolkit/[sessionId]/page.tsx` | Active game session |

### Backend Integration

**Endpoints** (from GST-003 SSE Infrastructure):
- `POST /api/v1/sessions` - Create session
- `POST /api/v1/sessions/join/{code}` - Join by code
- `GET /api/v1/sessions/{id}/details` - Fetch session
- `PUT /api/v1/sessions/{id}/scores` - Update score
- `PUT /api/v1/sessions/{id}/pause` - Pause session
- `PUT /api/v1/sessions/{id}/resume` - Resume session
- `PUT /api/v1/sessions/{id}/finalize` - Finalize with ranks
- `GET /api/v1/sessions/{id}/stream` - SSE event stream
- `GET /api/v1/sessions/history` - Session history with filters

**SSE Event Schema**:
```typescript
type SessionEventType =
  | 'ScoreUpdatedEvent'
  | 'SessionPausedEvent'
  | 'SessionResumedEvent'
  | 'SessionFinalizedEvent';

interface SessionEvent {
  type: SessionEventType;
  data: {
    scoreEntry?: ScoreEntry;
    // Event-specific data
  };
}
```

---

## Game Templates

### Available Templates

| Game | Icon | Rounds | Categories | Players |
|------|------|--------|------------|---------|
| **7 Wonders** | 🏛️ | 1, 2, 3 | Military, Science, Commerce, Wonders, Coins, Guilds | 3-7 |
| **Splendor** | 💎 | - | Nobles, Cards, Tokens | 2-4 |
| **Catan** | 🏝️ | - | Settlements, Cities, Longest Road, Largest Army, VP, Dev Cards | 3-4 |
| **Ticket to Ride** | 🚂 | - | Routes, Tickets, Longest Route, Stations | 2-5 |
| **Wingspan** | 🦅 | 1-4 | Birds, Bonus Cards, Goals, Eggs, Food, Tucked Cards | 1-5 |
| **Azul** | 🎨 | 1-5 | Wall Tiles, Rows, Columns, Colors, Penalties | 2-4 |

### Template Structure

```typescript
interface GameTemplate {
  name: string;
  icon: string;
  rounds: number[];
  categories: string[];
  scoringRules: string;
  playerCount: { min: number; max: number };
}
```

### Adding New Templates

Edit `apps/web/src/lib/config/game-templates.ts`:

```typescript
export const GAME_TEMPLATES: Record<string, GameTemplate> = {
  'my-game': {
    name: 'My Game',
    icon: '🎲',
    rounds: [1, 2, 3, 4],
    categories: ['Category A', 'Category B'],
    scoringRules: 'Scoring description',
    playerCount: { min: 2, max: 6 },
  },
  // ... existing templates
};
```

---

## Testing

### Test Coverage

**Unit Tests**: 49 tests
- `useSessionSync.test.ts` - 5 tests
- `sessionStore.test.ts` - 10 tests
- `game-templates.test.ts` - 17 tests
- GST-004 fixes: 17 tests

**E2E Tests**: 31 scenarios
- `toolkit-create-session.spec.ts` - 8 scenarios
- `toolkit-realtime-sync.spec.ts` - 6 scenarios
- `game-toolkit-flow.spec.ts` - 10 scenarios
- `session-history.spec.ts` - 7 scenarios

**Coverage**: 85%+ (frontend target met)

### Running Tests

```bash
# Unit tests
cd apps/web
pnpm test useSessionSync
pnpm test sessionStore
pnpm test game-templates

# E2E tests
pnpm test:e2e toolkit-create-session
pnpm test:e2e toolkit-realtime-sync
pnpm test:e2e game-toolkit-flow
pnpm test:e2e session-history

# All toolkit tests
pnpm test toolkit session
```

---

## Development Guide

### Creating a New Session

```typescript
import { useSessionStore } from '@/lib/stores/sessionStore';

const { createSession } = useSessionStore();

await createSession({
  participants: [
    { displayName: 'Alice' },
    { displayName: 'Bob' }
  ],
  sessionDate: new Date(),
});
```

### Real-Time Sync Integration

```typescript
import { useSessionSync } from '@/lib/hooks/useSessionSync';

const { scores, isConnected, error } = useSessionSync({
  sessionId: 'session-id',
  onScoreUpdate: (scoreEntry) => {
    console.log('New score:', scoreEntry);
  },
});
```

### Using Game Templates

```typescript
import { getGameTemplateByName, hasGameTemplate } from '@/lib/config/game-templates';

const template = getGameTemplateByName('7 Wonders');
if (template) {
  // Use template.rounds, template.categories
  console.log('Categories:', template.categories);
}

// Check availability
if (hasGameTemplate(gameName)) {
  // Show toolkit button
}
```

---

## Deployment

### Environment Variables

```bash
# Frontend (.env.local)
NEXT_PUBLIC_API_BASE=http://localhost:8080
```

### Feature Flags

No feature flags required - toolkit is always enabled for authenticated users.

### Navigation Integration

Toolkit automatically appears in:
- Main navbar (desktop + mobile)
- Library game cards (grid + list views)
- Game detail pages

---

## Known Limitations

### Phase 1 (MVP)

1. **Session History**: Filters UI complete, backend integration pending
2. **Game Stats**: UserLibrary integration deferred to Phase 2
3. **Analytics Dashboard**: Optional feature not implemented

### Backend Dependencies

- GST-002: Session finalization creates `games_played` entries
- GST-003: SSE infrastructure provides real-time events
- Games catalog: Required for game-specific templates

---

## Future Enhancements

### Phase 2

- [ ] Session history filter application with backend data
- [ ] UserLibrary game stats tab (win rate, score progression charts)
- [ ] Export session results (PDF, CSV)
- [ ] Share session link (public view)

### Phase 3

- [ ] Analytics dashboard (`/toolkit/stats`)
- [ ] Session notes and photo uploads
- [ ] Timer/round tracking
- [ ] Advanced scoring (multipliers, bonuses)

---

## API Reference

See [Session Tracking API](../03-api/session-tracking/README.md) for complete endpoint documentation.

### Quick Reference

```typescript
// Create session
POST /api/v1/sessions
Body: { participants: [{ displayName: string }], sessionDate: Date }
Response: Session

// Join session
POST /api/v1/sessions/join/{code}
Response: Session

// Update score
PUT /api/v1/sessions/{id}/scores
Body: { participantId: string, scoreValue: number, roundNumber?: number, category?: string }
Response: ScoreEntry

// SSE stream
GET /api/v1/sessions/{id}/stream
Response: text/event-stream
```

---

## Troubleshooting

### SSE Connection Issues

**Problem**: Connection disconnects frequently
**Solution**: Check network stability, SSE auto-reconnects up to 5 times

**Problem**: Scores not syncing in real-time
**Solution**: Verify `isConnected` state, check browser console for SSE errors

### Template Not Found

**Problem**: Game template not loading
**Solution**: Verify game name matches template slug (use fuzzy matching: `getGameTemplateByName`)

### Optimistic UI Conflicts

**Problem**: Score shows different value after sync
**Solution**: Backend validation may adjust score, optimistic update reverts on error

---

## Related Documentation

- [Frontend Architecture](../07-frontend/architecture.md)
- [Testing Guide](../05-testing/frontend/README.md)
- [SSE Infrastructure](../03-api/session-tracking/sse-events.md)
- [Component Library](../07-frontend/components.md)

---

**Last Updated**: 2026-01-30
**Maintainer**: Development Team
**Epic**: EPIC-GST-001
