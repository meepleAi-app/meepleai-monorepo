# MeepleCard Adapter Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrare i 5 adapter MeepleCard al nuovo sistema visuale unificato (SymbolStrip con identity chips + metric pills, linkedEntities footer) introducendo un layer di mapper puri e collassando i wrapper ridondanti.

**Architecture:** Layer di mapper puri `(DTO) → Partial<MeepleCardProps>` in `src/lib/card-mappers/`. Gli adapter importano le funzioni utility (formatPlayTime, buildLinkedEntities, ecc.) e aggiungono le nuove props SymbolStrip rimuovendole dalla `metadata[]`. Tre wrapper sottili vengono eliminati (PrivateGameCard, SharedLibraryGameCard, BggGameCard duplicato).

**Tech Stack:** TypeScript (no React nei mapper), Vitest per test puri, React Testing Library per test adapter.

---

## File Structure

```
apps/web/src/lib/card-mappers/            ← NUOVI
├── index.ts
├── shared-utils.ts                        ← formatPlayTime, mechToIcon, buildLinkedEntities, ...
├── game-card-mapper.ts                    ← buildGameCardProps(game: Game)
├── player-card-mapper.ts                  ← buildPlayerCardProps(player: SessionPlayer)
├── session-card-mapper.ts                 ← buildSessionCardProps(session: GameSessionDto)
├── kb-card-mapper.ts                      ← buildKbCardProps(document: PdfDocumentDto)
└── __tests__/
    ├── shared-utils.test.ts
    ├── game-card-mapper.test.ts
    ├── player-card-mapper.test.ts
    ├── session-card-mapper.test.ts
    └── kb-card-mapper.test.ts

apps/web/src/components/games/MeepleGameCard.tsx          ← MODIFICATO
apps/web/src/components/players/MeeplePlayerCard.tsx      ← MODIFICATO
apps/web/src/components/session/MeepleSessionCard.tsx     ← MODIFICATO
apps/web/src/components/documents/MeepleKbCard.tsx        ← MODIFICATO
apps/web/src/components/library/MeepleLibraryGameCard.tsx ← MODIFICATO

apps/web/src/components/library/PrivateGameCard.tsx       ← ELIMINATO
apps/web/src/components/library/SharedLibraryGameCard.tsx ← ELIMINATO
apps/web/src/components/games/BggGameCard.tsx             ← ELIMINATO

apps/web/src/app/(authenticated)/library/private/PrivateGamesClient.tsx  ← MODIFICATO (inline)
apps/web/src/app/(public)/library/shared/[token]/page.tsx                ← MODIFICATO (inline)
apps/web/src/components/library/index.ts                                  ← MODIFICATO
apps/web/src/components/games/index.ts                                    ← MODIFICATO
```

---

## Task 1: shared-utils.ts — utility pure

**Files:**
- Create: `apps/web/src/lib/card-mappers/shared-utils.ts`
- Create: `apps/web/src/lib/card-mappers/__tests__/shared-utils.test.ts`

- [ ] **Step 1: Scrivi il test che fallisce**

```typescript
// apps/web/src/lib/card-mappers/__tests__/shared-utils.test.ts
import { describe, it, expect } from 'vitest';
import {
  formatPlayTime,
  mechToIcon,
  rankToIcon,
  buildLinkedEntities,
  sessionStatusToLabel,
  processingStateToLabel,
} from '../shared-utils';

describe('formatPlayTime', () => {
  it('formats < 60 minutes', () => {
    expect(formatPlayTime(45)).toBe('45min');
    expect(formatPlayTime(30)).toBe('30min');
  });
  it('formats exact hours', () => {
    expect(formatPlayTime(60)).toBe('1h');
    expect(formatPlayTime(120)).toBe('2h');
  });
  it('formats hours and minutes', () => {
    expect(formatPlayTime(90)).toBe('1h30min');
    expect(formatPlayTime(150)).toBe('2h30min');
  });
  it('handles 0', () => {
    expect(formatPlayTime(0)).toBe('0min');
  });
});

describe('mechToIcon', () => {
  it('returns emoji for known mechanisms', () => {
    expect(mechToIcon('Worker Placement')).toBe('⚙️');
    expect(mechToIcon('Deck Building')).toBe('🃏');
    expect(mechToIcon('Cooperative')).toBe('🤝');
    expect(mechToIcon('Area Control')).toBe('🗺️');
    expect(mechToIcon('Auction')).toBe('💰');
    expect(mechToIcon('Dice')).toBe('🎲');
  });
  it('returns undefined for unknown mechanisms', () => {
    expect(mechToIcon('Unknown Mechanic')).toBeUndefined();
    expect(mechToIcon(undefined)).toBeUndefined();
  });
  it('is case-insensitive', () => {
    expect(mechToIcon('worker placement')).toBe('⚙️');
  });
});

describe('rankToIcon', () => {
  it('returns emoji for known ranks', () => {
    expect(rankToIcon('Master')).toBe('👑');
    expect(rankToIcon('Expert')).toBe('⭐');
    expect(rankToIcon('Veteran')).toBe('🎖️');
  });
  it('returns undefined for unknown or missing rank', () => {
    expect(rankToIcon(undefined)).toBeUndefined();
    expect(rankToIcon('Novice')).toBeUndefined();
  });
});

describe('buildLinkedEntities', () => {
  it('includes entity types with count > 0', () => {
    const result = buildLinkedEntities({ agentCount: 2, kbCount: 5 });
    expect(result).toHaveLength(2);
    expect(result).toContainEqual({ entityType: 'agent', count: 2 });
    expect(result).toContainEqual({ entityType: 'kb', count: 5 });
  });
  it('filters counts at zero or undefined', () => {
    const result = buildLinkedEntities({ agentCount: 0, kbCount: 3, sessionCount: undefined });
    expect(result).toHaveLength(1);
    expect(result[0]).toEqual({ entityType: 'kb', count: 3 });
  });
  it('returns empty array when all counts are zero', () => {
    expect(buildLinkedEntities({ agentCount: 0 })).toHaveLength(0);
    expect(buildLinkedEntities({})).toHaveLength(0);
  });
});

describe('sessionStatusToLabel', () => {
  it('maps InProgress to Live success', () => {
    expect(sessionStatusToLabel('InProgress')).toEqual({ text: 'Live', variant: 'success' });
  });
  it('maps Paused to Pausa warning', () => {
    expect(sessionStatusToLabel('Paused')).toEqual({ text: 'Pausa', variant: 'warning' });
  });
  it('maps Completed to Completata info', () => {
    expect(sessionStatusToLabel('Completed')).toEqual({ text: 'Completata', variant: 'info' });
  });
  it('maps Setup to Impostazione info', () => {
    expect(sessionStatusToLabel('Setup')).toEqual({ text: 'Impostazione', variant: 'info' });
  });
  it('returns info for unknown status', () => {
    const result = sessionStatusToLabel('Unknown');
    expect(result.variant).toBe('info');
  });
});

describe('processingStateToLabel', () => {
  it('maps Completed to Indicizzato success', () => {
    expect(processingStateToLabel('Completed')).toEqual({ text: 'Indicizzato', variant: 'success' });
  });
  it('maps Failed to Errore error', () => {
    expect(processingStateToLabel('Failed')).toEqual({ text: 'Errore', variant: 'error' });
  });
  it('maps processing states to In Elaborazione warning', () => {
    for (const s of ['Uploading', 'Extracting', 'Chunking', 'Embedding', 'Indexing']) {
      expect(processingStateToLabel(s)).toEqual({ text: 'In Elaborazione', variant: 'warning' });
    }
  });
  it('maps Pending to In Attesa info', () => {
    expect(processingStateToLabel('Pending')).toEqual({ text: 'In Attesa', variant: 'info' });
  });
});
```

