/**
 * AdvancedFiltersDrawer — DRAWER_SECTIONS configuration tests.
 *
 * SP4 mockup conformance (Issue #1585-followup, plan Task 3.3). The drawer
 * uses a static 7-section descriptor (no scope dispatch). Tests assert the
 * shape, ordering, kind variety, and default-open behaviour matches the
 * mockup (admin-mockups/design_files/sp4-library-desktop.jsx:319-388).
 */

import { describe, expect, it } from 'vitest';

import { DRAWER_SECTIONS, isDefaultOpen } from '../sections';

describe('DRAWER_SECTIONS', () => {
  it('exposes 7 sections matching the mockup order', () => {
    expect(DRAWER_SECTIONS.map(s => s.key)).toEqual([
      'statuses',
      'entities',
      'games',
      'period',
      'tags',
      'rating',
      'weights',
    ]);
  });

  it('uses chips-multi for status / entity / tags / weights sections', () => {
    const chipsMultiKeys = DRAWER_SECTIONS.filter(s => s.kind === 'chips-multi').map(s => s.key);
    expect(chipsMultiKeys).toEqual(['statuses', 'entities', 'tags', 'weights']);
  });

  it('uses select-multi for games section', () => {
    const section = DRAWER_SECTIONS.find(s => s.key === 'games');
    expect(section?.kind).toBe('select-multi');
  });

  it('uses period-quick for period section', () => {
    const section = DRAWER_SECTIONS.find(s => s.key === 'period');
    expect(section?.kind).toBe('period-quick');
  });

  it('uses range for rating section with [1..10] step 0.5 default [6, 10]', () => {
    const section = DRAWER_SECTIONS.find(s => s.key === 'rating');
    expect(section?.kind).toBe('range');
    if (section && section.kind === 'range') {
      expect(section.min).toBe(1);
      expect(section.max).toBe(10);
      expect(section.step).toBe(0.5);
      expect(section.defaultLo).toBe(6);
      expect(section.defaultHi).toBe(10);
      expect(section.minField).toBe('ratingMin');
      expect(section.maxField).toBe('ratingMax');
    }
  });

  it('exposes 4 status options (owned/wishlist/setup/archived)', () => {
    const section = DRAWER_SECTIONS.find(s => s.key === 'statuses');
    expect(section?.kind).toBe('chips-multi');
    if (section && section.kind === 'chips-multi') {
      expect(section.options.map(o => o.value)).toEqual(['owned', 'wishlist', 'setup', 'archived']);
    }
  });

  it('exposes 5 entity options (game/agent/kb/session/chat)', () => {
    const section = DRAWER_SECTIONS.find(s => s.key === 'entities');
    expect(section?.kind).toBe('chips-multi');
    if (section && section.kind === 'chips-multi') {
      expect(section.options.map(o => o.value)).toEqual(['game', 'agent', 'kb', 'session', 'chat']);
    }
  });

  it('exposes 5 period options (7d/30d/1y/all/range)', () => {
    const section = DRAWER_SECTIONS.find(s => s.key === 'period');
    expect(section?.kind).toBe('period-quick');
    if (section && section.kind === 'period-quick') {
      expect(section.options.map(o => o.value)).toEqual(['7d', '30d', '1y', 'all', 'range']);
    }
  });

  it('exposes 8 tag options (mockup taxonomy)', () => {
    const section = DRAWER_SECTIONS.find(s => s.key === 'tags');
    expect(section?.kind).toBe('chips-multi');
    if (section && section.kind === 'chips-multi') {
      expect(section.options.map(o => o.value)).toEqual([
        'family',
        'strategy',
        'coop',
        'engine',
        'auction',
        'roll-and-write',
        'card-driven',
        'tableau',
      ]);
    }
  });

  it('exposes 4 weight options (light/medium/heavy/extra)', () => {
    const section = DRAWER_SECTIONS.find(s => s.key === 'weights');
    expect(section?.kind).toBe('chips-multi');
    if (section && section.kind === 'chips-multi') {
      expect(section.options.map(o => o.value)).toEqual(['light', 'medium', 'heavy', 'extra']);
    }
  });

  it('assigns entity colors to chip options (mockup palette)', () => {
    const statusSection = DRAWER_SECTIONS.find(s => s.key === 'statuses');
    if (statusSection && statusSection.kind === 'chips-multi') {
      const ownedColor = statusSection.options.find(o => o.value === 'owned')?.color;
      expect(ownedColor).toBe('game');
      const wishlistColor = statusSection.options.find(o => o.value === 'wishlist')?.color;
      expect(wishlistColor).toBe('event');
    }
  });

  it('assigns icons to status / entity chip options (mockup affordance)', () => {
    const statusSection = DRAWER_SECTIONS.find(s => s.key === 'statuses');
    if (statusSection && statusSection.kind === 'chips-multi') {
      expect(statusSection.options.find(o => o.value === 'owned')?.icon).toBe('✓');
      expect(statusSection.options.find(o => o.value === 'wishlist')?.icon).toBe('★');
    }
    const entitySection = DRAWER_SECTIONS.find(s => s.key === 'entities');
    if (entitySection && entitySection.kind === 'chips-multi') {
      expect(entitySection.options.find(o => o.value === 'game')?.icon).toBe('🎲');
      expect(entitySection.options.find(o => o.value === 'agent')?.icon).toBe('🤖');
    }
  });

  it('omits icons on tag chip options (mockup parity)', () => {
    const tagSection = DRAWER_SECTIONS.find(s => s.key === 'tags');
    if (tagSection && tagSection.kind === 'chips-multi') {
      expect(tagSection.options.every(o => o.icon === undefined)).toBe(true);
    }
  });
});

describe('isDefaultOpen', () => {
  it('returns true for the first 3 sections (mockup defaultOpen=true)', () => {
    expect(isDefaultOpen(0)).toBe(true);
    expect(isDefaultOpen(1)).toBe(true);
    expect(isDefaultOpen(2)).toBe(true);
  });

  it('returns false for sections at index >= 3 (mockup collapsed by default)', () => {
    expect(isDefaultOpen(3)).toBe(false);
    expect(isDefaultOpen(4)).toBe(false);
    expect(isDefaultOpen(5)).toBe(false);
    expect(isDefaultOpen(6)).toBe(false);
  });
});
