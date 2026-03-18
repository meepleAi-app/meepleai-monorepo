# MeepleCard Usage Guide

## Issue #4030 + #4031: Multi-Entity Card System

### Basic Usage with Entity Actions

```tsx
import { MeepleCard } from '@/components/ui/data-display';
import { useEntityActions } from '@/hooks/use-entity-actions';

function GameList({ games }) {
  return (
    <div className="grid grid-cols-[repeat(auto-fill,minmax(220px,240px))] gap-6">
      {games.map(game => {
        const actions = useEntityActions({
          entity: 'game',
          id: game.id,
          userId: currentUserId,
          userRole: currentUserRole,
        });

        return (
          <MeepleCard
            key={game.id}
            entity="game"
            variant="grid"
            title={game.title}
            subtitle={`${game.publisher}, ${game.yearPublished}`}
            imageUrl={game.imageUrl}
            rating={game.averageRating}
            ratingMax={10}
            metadata={[
              { icon: Users, value: `${game.minPlayers}-${game.maxPlayers}` },
              { icon: Clock, value: `${game.playingTime}m` },
            ]}
            // New: Entity-specific quick actions
            entityQuickActions={actions.quickActions}
            showInfoButton
            infoHref={`/games/${game.id}`}
            infoTooltip="Vai al dettaglio"
          />
        );
      })}
    </div>
  );
}
```

### All Entity Types

#### Game
```tsx
const actions = useEntityActions({ entity: 'game', id: game.id });
// Quick: Chat con Agent, Avvia Sessione, Condividi
// Info: → /games/[id]
```

#### Session
```tsx
const actions = useEntityActions({
  entity: 'session',
  id: session.id,
  data: { sessionCode: session.sessionCode },
});
// Quick: Riprendi, Usa Toolkit, Condividi codice
// Info: → /sessions/[id]
```

#### Agent
```tsx
const actions = useEntityActions({
  entity: 'agent',
  id: agent.id,
  userRole: currentUserRole, // for admin-only actions
});
// Quick: Chat, Statistiche
// Info: → /agents/[id]
```

#### Document
```tsx
const actions = useEntityActions({
  entity: 'kb',
  id: doc.id,
  userId: currentUserId,
  userRole: currentUserRole,
  data: { ownerId: doc.uploadedByUserId, isShared: doc.isSharedGame },
});
// Quick: Download, Chat sui contenuti
// Info: → /documents/[id]
```

#### ChatSession
```tsx
const actions = useEntityActions({ entity: 'chatSession', id: chat.id });
// Quick: Continua Chat, Esporta
// Info: → /chat/[id]
```

#### Player
```tsx
const actions = useEntityActions({ entity: 'player', id: player.id });
// Quick: Messaggia, Invita a Sessione
// Info: → /players/[id]
```

#### Event
```tsx
const actions = useEntityActions({ entity: 'event', id: event.id });
// Quick: Partecipa, Condividi
// Info: → /events/[id]
```

### Props Reference

```typescript
interface MeepleCardProps {
  // ... existing props ...

  // Issue #4030: New action system
  entityQuickActions?: QuickAction[];  // From useEntityActions hook
  showInfoButton?: boolean;             // Enable info button
  infoHref?: string;                    // Navigation target
  infoTooltip?: string;                 // Tooltip text (default: "View details")
}
```

### Grid Layout (Board Game Card Proportions)

```tsx
<div className="grid grid-cols-[repeat(auto-fill,minmax(220px,240px))] gap-6">
  {/* Cards will be 220-240px wide with 7:10 aspect ratio */}
</div>
```

### Permission Example

```tsx
// Document with role-gated actions
const actions = useEntityActions({
  entity: 'kb',
  id: doc.id,
  userId: currentUserId,
  userRole: currentUserRole,
  data: {
    ownerId: doc.uploadedByUserId,
    isShared: doc.isSharedGame,
  },
});

// Hook returns:
// - Download, Chat (always visible)
// - Rielabora (admin + isShared only) → in moreActions
// - Elimina (own only) → in moreActions
```

## Next Steps (Future Issues)

The `useEntityActions` hook currently implements basic navigation and clipboard actions. Future enhancements:

- **More menu actions** (secondary dropdown)
- **Modal triggers** (share modal, invite modal, etc.)
- **API integration** (pause session, RSVP event, etc.)
- **Optimistic UI updates**
- **Toast notifications**
- **Error handling**

These can be added incrementally without breaking existing functionality.