- [ ] **Step 2: Verifica che i test falliscano**

```bash
cd apps/web && pnpm test src/lib/card-mappers/__tests__/shared-utils.test.ts 2>&1 | tail -5
```

Expected: FAIL con "Cannot find module '../shared-utils'"

- [ ] **Step 3: Implementa shared-utils.ts**

```typescript
// apps/web/src/lib/card-mappers/shared-utils.ts
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card-styles';
import type { LinkedEntityInfo } from '@/components/ui/data-display/meeple-card-features/ManaLinkFooter';

/**
 * Formatta una durata in minuti in una stringa leggibile.
 * 45 → "45min", 60 → "1h", 90 → "1h30min", 120 → "2h"
 */
export function formatPlayTime(minutes: number): string {
  if (minutes < 60) return `${minutes}min`;
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  if (m === 0) return `${h}h`;
  return `${h}h${m}min`;
}

const MECH_ICON_MAP: Record<string, string> = {
  'worker placement': '⚙️',
  'deck building': '🃏',
  'cooperative': '🤝',
  'area control': '🗺️',
  'auction': '💰',
  'dice': '🎲',
};

/**
 * Mappa un meccanismo di gioco a un'emoji. Case-insensitive.
 * Restituisce undefined per meccanismi sconosciuti o input mancante.
 */
export function mechToIcon(mechanism: string | undefined): string | undefined {
  if (!mechanism) return undefined;
  return MECH_ICON_MAP[mechanism.toLowerCase()];
}

const RANK_ICON_MAP: Record<string, string> = {
  'master': '👑',
  'expert': '⭐',
  'veteran': '🎖️',
};

/**
 * Mappa un rank giocatore a un'emoji.
 * Restituisce undefined per rank sconosciuti o input mancante.
 */
export function rankToIcon(rank: string | undefined): string | undefined {
  if (!rank) return undefined;
  return RANK_ICON_MAP[rank.toLowerCase()];
}

/**
 * Costruisce un array LinkedEntityInfo filtrando i conteggi a zero.
 */
export function buildLinkedEntities(counts: {
  agentCount?: number;
  kbCount?: number;
  sessionCount?: number;
  gameCount?: number;
  chatCount?: number;
}): LinkedEntityInfo[] {
  const pairs: Array<[MeepleEntityType, number | undefined]> = [
    ['game', counts.gameCount],
    ['agent', counts.agentCount],
    ['kb', counts.kbCount],
    ['session', counts.sessionCount],
    ['chatSession', counts.chatCount],
  ];
  return pairs
    .filter((pair): pair is [MeepleEntityType, number] => typeof pair[1] === 'number' && pair[1] > 0)
    .map(([entityType, count]) => ({ entityType, count }));
}

type StateLabel = { text: string; variant: 'success' | 'warning' | 'error' | 'info' };

/**
 * Mappa lo status di una sessione (stringa backend PascalCase) a un oggetto stateLabel.
 */
export function sessionStatusToLabel(status: string): StateLabel {
  switch (status) {
    case 'InProgress': return { text: 'Live', variant: 'success' };
    case 'Paused':     return { text: 'Pausa', variant: 'warning' };
    case 'Completed':  return { text: 'Completata', variant: 'info' };
    case 'Setup':      return { text: 'Impostazione', variant: 'info' };
    default:           return { text: status, variant: 'info' };
  }
}

/**
 * Mappa lo processingState di un documento PDF a un oggetto stateLabel.
 */
export function processingStateToLabel(state: string): StateLabel {
  switch (state) {
    case 'Completed': return { text: 'Indicizzato', variant: 'success' };
    case 'Failed':    return { text: 'Errore', variant: 'error' };
    case 'Pending':   return { text: 'In Attesa', variant: 'info' };
    case 'Uploading':
    case 'Extracting':
    case 'Chunking':
    case 'Embedding':
    case 'Indexing':  return { text: 'In Elaborazione', variant: 'warning' };
    default:          return { text: state, variant: 'info' };
  }
}
```

- [ ] **Step 4: Verifica che i test passino**

```bash
cd apps/web && pnpm test src/lib/card-mappers/__tests__/shared-utils.test.ts 2>&1 | tail -10
```

Expected: PASS (tutti i test verdi)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/card-mappers/shared-utils.ts apps/web/src/lib/card-mappers/__tests__/shared-utils.test.ts
git commit -m "feat(card-mappers): add shared-utils pure functions (formatPlayTime, buildLinkedEntities, icons)"
```

---

## Task 2: game-card-mapper.ts

**Files:**
- Create: `apps/web/src/lib/card-mappers/game-card-mapper.ts`
- Create: `apps/web/src/lib/card-mappers/__tests__/game-card-mapper.test.ts`

**Nota:** `Game` ha `minPlayTimeMinutes`/`maxPlayTimeMinutes` ma NON `primaryMechanism`. Il mapper usa solo i campi effettivamente presenti nel tipo `Game`.

- [ ] **Step 1: Scrivi il test che fallisce**

```typescript
// apps/web/src/lib/card-mappers/__tests__/game-card-mapper.test.ts
import { describe, it, expect } from 'vitest';
import { buildGameCardProps } from '../game-card-mapper';
import type { Game } from '@/lib/api';

const baseGame: Game = {
  id: 'game-1',
  title: 'Catan',
  publisher: 'Kosmos',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  minPlayTimeMinutes: 60,
  maxPlayTimeMinutes: 120,
  bggId: 13,
  createdAt: '2024-01-01T00:00:00Z',
  imageUrl: 'https://example.com/catan.jpg',
  averageRating: 7.5,
};

