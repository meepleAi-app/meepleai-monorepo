/**
 * Smoke test: verifies MeepleCard mounts without throwing for each of the 9
 * entity types representative of the 12 consumer surface categories.
 *
 * This is NOT a visual diff test (per #1856 DEC-6 — Visual Gate REMOVED
 * 2026-05-20). It's a render-smoke gate that catches structural breakage
 * (e.g. missing prop wiring, runtime errors in new parts).
 */
import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';

import { MeepleCard } from '../MeepleCard';

import type { MeepleEntityType } from '../types';

const surfaceCategories: Array<{
  name: string;
  entity: MeepleEntityType;
  withImage: boolean;
  withCoverEmoji: boolean;
  withStatus: boolean;
}> = [
  {
    name: 'library / games',
    entity: 'game',
    withImage: false,
    withCoverEmoji: false,
    withStatus: true,
  },
  { name: 'games hub', entity: 'game', withImage: true, withCoverEmoji: false, withStatus: false },
  { name: 'sessions', entity: 'session', withImage: false, withCoverEmoji: true, withStatus: true },
  { name: 'players', entity: 'player', withImage: false, withCoverEmoji: false, withStatus: false },
  { name: 'agents', entity: 'agent', withImage: false, withCoverEmoji: false, withStatus: true },
  { name: 'kb', entity: 'kb', withImage: false, withCoverEmoji: false, withStatus: true },
  { name: 'chat', entity: 'chat', withImage: false, withCoverEmoji: false, withStatus: false },
  {
    name: 'events / game-night',
    entity: 'event',
    withImage: false,
    withCoverEmoji: true,
    withStatus: false,
  },
  {
    name: 'toolkits',
    entity: 'toolkit',
    withImage: false,
    withCoverEmoji: true,
    withStatus: false,
  },
  { name: 'tools', entity: 'tool', withImage: false, withCoverEmoji: false, withStatus: false },
];

describe('MeepleCard consumer-category smoke (#1856)', () => {
  for (const cat of surfaceCategories) {
    it(`mounts for ${cat.name} surface (entity=${cat.entity})`, () => {
      expect(() =>
        render(
          <MeepleCard
            entity={cat.entity}
            title={`Smoke ${cat.name}`}
            imageUrl={cat.withImage ? 'https://cdn.example.com/x.webp' : undefined}
            coverEmoji={cat.withCoverEmoji ? '🎯' : undefined}
            status={cat.withStatus ? 'owned' : undefined}
            badge={cat.withStatus ? 'OWNED' : undefined}
          />
        )
      ).not.toThrow();
    });
  }
});
