/**
 * Mana System Integration Tests
 * Verifies all 16 entity types work with the mana components.
 */
import React from 'react';

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ManaSymbol } from '../mana/ManaSymbol';
import { MANA_DISPLAY, ENTITY_RELATIONSHIPS, getManaDisplayName } from '../mana/mana-config';
import { ManaBadge } from '../meeple-card-features/ManaBadge';
import { ManaLinkFooter } from '../meeple-card-features/ManaLinkFooter';
import { StatusGlow } from '../meeple-card-features/StatusGlow';
import { PrimaryActions } from '../meeple-card-features/PrimaryActions';
import { entityColors, type MeepleEntityType } from '../meeple-card-styles';

const ALL_ENTITY_TYPES: MeepleEntityType[] = [
  'game',
  'session',
  'player',
  'event',
  'collection',
  'group',
  'location',
  'expansion',
  'agent',
  'kb',
  'chatSession',
  'note',
  'toolkit',
  'tool',
  'achievement',
  'custom',
];

describe('Mana System Integration', () => {
  describe('ManaSymbol renders for all 16 entity types', () => {
    ALL_ENTITY_TYPES.forEach(entity => {
      it(`renders ${entity} mana symbol`, () => {
        render(<ManaSymbol entity={entity} size="medium" />);
        expect(screen.getByTestId(`mana-symbol-${entity}`)).toBeInTheDocument();
      });
    });
  });

  describe('ManaBadge renders correct display names', () => {
    const DISPLAY_NAME_MAP: Partial<Record<MeepleEntityType, string>> = {
      kb: 'Knowledge',
      chatSession: 'Chat',
      game: 'Game',
      collection: 'Collection',
      achievement: 'Achievement',
    };

    Object.entries(DISPLAY_NAME_MAP).forEach(([entity, name]) => {
      it(`${entity} badge shows "${name}"`, () => {
        render(<ManaBadge entity={entity as MeepleEntityType} />);
        expect(screen.getByText(name)).toBeInTheDocument();
      });
    });
  });

  describe('Entity color system completeness', () => {
    it('entityColors has entries for all 16 types', () => {
      ALL_ENTITY_TYPES.forEach(type => {
        expect(entityColors[type]).toBeDefined();
        expect(entityColors[type].hsl).toBeTruthy();
      });
    });

    it('MANA_DISPLAY has config for all 16 types', () => {
      ALL_ENTITY_TYPES.forEach(type => {
        expect(MANA_DISPLAY[type]).toBeDefined();
        expect(MANA_DISPLAY[type].symbol).toBeTruthy();
        expect(MANA_DISPLAY[type].displayName).toBeTruthy();
      });
    });

    it('ENTITY_RELATIONSHIPS has entries for all 16 types', () => {
      ALL_ENTITY_TYPES.forEach(type => {
        expect(ENTITY_RELATIONSHIPS[type]).toBeDefined();
        expect(Array.isArray(ENTITY_RELATIONSHIPS[type])).toBe(true);
      });
    });
  });

  describe('ManaLinkFooter renders linked entities', () => {
    it('renders mana pips for game linked entities', () => {
      const gameLinks = ENTITY_RELATIONSHIPS.game;
      const linkedEntities = gameLinks.slice(0, 3).map(et => ({ entityType: et, count: 1 }));

      render(<ManaLinkFooter linkedEntities={linkedEntities} onPipClick={vi.fn()} />);

      linkedEntities.forEach(({ entityType }) => {
        expect(screen.getByTestId(`mana-pip-${entityType}`)).toBeInTheDocument();
      });
    });
  });

  describe('StatusGlow works with all entity colors', () => {
    it('renders glow for each entity type without errors', () => {
      ALL_ENTITY_TYPES.forEach(entity => {
        const { unmount } = render(
          <StatusGlow state="active" entityColor={entityColors[entity].hsl} />
        );
        unmount();
      });
    });
  });

  describe('Cross-component consistency', () => {
    it('getManaDisplayName matches MANA_DISPLAY', () => {
      ALL_ENTITY_TYPES.forEach(type => {
        expect(getManaDisplayName(type)).toBe(MANA_DISPLAY[type].displayName);
      });
    });

    it('all relationship targets are valid entity types', () => {
      ALL_ENTITY_TYPES.forEach(type => {
        ENTITY_RELATIONSHIPS[type].forEach(target => {
          expect(ALL_ENTITY_TYPES).toContain(target);
        });
      });
    });
  });
});