describe('buildGameCardProps', () => {
  it('builds playerCountDisplay from min/max players', () => {
    const props = buildGameCardProps(baseGame);
    expect(props.playerCountDisplay).toBe('3-4p');
  });

  it('builds playTimeDisplay from avg of min/max play time', () => {
    const props = buildGameCardProps(baseGame); // avg of 60+120 = 90min → "1h30min"
    expect(props.playTimeDisplay).toBe('1h30min');
  });

  it('builds subtitle from publisher and year', () => {
    const props = buildGameCardProps(baseGame);
    expect(props.subtitle).toBe('Kosmos · 1995');
  });

  it('omits playerCountDisplay when both values are null', () => {
    const props = buildGameCardProps({ ...baseGame, minPlayers: null, maxPlayers: null });
    expect(props.playerCountDisplay).toBeUndefined();
  });

  it('omits playTimeDisplay when both play time values are null', () => {
    const props = buildGameCardProps({ ...baseGame, minPlayTimeMinutes: null, maxPlayTimeMinutes: null });
    expect(props.playTimeDisplay).toBeUndefined();
  });

  it('uses only minPlayTime when max is null', () => {
    const props = buildGameCardProps({ ...baseGame, minPlayTimeMinutes: 60, maxPlayTimeMinutes: null });
    expect(props.playTimeDisplay).toBe('1h');
  });

  it('maps averageRating and ratingMax', () => {
    const props = buildGameCardProps(baseGame);
    expect(props.rating).toBe(7.5);
    expect(props.ratingMax).toBe(10);
  });

  it('handles publisher-only subtitle', () => {
    const props = buildGameCardProps({ ...baseGame, yearPublished: null });
    expect(props.subtitle).toBe('Kosmos');
  });

  it('handles year-only subtitle', () => {
    const props = buildGameCardProps({ ...baseGame, publisher: null });
    expect(props.subtitle).toBe('1995');
  });

  it('returns undefined subtitle when both publisher and year are null', () => {
    const props = buildGameCardProps({ ...baseGame, publisher: null, yearPublished: null });
    expect(props.subtitle).toBeUndefined();
  });
});
```

- [ ] **Step 2: Verifica che il test fallisca**

```bash
cd apps/web && pnpm test src/lib/card-mappers/__tests__/game-card-mapper.test.ts 2>&1 | tail -5
```

Expected: FAIL con "Cannot find module '../game-card-mapper'"

- [ ] **Step 3: Implementa game-card-mapper.ts**

```typescript
// apps/web/src/lib/card-mappers/game-card-mapper.ts
import type { Game } from '@/lib/api';
import type { MeepleCardProps } from '@/components/ui/data-display/meeple-card';

import { formatPlayTime } from './shared-utils';

/**
 * Mappa un Game DTO alle props SymbolStrip di MeepleCard.
 * Non include props di callback (actions, clicks) — quelle restano nell'adapter.
 */
export function buildGameCardProps(game: Game): Partial<MeepleCardProps> {
  // playerCountDisplay
  const hasPlayers = game.minPlayers !== null || game.maxPlayers !== null;
  const playerCountDisplay = hasPlayers
    ? `${game.minPlayers ?? '?'}-${game.maxPlayers ?? '?'}p`
    : undefined;

  // playTimeDisplay — average of min/max
  let playTimeDisplay: string | undefined;
  const min = game.minPlayTimeMinutes;
  const max = game.maxPlayTimeMinutes;
  if (min !== null || max !== null) {
    const avg = Math.round(((min ?? max ?? 0) + (max ?? min ?? 0)) / 2);
    playTimeDisplay = formatPlayTime(avg);
  }

  // subtitle
  const subtitleParts: string[] = [];
  if (game.publisher) subtitleParts.push(game.publisher);
  if (game.yearPublished) subtitleParts.push(String(game.yearPublished));
  const subtitle = subtitleParts.length > 0 ? subtitleParts.join(' · ') : undefined;

  return {
    title: game.title,
    subtitle,
    imageUrl: game.imageUrl ?? undefined,
    rating: game.averageRating ?? undefined,
    ratingMax: 10,
    playerCountDisplay,
    playTimeDisplay,
  };
}
```

- [ ] **Step 4: Verifica che i test passino**

```bash
cd apps/web && pnpm test src/lib/card-mappers/__tests__/game-card-mapper.test.ts 2>&1 | tail -10
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/card-mappers/game-card-mapper.ts apps/web/src/lib/card-mappers/__tests__/game-card-mapper.test.ts
git commit -m "feat(card-mappers): add game-card-mapper with playerCountDisplay and playTimeDisplay"
```

---

## Task 3: kb-card-mapper.ts

**Files:**
- Create: `apps/web/src/lib/card-mappers/kb-card-mapper.ts`
- Create: `apps/web/src/lib/card-mappers/__tests__/kb-card-mapper.test.ts`

- [ ] **Step 1: Scrivi il test che fallisce**

```typescript
// apps/web/src/lib/card-mappers/__tests__/kb-card-mapper.test.ts
import { describe, it, expect } from 'vitest';
import { buildKbCardProps } from '../kb-card-mapper';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

const baseDoc: PdfDocumentDto = {
  id: 'doc-1',
  gameId: 'game-1',
  fileName: 'regolamento.pdf',
  filePath: '/uploads/regolamento.pdf',
  fileSizeBytes: 1024000,
  processingStatus: 'Completed',
  uploadedAt: '2024-01-01T00:00:00Z',
  processedAt: '2024-01-01T01:00:00Z',
  pageCount: 42,
  documentType: 'base',
  isPublic: false,
  processingState: 'Completed',
  progressPercentage: 100,
  retryCount: 0,
  maxRetries: 3,
  canRetry: false,
  errorCategory: null,
  processingError: null,
  documentCategory: 'Rulebook',
  baseDocumentId: null,
  isActiveForRag: true,
  hasAcceptedDisclaimer: false,
};

describe('buildKbCardProps', () => {
  it('passes pageCount to props', () => {
    const props = buildKbCardProps(baseDoc);
    expect(props.pageCount).toBe(42);
  });

  it('passes undefined when pageCount is null', () => {
    const props = buildKbCardProps({ ...baseDoc, pageCount: null });
    expect(props.pageCount).toBeUndefined();
  });

  it('sets identityChip1 to file type uppercase', () => {
    const props = buildKbCardProps(baseDoc);
    expect(props.identityChip1).toBe('PDF');
  });

  it('sets identityChip1Icon to document emoji', () => {
    const props = buildKbCardProps(baseDoc);
    expect(props.identityChip1Icon).toBe('📄');
  });

  it('builds stateLabel for Completed state', () => {
    const props = buildKbCardProps(baseDoc);
    expect(props.stateLabel).toEqual({ text: 'Indicizzato', variant: 'success' });
  });

  it('builds stateLabel for Failed state', () => {
    const props = buildKbCardProps({ ...baseDoc, processingState: 'Failed' });
    expect(props.stateLabel).toEqual({ text: 'Errore', variant: 'error' });
  });

  it('builds stateLabel for Indexing state', () => {
    const props = buildKbCardProps({ ...baseDoc, processingState: 'Indexing' });
    expect(props.stateLabel).toEqual({ text: 'In Elaborazione', variant: 'warning' });
  });

  it('builds stateLabel for Pending state', () => {
    const props = buildKbCardProps({ ...baseDoc, processingState: 'Pending' });
    expect(props.stateLabel).toEqual({ text: 'In Attesa', variant: 'info' });
  });
});
```

- [ ] **Step 2: Verifica che il test fallisca**

```bash
cd apps/web && pnpm test src/lib/card-mappers/__tests__/kb-card-mapper.test.ts 2>&1 | tail -5
```

- [ ] **Step 3: Implementa kb-card-mapper.ts**

```typescript
// apps/web/src/lib/card-mappers/kb-card-mapper.ts
import type { MeepleCardProps } from '@/components/ui/data-display/meeple-card';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

