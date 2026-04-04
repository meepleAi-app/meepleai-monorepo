# US-43: Entity Relationships UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Display game relationships (expansions, sequels, reimplementations, related games) in the UI and allow users to create their own relationship links.

**Architecture:** Frontend components to display EntityLink data from the existing API. Relationships shown on game detail pages grouped by link type. User can create User-scope links. Admin can bulk-import from BGG.

**Tech Stack:** Next.js 16, React 19, Tailwind 4, shadcn/ui

---

## File Structure

### Files to Create
- `apps/web/src/lib/api/clients/entityLinksClient.ts` — API client for entity link endpoints
- `apps/web/src/components/game-detail/GameRelationships.tsx` — relationships display component
- `apps/web/src/components/game-detail/AddRelationshipDialog.tsx` — dialog for creating links
- `apps/web/src/__tests__/components/GameRelationships.test.tsx` — tests

### Files to Modify
- Game detail page — add relationships section
- Library game detail page — add relationships section

---

### Task 1: Create Entity Links API Client

**Files:**
- Create: `apps/web/src/lib/api/clients/entityLinksClient.ts`

- [ ] **Step 1: Create the client file**

```typescript
import type { HttpClient } from '../core/httpClient';

export type MeepleEntityType = 'Game' | 'Player' | 'Agent' | 'KbCard' | 'Collection' | 'Event';
export type EntityLinkType = 'ExpansionOf' | 'SequelOf' | 'Reimplements' | 'CompanionTo' | 'RelatedTo' | 'PartOf' | 'CollaboratesWith' | 'SpecializedBy';
export type EntityLinkScope = 'User' | 'System' | 'Admin';

export interface EntityLinkDto {
  id: string;
  sourceEntityType: MeepleEntityType;
  sourceEntityId: string;
  targetEntityType: MeepleEntityType;
  targetEntityId: string;
  linkType: EntityLinkType;
  scope: EntityLinkScope;
  metadata?: string;
  isOwner: boolean;
  isBidirectional: boolean;
}

export interface CreateEntityLinkRequest {
  sourceEntityType: MeepleEntityType;
  sourceEntityId: string;
  targetEntityType: MeepleEntityType;
  targetEntityId: string;
  linkType: EntityLinkType;
}

export function createEntityLinksClient({ httpClient }: { httpClient: HttpClient }) {
  return {
    async getLinks(params: {
      entityType: MeepleEntityType;
      entityId: string;
      linkType?: EntityLinkType;
      targetEntityType?: MeepleEntityType;
    }) {
      const qs = new URLSearchParams();
      qs.append('entityType', params.entityType);
      qs.append('entityId', params.entityId);
      if (params.linkType) qs.append('linkType', params.linkType);
      if (params.targetEntityType) qs.append('targetEntityType', params.targetEntityType);
      return httpClient.get<EntityLinkDto[]>(`/api/v1/library/entity-links?${qs}`);
    },
    async getLinkCount(entityType: MeepleEntityType, entityId: string) {
      const qs = new URLSearchParams({ entityType, entityId });
      return httpClient.get<number>(`/api/v1/library/entity-links/count?${qs}`);
    },
    async create(data: CreateEntityLinkRequest) {
      return httpClient.post<EntityLinkDto>('/api/v1/library/entity-links', data);
    },
    async delete(linkId: string) {
      return httpClient.delete(`/api/v1/library/entity-links/${linkId}`);
    },
  };
}
```

- [ ] **Step 2: Register in API barrel export**

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/
git commit -m "feat(entity-links): create entity links API client"
```

---

### Task 2: Create GameRelationships Component

**Files:**
- Create: `apps/web/src/components/game-detail/GameRelationships.tsx`

- [ ] **Step 1: Create the component**

```tsx
'use client';

import { useCallback, useEffect, useState } from 'react';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { api } from '@/lib/api';
import type { EntityLinkDto, EntityLinkType } from '@/lib/api/clients/entityLinksClient';

const LINK_TYPE_LABELS: Record<EntityLinkType, string> = {
  ExpansionOf: 'Espansioni',
  SequelOf: 'Sequel',
  Reimplements: 'Reimplementazioni',
  CompanionTo: 'Companion',
  RelatedTo: 'Giochi Correlati',
  PartOf: 'Parte di',
  CollaboratesWith: 'Collabora con',
  SpecializedBy: 'Specializzato da',
};

interface GameRelationshipsProps {
  gameId: string;
  gameName: string;
}

