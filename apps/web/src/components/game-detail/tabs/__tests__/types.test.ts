import { describe, expect, it } from 'vitest';

import { GAME_TABS, isGameTabId } from '../types';

// Newman Strategy 1 regression — #2010 spec-panel 2026-06-08.
// Labels track the sp4-game-detail.jsx mockup; IDs remain stable so existing
// `?tab=aiChat`/`?tab=toolbox` URLs + legacy `agent/page.tsx` + `toolbox/page.tsx`
// redirects keep working. If a rename ever happens, ship a proper deprecation
// strategy (Newman Strategy 2 — redirects + window) — not a silent ID swap.

describe('GAME_TABS', () => {
  it('keeps tab IDs stable (URL contract)', () => {
    expect(GAME_TABS.map(t => t.id)).toEqual([
      'info',
      'aiChat',
      'toolbox',
      'houseRules',
      'partite',
    ]);
  });

  it('renders the sp4-game-detail.jsx mockup labels', () => {
    const labels = Object.fromEntries(GAME_TABS.map(t => [t.id, t.label]));
    expect(labels.info).toBe('Info');
    expect(labels.aiChat).toBe('Agente');
    expect(labels.toolbox).toBe('Toolkit');
    expect(labels.houseRules).toBe('House Rules');
    expect(labels.partite).toBe('Partite');
  });
});

describe('isGameTabId', () => {
  it.each(['info', 'aiChat', 'toolbox', 'houseRules', 'partite'] as const)(
    'accepts canonical ID %s',
    id => {
      expect(isGameTabId(id)).toBe(true);
    }
  );

  // Display labels are NOT valid IDs — guards against accidental rename.
  it.each(['agente', 'toolkit', 'Agente', 'Toolkit'])(
    'rejects mockup-label string %s as tab ID',
    label => {
      expect(isGameTabId(label)).toBe(false);
    }
  );

  it.each([null, undefined, '', 'unknown'])('rejects %s', value => {
    expect(isGameTabId(value)).toBe(false);
  });
});