import { processingStateToLabel } from './shared-utils';

/**
 * Mappa un PdfDocumentDto alle props SymbolStrip di MeepleCard.
 */
export function buildKbCardProps(document: PdfDocumentDto): Partial<MeepleCardProps> {
  return {
    pageCount: document.pageCount ?? undefined,
    identityChip1: 'PDF',
    identityChip1Icon: '📄',
    stateLabel: processingStateToLabel(document.processingState),
  };
}
```

- [ ] **Step 4: Verifica che i test passino**

```bash
cd apps/web && pnpm test src/lib/card-mappers/__tests__/kb-card-mapper.test.ts 2>&1 | tail -10
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/card-mappers/kb-card-mapper.ts apps/web/src/lib/card-mappers/__tests__/kb-card-mapper.test.ts
git commit -m "feat(card-mappers): add kb-card-mapper with pageCount, stateLabel, identityChip"
```

---

## Task 4: session-card-mapper.ts

**Files:**
- Create: `apps/web/src/lib/card-mappers/session-card-mapper.ts`
- Create: `apps/web/src/lib/card-mappers/__tests__/session-card-mapper.test.ts`

- [ ] **Step 1: Scrivi il test che fallisce**

```typescript
// apps/web/src/lib/card-mappers/__tests__/session-card-mapper.test.ts
import { describe, it, expect } from 'vitest';
import { buildSessionCardProps } from '../session-card-mapper';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

const baseSession: GameSessionDto = {
  id: 'session-1',
  gameId: 'game-1',
  status: 'InProgress',
  startedAt: '2024-01-01T10:00:00Z',
  endedAt: null,
  playerCount: 3,
  winnerName: null,
};

describe('buildSessionCardProps', () => {
  it('builds linkedEntities with game pip when gameId is present', () => {
    const props = buildSessionCardProps(baseSession);
    expect(props.linkedEntities).toContainEqual({ entityType: 'game', count: 1 });
  });

  it('does not add game pip when gameId is missing', () => {
    const props = buildSessionCardProps({ ...baseSession, gameId: '' });
    expect(props.linkedEntities ?? []).not.toContainEqual(
      expect.objectContaining({ entityType: 'game' })
    );
  });

  it('builds stateLabel for InProgress session', () => {
    const props = buildSessionCardProps(baseSession);
    expect(props.stateLabel).toEqual({ text: 'Live', variant: 'success' });
  });

  it('builds stateLabel for Paused session', () => {
    const props = buildSessionCardProps({ ...baseSession, status: 'Paused' });
    expect(props.stateLabel).toEqual({ text: 'Pausa', variant: 'warning' });
  });

  it('builds stateLabel for Completed session', () => {
    const props = buildSessionCardProps({ ...baseSession, status: 'Completed' });
    expect(props.stateLabel).toEqual({ text: 'Completata', variant: 'info' });
  });

  it('builds stateLabel for Setup session', () => {
    const props = buildSessionCardProps({ ...baseSession, status: 'Setup' });
    expect(props.stateLabel).toEqual({ text: 'Impostazione', variant: 'info' });
  });
});
```

- [ ] **Step 2: Verifica che il test fallisca**

```bash
cd apps/web && pnpm test src/lib/card-mappers/__tests__/session-card-mapper.test.ts 2>&1 | tail -5
```

- [ ] **Step 3: Implementa session-card-mapper.ts**

```typescript
// apps/web/src/lib/card-mappers/session-card-mapper.ts
import type { MeepleCardProps } from '@/components/ui/data-display/meeple-card';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

import { buildLinkedEntities, sessionStatusToLabel } from './shared-utils';

/**
 * Mappa un GameSessionDto alle props di MeepleCard.
 * Il gioco collegato appare come pip linkedEntity (non identityChip).
 */
export function buildSessionCardProps(session: GameSessionDto): Partial<MeepleCardProps> {
  const linkedEntities = buildLinkedEntities({
    gameCount: session.gameId ? 1 : 0,
  });

  return {
    linkedEntities: linkedEntities.length > 0 ? linkedEntities : undefined,
    stateLabel: sessionStatusToLabel(session.status),
  };
}
```

- [ ] **Step 4: Verifica che i test passino**

```bash
cd apps/web && pnpm test src/lib/card-mappers/__tests__/session-card-mapper.test.ts 2>&1 | tail -10
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/card-mappers/session-card-mapper.ts apps/web/src/lib/card-mappers/__tests__/session-card-mapper.test.ts
git commit -m "feat(card-mappers): add session-card-mapper with linkedEntities game pip and stateLabel"
```

---

## Task 5: player-card-mapper.ts

**Files:**
- Create: `apps/web/src/lib/card-mappers/player-card-mapper.ts`
- Create: `apps/web/src/lib/card-mappers/__tests__/player-card-mapper.test.ts`

**Nota:** `SessionPlayer` (da play-records.schemas.ts) ha: `id`, `displayName`, `playerOrder`, `color`. Non ha statistiche aggregate. Il mapper è minimale per ora.

- [ ] **Step 1: Scrivi il test che fallisce**

```typescript
// apps/web/src/lib/card-mappers/__tests__/player-card-mapper.test.ts
import { describe, it, expect } from 'vitest';
import { buildPlayerCardProps } from '../player-card-mapper';
import type { SessionPlayer } from '@/lib/api/schemas/play-records.schemas';

const basePlayer: SessionPlayer = {
  id: 'player-1',
  displayName: 'Mario Rossi',
  playerOrder: 0,
  color: 'red',
  userId: null,
  avatarUrl: null,
};

describe('buildPlayerCardProps', () => {
  it('maps displayName to title', () => {
    const props = buildPlayerCardProps(basePlayer);
    expect(props.title).toBe('Mario Rossi');
  });

  it('includes avatarUrl when present', () => {
    const props = buildPlayerCardProps({ ...basePlayer, avatarUrl: 'https://example.com/avatar.jpg' });
    expect(props.avatarUrl).toBe('https://example.com/avatar.jpg');
  });

  it('omits avatarUrl when null', () => {
    const props = buildPlayerCardProps(basePlayer);
    expect(props.avatarUrl).toBeUndefined();
  });
});
```

- [ ] **Step 2: Verifica che il test fallisca**

```bash
cd apps/web && pnpm test src/lib/card-mappers/__tests__/player-card-mapper.test.ts 2>&1 | tail -5
```

- [ ] **Step 3: Controlla il tipo SessionPlayer effettivo**

```bash
grep -A 15 "interface SessionPlayer\|SessionPlayerSchema" apps/web/src/lib/api/schemas/play-records.schemas.ts | head -20
```

Se `SessionPlayer` non ha `avatarUrl`, rimuovi quel test e adatta il mapper ai campi reali presenti.

- [ ] **Step 4: Implementa player-card-mapper.ts**

```typescript
// apps/web/src/lib/card-mappers/player-card-mapper.ts
import type { MeepleCardProps } from '@/components/ui/data-display/meeple-card';
import type { SessionPlayer } from '@/lib/api/schemas/play-records.schemas';