export function GameRelationships({ gameId, gameName }: GameRelationshipsProps) {
  const [links, setLinks] = useState<EntityLinkDto[]>([]);
  const [loading, setLoading] = useState(true);

  const loadLinks = useCallback(async () => {
    try {
      const data = await api.entityLinks.getLinks({
        entityType: 'Game',
        entityId: gameId,
      });
      setLinks(data);
    } catch {
      // Silent fail — relationships are supplementary
    } finally {
      setLoading(false);
    }
  }, [gameId]);

  useEffect(() => { loadLinks(); }, [loadLinks]);

  if (loading) return <div className="animate-pulse h-20" />;
  if (links.length === 0) return null;

  // Group by link type
  const grouped = links.reduce((acc, link) => {
    const type = link.linkType;
    if (!acc[type]) acc[type] = [];
    acc[type].push(link);
    return acc;
  }, {} as Record<string, EntityLinkDto[]>);

  return (
    <div className="space-y-4">
      {Object.entries(grouped).map(([type, typeLinks]) => (
        <div key={type}>
          <h3 className="text-sm font-semibold text-muted-foreground mb-2">
            {LINK_TYPE_LABELS[type as EntityLinkType] ?? type}
          </h3>
          <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
            {typeLinks.map(link => {
              const targetId = link.sourceEntityId === gameId
                ? link.targetEntityId
                : link.sourceEntityId;
              return (
                <MeepleCard
                  key={link.id}
                  entity="game"
                  variant="compact"
                  title={link.metadata ?? 'Linked Game'}
                  href={`/library/games/${targetId}`}
                />
              );
            })}
          </div>
        </div>
      ))}
    </div>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/components/game-detail/GameRelationships.tsx
git commit -m "feat(entity-links): create GameRelationships display component"
```

---

### Task 3: Create AddRelationshipDialog

**Files:**
- Create: `apps/web/src/components/game-detail/AddRelationshipDialog.tsx`

- [ ] **Step 1: Create the dialog component**

A dialog that allows users to search for a game and create a relationship link. Uses game search autocomplete + link type selector.

- [ ] **Step 2: Wire to GameRelationships component**

Add an "Add Relationship" button that opens the dialog (only for authenticated users).

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/game-detail/
git commit -m "feat(entity-links): create AddRelationshipDialog component"
```

---

### Task 4: Wire into Game Detail Pages

**Files:**
- Modify: Game detail page (shared games or library games)

- [ ] **Step 1: Read the game detail page structure**

Find where tabs or sections are rendered on `/games/[id]` or `/library/games/[gameId]`.

- [ ] **Step 2: Add GameRelationships section**

Add the component below the main game info, passing `gameId` and `gameName`.

```tsx
<GameRelationships gameId={game.id} gameName={game.title} />
```

- [ ] **Step 3: Test rendering**

Navigate to a game detail page and verify relationships load (or are hidden when empty).

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/
git commit -m "feat(entity-links): wire GameRelationships into game detail pages"
```

---

### Task 5: Write Tests

**Files:**
- Create: `apps/web/src/__tests__/components/GameRelationships.test.tsx`

- [ ] **Step 1: Write test for rendering grouped links**

```typescript
import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { GameRelationships } from '@/components/game-detail/GameRelationships';

vi.mock('@/lib/api', () => ({
  api: {
    entityLinks: {
      getLinks: vi.fn().mockResolvedValue([
        { id: '1', linkType: 'ExpansionOf', sourceEntityId: 'game-1', targetEntityId: 'game-2', metadata: 'Catan: Seafarers' },
        { id: '2', linkType: 'ExpansionOf', sourceEntityId: 'game-1', targetEntityId: 'game-3', metadata: 'Catan: Cities' },
        { id: '3', linkType: 'RelatedTo', sourceEntityId: 'game-1', targetEntityId: 'game-4', metadata: 'Settlers' },
      ]),
    },
  },
}));

describe('GameRelationships', () => {
  it('groups links by type', async () => {
    renderWithQuery(<GameRelationships gameId="game-1" gameName="Catan" />);
    // Wait for async load
    expect(await screen.findByText('Espansioni')).toBeInTheDocument();
    expect(screen.getByText('Giochi Correlati')).toBeInTheDocument();
  });

  it('renders nothing when no links', async () => {
    vi.mocked(api.entityLinks.getLinks).mockResolvedValueOnce([]);
    const { container } = renderWithQuery(<GameRelationships gameId="game-1" gameName="Catan" />);
    // After loading, component returns null
  });
});
```

- [ ] **Step 2: Run tests**

```bash
cd apps/web && pnpm test -- --grep "GameRelationships" --run
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/__tests__/
git commit -m "test(entity-links): add GameRelationships component tests"
```

---

### Task 6: Quality Checks and PR

- [ ] **Step 1: Run typecheck, lint, tests**

```bash
cd apps/web && pnpm typecheck && pnpm lint && pnpm test --run
```

- [ ] **Step 2: Fix any issues**

- [ ] **Step 3: Push and create PR**

```bash
git push -u origin feature/us43-entity-relationships
gh pr create --base frontend-dev --title "feat(entity-links): display game relationships (US-43)" --body "## Summary
- Create entity links API client
- GameRelationships component with grouped display
- AddRelationshipDialog for user-created links
- Wire into game detail pages

## User Story
US-43: Come utente, voglio vedere relazioni tra giochi (espansioni, varianti)

## Test Plan
- [ ] Expansions display grouped under 'Espansioni'
- [ ] Related games grouped under 'Giochi Correlati'
- [ ] Empty state hides the section
- [ ] User can add a new relationship link
- [ ] User can delete their own links"
```

---

## Backend Endpoints Reference

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/v1/library/entity-links` | Get links for entity |
| GET | `/api/v1/library/entity-links/count` | Count links |
| POST | `/api/v1/library/entity-links` | Create user-scope link |
| DELETE | `/api/v1/library/entity-links/{id}` | Delete own link |

## Link Types

| Type | Bidirectional | Example |
|------|--------------|---------|
| `ExpansionOf` | No | "Catan: Seafarers" → ExpansionOf → "Catan" |
| `SequelOf` | No | "Pandemic Legacy S2" → SequelOf → "Pandemic Legacy S1" |
| `Reimplements` | No | "Dune: Imperium" → Reimplements → "El Grande" |
| `CompanionTo` | Yes | "Codenames" ↔ CompanionTo ↔ "Codenames: Pictures" |
| `RelatedTo` | Yes | "Catan" ↔ RelatedTo ↔ "Settlers of America" |
| `PartOf` | No | "Module A" → PartOf → "Big Game Collection" |

**IMPORTANT**: EntityLinkType values are PascalCase: `'ExpansionOf'` not `'expansion_of'`
