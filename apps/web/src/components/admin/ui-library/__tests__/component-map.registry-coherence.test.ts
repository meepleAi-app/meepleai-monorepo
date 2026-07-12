import { describe, it, expect } from 'vitest';

import { COMPONENT_REGISTRY } from '@/config/component-registry';

import { COMPONENT_MAP } from '../component-map';

/**
 * Guards coherence between the UI-library showcase's two paired structures:
 * `COMPONENT_REGISTRY` (the catalog metadata) and `COMPONENT_MAP` (id → real
 * React component used by StaticRenderer/ComponentDetail).
 *
 * Every `COMPONENT_MAP` key MUST correspond to a live registry entry id;
 * otherwise the map holds a renderer for an id the catalog no longer lists —
 * dead code that can silently drift (see audit
 * docs/for-developers/audits/2026-07-12-meeplecard-css-drift-audit.md, issue #2853,
 * follow-up of the 49-entry registry cleanup in #2843).
 */
describe('COMPONENT_MAP ↔ COMPONENT_REGISTRY coherence', () => {
  it('every COMPONENT_MAP key maps to an existing registry entry id', () => {
    const registryIds = new Set(COMPONENT_REGISTRY.map(e => e.id));
    const orphanKeys = Object.keys(COMPONENT_MAP).filter(key => !registryIds.has(key));
    expect(orphanKeys).toEqual([]);
  });

  it('detects an orphan key (negative control)', () => {
    const registryIds = new Set(['meeple-card', 'badge']);
    const fakeMap = { 'meeple-card': 0, 'ghost-key': 0 };
    const orphanKeys = Object.keys(fakeMap).filter(key => !registryIds.has(key));
    expect(orphanKeys).toEqual(['ghost-key']);
  });
});