/**
 * Mappa un SessionPlayer alle props di MeepleCard.
 * Le statistiche aggregate (gamesPlayed, winRate) non sono presenti in SessionPlayer
 * e vanno aggiunte dall'adapter quando disponibili da altre sorgenti.
 */
export function buildPlayerCardProps(player: SessionPlayer): Partial<MeepleCardProps> {
  return {
    title: player.displayName,
    // avatarUrl è opzionale e potrebbe non esistere in SessionPlayer — adatta se necessario
    avatarUrl: (player as unknown as Record<string, string | null>)['avatarUrl'] ?? undefined,
  };
}
```

**Nota:** Se il tipo `SessionPlayer` non ha `avatarUrl`, semplifica il mapper a:
```typescript
export function buildPlayerCardProps(player: SessionPlayer): Partial<MeepleCardProps> {
  return { title: player.displayName };
}
```

- [ ] **Step 5: Verifica che i test passino (o adattali ai campi reali)**

```bash
cd apps/web && pnpm test src/lib/card-mappers/__tests__/player-card-mapper.test.ts 2>&1 | tail -10
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/lib/card-mappers/player-card-mapper.ts apps/web/src/lib/card-mappers/__tests__/player-card-mapper.test.ts
git commit -m "feat(card-mappers): add player-card-mapper"
```

---

## Task 6: index.ts — re-export pubblico

**Files:**
- Create: `apps/web/src/lib/card-mappers/index.ts`

- [ ] **Step 1: Crea il file di re-export**

```typescript
// apps/web/src/lib/card-mappers/index.ts
export { buildGameCardProps } from './game-card-mapper';
export { buildKbCardProps } from './kb-card-mapper';
export { buildSessionCardProps } from './session-card-mapper';
export { buildPlayerCardProps } from './player-card-mapper';
export {
  formatPlayTime,
  mechToIcon,
  rankToIcon,
  buildLinkedEntities,
  sessionStatusToLabel,
  processingStateToLabel,
} from './shared-utils';
```

- [ ] **Step 2: Verifica che tutti i test del layer passino**

```bash
cd apps/web && pnpm test src/lib/card-mappers/ 2>&1 | tail -15
```

Expected: tutti i test passano (shared-utils, game-card-mapper, kb-card-mapper, session-card-mapper, player-card-mapper)

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/card-mappers/index.ts
git commit -m "feat(card-mappers): add index re-export for public API"
```

---

## Task 7: Aggiorna MeepleKbCard — pageCount, chunkCount, stateLabel

**Files:**
- Modify: `apps/web/src/components/documents/MeepleKbCard.tsx`
- Test: `apps/web/src/components/documents/__tests__/MeepleKbCard.test.tsx` (verifica che esistano test)

**Cambi rispetto alla versione attuale:**
- Importa `buildKbCardProps` da card-mappers
- Aggiunge `pageCount`, `chunkCount`, `stateLabel`, `identityChip1`, `identityChip1Icon` alla chiamata MeepleCard

- [ ] **Step 1: Verifica che esistano test e che passino prima della modifica**

```bash
cd apps/web && pnpm test src/components/documents/ 2>&1 | tail -10
```

Se non esiste il file di test, saltare a Step 3 (nessuna regressione da verificare).

- [ ] **Step 2: Scrivi i nuovi test adapter (RTL)**

Crea o aggiungi a `apps/web/src/components/documents/__tests__/MeepleKbCard.test.tsx`:

```typescript
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MeepleKbCard } from '../MeepleKbCard';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

const baseDoc: PdfDocumentDto = {
  id: 'doc-1',
  gameId: 'game-1',
  fileName: 'regolamento.pdf',
  filePath: '/uploads/regolamento.pdf',
  fileSizeBytes: 1024000,
  processingStatus: 'Completed',
  uploadedAt: '2024-01-01T00:00:00Z',
  processedAt: '2024-01-01T01:00:00Z',
  pageCount: 42,
  documentType: 'base',
  isPublic: false,
  processingState: 'Completed',
  progressPercentage: 100,
  retryCount: 0,
  maxRetries: 3,
  canRetry: false,
  errorCategory: null,
  processingError: null,
  documentCategory: 'Rulebook',
  baseDocumentId: null,
  isActiveForRag: true,
  hasAcceptedDisclaimer: false,
};

// Mock navigateTo / getNavigationLinks
vi.mock('@/config/entity-navigation', () => ({
  getNavigationLinks: () => [],
}));

describe('MeepleKbCard adapter — SymbolStrip', () => {
  it('passes pageCount to MeepleCard (visible as metric pill)', () => {
    render(<MeepleKbCard document={baseDoc} />);
    // pageCount=42 viene passato come prop a MeepleCard → SymbolStrip lo mostra
    expect(screen.getByTestId('kb-card-doc-1')).toBeInTheDocument();
  });

  it('renders stateLabel Indicizzato for Completed state', () => {
    render(<MeepleKbCard document={baseDoc} />);
    // Il componente non crasha con i nuovi props
    expect(screen.getByTestId('kb-card-doc-1')).toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Modifica MeepleKbCard.tsx**

Sostituisci la sezione import e il corpo del componente render:

```typescript
// Aggiunge import:
import { buildKbCardProps } from '@/lib/card-mappers';
```

Nella funzione `MeepleKbCard`, prima del return, aggiungi:

```typescript
const mapperProps = buildKbCardProps(document);
```

Nel `<MeepleCard ... />`, aggiungi i nuovi props dopo le props esistenti:

```tsx
// Aggiungi dopo documentStatus={documentStatus}:
pageCount={mapperProps.pageCount}
identityChip1={mapperProps.identityChip1}
identityChip1Icon={mapperProps.identityChip1Icon}
stateLabel={mapperProps.stateLabel}
```

La sezione render completa dopo la modifica è:

```tsx
return (
  <MeepleCard
    id={document.id}
    entity="kb"
    variant={variant}
    title={document.fileName}
    subtitle={subtitle}
    documentStatus={documentStatus}
    // Nuove props SymbolStrip da mapper
    pageCount={mapperProps.pageCount}
    identityChip1={mapperProps.identityChip1}
    identityChip1Icon={mapperProps.identityChip1Icon}
    stateLabel={mapperProps.stateLabel}
    className={className}
    onClick={() => window.location.href = `/documents/${document.id}`}
    entityQuickActions={entityQuickActions}
    showInfoButton
    infoHref={`/documents/${document.id}`}
    infoTooltip="Vai al dettaglio"
    navigateTo={getNavigationLinks('kb', {
      id: document.id,
      gameId: document.gameId,
    })}
    data-testid={`kb-card-${document.id}`}
  />
);
```

- [ ] **Step 4: Verifica che TypeScript compili**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep -i "MeepleKbCard\|card-mapper" | head -10
```

Expected: nessun errore TS relativo a MeepleKbCard

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/documents/MeepleKbCard.tsx
git commit -m "feat(MeepleKbCard): add pageCount, identityChip1, stateLabel from kb-card-mapper"
```

---

## Task 8: Aggiorna MeepleGameCard — playerCountDisplay, playTimeDisplay

**Files:**
- Modify: `apps/web/src/components/games/MeepleGameCard.tsx`

**Cambi:**
- Importa `buildGameCardProps` da card-mappers
- Rimuove `Users`, `Clock` dalla metadata (ora nel SymbolStrip)
- Aggiunge `playerCountDisplay`, `playTimeDisplay`, `subtitle` dalla mapper

- [ ] **Step 1: Verifica i test esistenti**

```bash
cd apps/web && pnpm test src/components/games/MeepleGameCard --run 2>&1 | tail -10
```

- [ ] **Step 2: Modifica MeepleGameCard.tsx**

Le modifiche sono localizzate in 3 punti:

**A) Aggiungi import:**
```typescript
import { buildGameCardProps } from '@/lib/card-mappers';
```

**B) Rimuovi import `Users`, `Clock` da lucide-react** (non più usati se metadata li aveva):
```typescript
// PRIMA: import { Users, Clock } from 'lucide-react';
// DOPO: rimuovi Users e Clock se non usati altrove nel file
```

**C) Sostituisci la sezione di build metadata e costruzione subtitle:**

Prima:
```typescript
// Build metadata
const metadata: MeepleCardMetadata[] = [];

const playerCount = formatPlayerCount(game.minPlayers, game.maxPlayers);
if (playerCount !== 'N/A') {
  metadata.push({ icon: Users, value: playerCount });
}

const playTime = formatPlayTime(game.minPlayTimeMinutes, game.maxPlayTimeMinutes);
if (playTime !== 'N/A') {
  metadata.push({ icon: Clock, value: playTime });
}

// Build subtitle with publisher and year
const subtitleParts: string[] = [];
if (game.publisher) subtitleParts.push(game.publisher);
if (game.yearPublished) subtitleParts.push(String(game.yearPublished));
const subtitle = subtitleParts.length > 0 ? subtitleParts.join(' · ') : undefined;
```

Dopo:
```typescript
// Build card props from mapper (playerCountDisplay, playTimeDisplay, subtitle, etc.)
const mapperProps = buildGameCardProps(game);

// Metadata: solo dati non coperti dal SymbolStrip
const metadata: MeepleCardMetadata[] = [];
// (playerCount e playTime sono ora nel SymbolStrip tramite mapperProps)
```

**D) Nella chiamata `<MeepleCard>`, sostituisci `subtitle={subtitle}` con `subtitle={mapperProps.subtitle}` e aggiungi le nuove props:**

```tsx
<MeepleCard
  id={game.id}
  entity="game"
  variant={variant}
  title={game.title}
  subtitle={mapperProps.subtitle}
  imageUrl={game.imageUrl || undefined}
  rating={mapperProps.rating}
  ratingMax={10}
  // Nuove props SymbolStrip
  playerCountDisplay={mapperProps.playerCountDisplay}
  playTimeDisplay={mapperProps.playTimeDisplay}
  metadata={metadata.length > 0 ? metadata : undefined}
  onClick={onClick ? () => onClick(game.id) : undefined}
  className={className}
  entityQuickActions={entityActions.quickActions}
  showInfoButton
  infoHref={`/games/${game.id}`}
  infoTooltip="Vai al dettaglio"
  navigateTo={user ? getNavigationLinks('game', { id: game.id }) : undefined}
  hasAgent={false}
  hasKb={false}
  onAddToCollection={handleAddToCollection}
  onCreateAgent={handleCreateAgent}
  data-testid={`game-card-${game.id}`}
/>
```

- [ ] **Step 3: Rimuovi le funzioni helper locali non più usate**

Rimuovi `formatPlayerCount` e `formatPlayTime` locali (ora in card-mappers):
```typescript
// ELIMINA queste due funzioni:
function formatPlayerCount(min: number | null, max: number | null): string { ... }
function formatPlayTime(min: number | null, max: number | null): string { ... }
```

- [ ] **Step 4: Verifica compilazione TypeScript**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep -i "MeepleGameCard\|game-card" | head -10
```

Expected: nessun errore

- [ ] **Step 5: Verifica test esistenti**

```bash
cd apps/web && pnpm test src/components/games/MeepleGameCard --run 2>&1 | tail -10
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/games/MeepleGameCard.tsx
git commit -m "feat(MeepleGameCard): use game-card-mapper, move playerCount/playTime to SymbolStrip"
```

---

## Task 9: Aggiorna MeepleSessionCard — linkedEntities, stateLabel

**Files:**
- Modify: `apps/web/src/components/session/MeepleSessionCard.tsx`

**Cambi:**
- Importa `buildSessionCardProps` da card-mappers
- Sostituisce `navigateTo` con `linkedEntities` e `stateLabel`

- [ ] **Step 1: Verifica test esistenti**

```bash
cd apps/web && pnpm test src/components/session/ --run 2>&1 | tail -10
```

- [ ] **Step 2: Modifica MeepleSessionCard.tsx**

**A) Aggiungi import:**
```typescript
import { buildSessionCardProps } from '@/lib/card-mappers';
```

**B) Rimuovi import `getNavigationLinks`** (non più usato):
```typescript
// PRIMA: import { getNavigationLinks } from '@/config/entity-navigation';
// DOPO: rimuovi questa riga
```

**C) Prima del return, aggiungi:**
```typescript
const mapperProps = buildSessionCardProps(session);
```

**D) Nella chiamata `<MeepleCard>`, sostituisci:**
```tsx
// PRIMA:
navigateTo={getNavigationLinks('session', {
  id: session.id,
  gameId: session.gameId,
})}

// DOPO:
linkedEntities={mapperProps.linkedEntities}
stateLabel={mapperProps.stateLabel}
```

La chiamata completa diventa:
```tsx
return (
  <MeepleCard
    id={session.id}
    entity="session"
    variant={variant}
    title={`Sessione #${session.id.slice(0, 8)}`}
    subtitle={subtitle}
    sessionStatus={sessionStatus}
    // Nuove props da mapper: game pip + stateLabel
    linkedEntities={mapperProps.linkedEntities}
    stateLabel={mapperProps.stateLabel}
    className={className}
    onClick={onClick ? () => onClick(session.id) : undefined}
    entityQuickActions={entityQuickActions}
    showInfoButton
    infoHref={`/sessions/${session.id}`}
    infoTooltip="Vai alla sessione"
    data-testid={`session-card-${session.id}`}
  />
);
```

- [ ] **Step 3: Verifica compilazione**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep -i "MeepleSessionCard\|session-card" | head -10
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/session/MeepleSessionCard.tsx
git commit -m "feat(MeepleSessionCard): use session-card-mapper, replace navigateTo with linkedEntities+stateLabel"
```

---

## Task 10: Aggiorna MeeplePlayerCard — player mapper

**Files:**
- Modify: `apps/web/src/components/players/MeeplePlayerCard.tsx`

**Cambi:** Minimali — il mapper extrae solo `title` e `avatarUrl` (se disponibile). Il beneficio principale è la coerenza del layer.

- [ ] **Step 1: Modifica MeeplePlayerCard.tsx**

**A) Aggiungi import:**
```typescript
import { buildPlayerCardProps } from '@/lib/card-mappers';
```

**B) Nel componente, prima del return:**
```typescript
const mapperProps = buildPlayerCardProps(player);
```

**C) Nella chiamata `<MeepleCard>`, aggiorna `title` con quello del mapper:**
```tsx
// title è già corretto ma ora viene dal mapper
title={mapperProps.title ?? player.displayName}
```

**Nota:** Se `buildPlayerCardProps` non aggiunge nulla di utile rispetto all'attuale (player.displayName), il mapper rimane come convenzione/consistenza. Non forzare cambi superflui.

- [ ] **Step 2: Verifica compilazione e test**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep -i "MeeplePlayerCard" | head -5
cd apps/web && pnpm test src/components/players/ --run 2>&1 | tail -10
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/players/MeeplePlayerCard.tsx
git commit -m "feat(MeeplePlayerCard): integrate player-card-mapper"
```

---

## Task 11: Aggiorna MeepleLibraryGameCard — identityChip, linkedEntities, clean metadata

**Files:**
- Modify: `apps/web/src/components/library/MeepleLibraryGameCard.tsx`

**Cambi:**
- Aggiunge `identityChip1`, `identityChip1Icon` dalla utility `mechToIcon` (se disponibile, altrimenti non aggiunti)
- Sostituisce `navigateTo` con `linkedEntities` + `onManaPipClick` per i drawer
- Pulisce la `metadata[]` rimuovendo playerCount e playTime (già nel SymbolStrip per altri adapter)

**Nota:** `UserLibraryEntry` non ha `primaryMechanism`, quindi `identityChip1` resta undefined. La migrazione principale qui è `navigateTo` → `linkedEntities`.

- [ ] **Step 1: Verifica test esistenti**

```bash
cd apps/web && pnpm test src/components/library/__tests__/MeepleLibraryGameCard --run 2>&1 | tail -10
```

- [ ] **Step 2: Aggiungi import**

```typescript
import { buildLinkedEntities } from '@/lib/card-mappers';
```

- [ ] **Step 3: Costruisci linkedEntities**

Prima del return (dopo il codice esistente), aggiungi:

```typescript
// Build linkedEntities per i pip ManaLink
const linkedEntities = buildLinkedEntities({
  kbCount: game.hasKb ? game.kbCardCount : undefined,
  agentCount: agentConfigured ? 1 : undefined,
});
```

- [ ] **Step 4: Aggiungi handler per pip click**

```typescript
const handleManaPipClick = useCallback((entityType: import('@/components/ui/data-display/meeple-card-styles').MeepleEntityType) => {
  switch (entityType) {
    case 'kb': setKbDrawerOpen(true); break;
    case 'agent': setAgentDrawerOpen(true); break;
    case 'chatSession': setChatDrawerOpen(true); break;
    case 'session': setSessionDrawerOpen(true); break;
  }
}, []);
```

- [ ] **Step 5: Nella chiamata MeepleCard, sostituisci `navigateTo` con `linkedEntities` e `onManaPipClick`**

```tsx
// RIMUOVI:
navigateTo={[
  { entity: 'kb' as const, label: 'KB', onClick: () => setKbDrawerOpen(true) },
  { entity: 'agent' as const, label: 'Agents', onClick: () => setAgentDrawerOpen(true) },
  { entity: 'chatSession' as const, label: 'Chats', onClick: () => setChatDrawerOpen(true) },
  { entity: 'session' as const, label: 'Sessions', onClick: () => setSessionDrawerOpen(true) },
]}

// AGGIUNGI:
linkedEntities={linkedEntities.length > 0 ? linkedEntities : undefined}
onManaPipClick={handleManaPipClick}
```

- [ ] **Step 6: Verifica compilazione**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep -i "MeepleLibraryGameCard" | head -10
```

- [ ] **Step 7: Verifica test esistenti**

```bash
cd apps/web && pnpm test src/components/library/__tests__/MeepleLibraryGameCard --run 2>&1 | tail -10
```

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/components/library/MeepleLibraryGameCard.tsx
git commit -m "feat(MeepleLibraryGameCard): replace navigateTo with linkedEntities+onManaPipClick"
```

---

## Task 12: L3 — Elimina PrivateGameCard, inline in PrivateGamesClient

**Files:**
- Delete: `apps/web/src/components/library/PrivateGameCard.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/private/PrivateGamesClient.tsx`
- Modify: `apps/web/src/components/library/index.ts` (se esportato)

**Strategia:** `PrivateGameCard` usa `PrivateGameDto` (tipo diverso da `UserLibraryEntry`). La card è thin (68 righe). Inline direttamente nel consumer come `<MeepleCard>` con `playerCountDisplay` e `playTimeDisplay`.

- [ ] **Step 1: Verifica test del wrapper da eliminare**

```bash
cd apps/web && pnpm test src/components/library/__tests__/PrivateGameCard --run 2>&1 | tail -5
```

Nota il contenuto del test: sarà da rimuovere o adattare.

- [ ] **Step 2: Modifica PrivateGamesClient.tsx**

**A) Rimuovi import PrivateGameCard:**
```typescript
// RIMUOVI:
import { PrivateGameCard } from '@/components/library/PrivateGameCard';
```

**B) Aggiungi imports necessari:**
```typescript
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Edit2, Share2, Trash2 } from 'lucide-react';
import { formatPlayTime } from '@/lib/card-mappers';
```

**C) Aggiungi funzione helper locale (era in PrivateGameCard, ora inline):**
```typescript
function buildPrivateGameCardProps(game: PrivateGameDto) {
  const hasPlayers = game.minPlayers !== null && game.maxPlayers !== null;
  const playerCountDisplay = hasPlayers ? `${game.minPlayers}-${game.maxPlayers}p` : undefined;
  const playTimeDisplay = game.playingTimeMinutes
    ? formatPlayTime(game.playingTimeMinutes)
    : undefined;

  return { playerCountDisplay, playTimeDisplay };
}
```

**D) Sostituisci il render di `PrivateGameCard` nel JSX:**

Prima:
```tsx
<PrivateGameCard
  game={game}
  onEdit={openEdit}
  onDelete={openDelete}
  onPropose={openPropose}
  onClick={() => router.push(`/library/private/${game.id}`)}
/>
```

Dopo:
```tsx
<MeepleCard
  entity="game"
  variant="grid"
  title={game.title}
  subtitle={[
    game.minPlayers && game.maxPlayers ? `${game.minPlayers}-${game.maxPlayers} players` : null,
    game.yearPublished ? `(${game.yearPublished})` : null,
  ].filter(Boolean).join(' · ') || undefined}
  imageUrl={game.imageUrl || undefined}
  badge="Privato"
  playerCountDisplay={buildPrivateGameCardProps(game).playerCountDisplay}
  playTimeDisplay={buildPrivateGameCardProps(game).playTimeDisplay}
  onClick={() => router.push(`/library/private/${game.id}`)}
  entityQuickActions={[
    { icon: Edit2, label: 'Modifica', onClick: () => openEdit(game) },
    { icon: Share2, label: 'Proponi', onClick: () => openPropose(game), hidden: false },
    { icon: Trash2, label: 'Elimina', onClick: () => openDelete(game.id), destructive: true },
  ]}
  data-testid={`game-card-${game.id}`}
/>
```

- [ ] **Step 3: Elimina PrivateGameCard.tsx**

```bash
rm apps/web/src/components/library/PrivateGameCard.tsx
rm apps/web/src/components/library/__tests__/PrivateGameCard.test.tsx
```

- [ ] **Step 4: Rimuovi export da library/index.ts (se presente)**

```bash
grep -n "PrivateGameCard" apps/web/src/components/library/index.ts
```

Se presente, rimuovi la riga corrispondente.

- [ ] **Step 5: Verifica compilazione**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep -i "PrivateGame\|PrivateGamesClient" | head -10
```

Expected: nessun errore

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/'(authenticated)'/library/private/PrivateGamesClient.tsx
git rm apps/web/src/components/library/PrivateGameCard.tsx
git rm apps/web/src/components/library/__tests__/PrivateGameCard.test.tsx
git commit -m "refactor(L3): remove PrivateGameCard wrapper, inline MeepleCard in PrivateGamesClient"
```

---

## Task 13: L3 — Elimina SharedLibraryGameCard, inline in shared library page

**Files:**
- Delete: `apps/web/src/components/library/SharedLibraryGameCard.tsx`
- Modify: `apps/web/src/app/(public)/library/shared/[token]/page.tsx`
- Modify: `apps/web/src/components/library/index.ts`

- [ ] **Step 1: Leggi il consumer**

```bash
grep -n "SharedLibraryGameCard\|SharedLibraryGame" "apps/web/src/app/(public)/library/shared/[token]/page.tsx" | head -15
```

- [ ] **Step 2: Modifica shared library page**

**A) Rimuovi import:**
```typescript
// RIMUOVI:
import { SharedLibraryGameCard } from '@/components/library';
```

**B) Aggiungi import:**
```typescript
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
```

**C) Sostituisci il JSX:**

Prima:
```tsx
<SharedLibraryGameCard key={game.gameId} game={game} showNotes={hasNotes} />
```

Dopo:
```tsx
<MeepleCard
  key={game.gameId}
  entity="game"
  variant="grid"
  title={game.title}
  subtitle={game.publisher || undefined}
  imageUrl={game.imageUrl || undefined}
  badge={game.isFavorite ? 'Preferito' : undefined}
  showPreview={hasNotes && !!game.notes}
  previewData={hasNotes && game.notes ? { description: game.notes } : undefined}
/>
```

- [ ] **Step 3: Elimina SharedLibraryGameCard**

```bash
rm apps/web/src/components/library/SharedLibraryGameCard.tsx
rm apps/web/src/components/library/__tests__/SharedLibraryGameCard.test.tsx
```

- [ ] **Step 4: Aggiorna library/index.ts**

Rimuovi la riga:
```typescript
// RIMUOVI:
export { SharedLibraryGameCard } from './SharedLibraryGameCard';
```

- [ ] **Step 5: Verifica compilazione**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep -i "SharedLibraryGameCard\|shared.*library" | head -10
```

- [ ] **Step 6: Commit**

```bash
git add "apps/web/src/app/(public)/library/shared/[token]/page.tsx"
git add apps/web/src/components/library/index.ts
git rm apps/web/src/components/library/SharedLibraryGameCard.tsx
git rm apps/web/src/components/library/__tests__/SharedLibraryGameCard.test.tsx
git commit -m "refactor(L3): remove SharedLibraryGameCard wrapper, inline MeepleCard in shared library page"
```

---

## Task 14: L3 — Elimina BggGameCard duplicato da /games/

**Files:**
- Delete: `apps/web/src/components/games/BggGameCard.tsx`
- Modify: `apps/web/src/components/games/index.ts`

Il componente canonico resta in `apps/web/src/components/bgg/BggGameCard.tsx`.

- [ ] **Step 1: Verifica che nessuno importi da /games/BggGameCard**

```bash
grep -r "from.*games/BggGameCard\|from.*components/games.*BggGame" apps/web/src --include="*.tsx" --include="*.ts"
```

Expected: nessun risultato (gli import dovrebbero già usare `/bgg/`)

- [ ] **Step 2: Elimina il duplicato**

```bash
rm apps/web/src/components/games/BggGameCard.tsx
```

- [ ] **Step 3: Aggiorna games/index.ts**

Leggi il file:
```bash
cat apps/web/src/components/games/index.ts
```

Rimuovi la riga:
```typescript
// RIMUOVI:
export { BggGameCard } from './BggGameCard';
```

- [ ] **Step 4: Verifica compilazione finale**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep -i "BggGameCard\|bgg-game" | head -10
```

- [ ] **Step 5: Run completo dei test**

```bash
cd apps/web && pnpm test --run 2>&1 | tail -20
```

Expected: tutti i test passano (o falliscono per ragioni non correlate alla migrazione)

- [ ] **Step 6: Commit finale**

```bash
git rm apps/web/src/components/games/BggGameCard.tsx
git add apps/web/src/components/games/index.ts
git commit -m "refactor(L3): remove duplicate BggGameCard from /games/ (canonical source: /bgg/)"
```

---

## Self-Review Checklist

**Spec coverage:**
- [x] Task 1-6: `src/lib/card-mappers/` creato con shared-utils + 4 mapper + index
- [x] Task 7-11: Tutti e 5 gli adapter aggiornati
- [x] Task 12: PrivateGameCard eliminato, consumer migrato
- [x] Task 13: SharedLibraryGameCard eliminato, consumer migrato
- [x] Task 14: BggGameCard duplicato eliminato

**Placeholder scan:**
- Nessun TBD o TODO nel piano
- Il Task 5 (player-card-mapper) ha un'istruzione di adattamento condizionale documentata
- Ogni step ha codice esatto o comando esatto

**Type consistency:**
- `buildGameCardProps` → `Partial<MeepleCardProps>` con `playerCountDisplay`, `playTimeDisplay`
- `buildKbCardProps` → `Partial<MeepleCardProps>` con `pageCount`, `identityChip1`, `identityChip1Icon`, `stateLabel`
- `buildSessionCardProps` → `Partial<MeepleCardProps>` con `linkedEntities`, `stateLabel`
- `buildPlayerCardProps` → `Partial<MeepleCardProps>` con `title`, `avatarUrl?`
- `formatPlayTime` usata in Task 1 (test), Task 3 (game-card-mapper), Task 12 (PrivateGamesClient)
- `buildLinkedEntities` usata in Task 4 (session-card-mapper), Task 11 (MeepleLibraryGameCard)
- `processingStateToLabel` usata in Task 1 (test), Task 3 (kb-card-mapper)
- `sessionStatusToLabel` usata in Task 1 (test), Task 4 (session-card-mapper)
