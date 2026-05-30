/**
 * parseCitationMarkers — 14 test scenarios per spec-panel D-1703-D (2026-05-30).
 *
 * Strict digit-only regex /\[(\d+(?:,\s*\d+)*)\]/g.
 * Coexists with [ref:docId:page] copyright markers (has colon → excluded)
 * and [text](url) markdown links (has paren → excluded).
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render } from '@testing-library/react';
import { isValidElement, type ReactNode } from 'react';

import type { KbCitation } from '@/lib/api/schemas/kb-ask.schemas';

import { parseCitationMarkers } from '../parseCitationMarkers';

const cite = (docId: string, page: number, snippet = 'snippet'): KbCitation => ({
  docId,
  source: docId,
  page,
  snippet,
  score: 0.9,
});

const formatAriaLabel = (c: KbCitation, n: number): string =>
  `Citazione ${n}, documento ${c.docId}, pagina ${c.page}`;

function renderNodes(nodes: ReactNode[]): HTMLElement {
  // Wrap returned nodes in a fragment-like container so RTL can render them.
  const { container } = render(<>{nodes}</>);
  return container;
}

function countPills(container: HTMLElement): number {
  return container.querySelectorAll('[data-slot="kb-globale-citation-pill"]').length;
}

describe('parseCitationMarkers (D-1703-D)', () => {
  let warnSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
  });

  afterEach(() => {
    warnSpy.mockRestore();
  });

  // ── Scenario 1: happy path ─────────────────────────────────────────────
  it('1. renders inline pills for "Vedi [1] e [2]" with 2 citations', () => {
    const citations = [cite('doc-1', 14), cite('doc-2', 21)];
    const nodes = parseCitationMarkers('Vedi [1] e [2]', citations, { formatAriaLabel });

    const container = renderNodes(nodes);
    expect(countPills(container)).toBe(2);
    expect(container.textContent).toContain('Vedi ');
    expect(container.textContent).toContain(' e ');
    expect(warnSpy).not.toHaveBeenCalled();
  });

  // ── Scenario 2: [0] is invalid (1-based) ───────────────────────────────
  it('2. [0] is rendered as literal text + warns (1-based invariant)', () => {
    const nodes = parseCitationMarkers('See [0] here', [cite('d', 1)], { formatAriaLabel });

    const container = renderNodes(nodes);
    expect(countPills(container)).toBe(0);
    expect(container.textContent).toBe('See [0] here');
    expect(warnSpy).toHaveBeenCalledTimes(1);
  });

  // ── Scenario 3: N > citations.length is invalid ────────────────────────
  it('3. [N] where N exceeds citations.length renders as literal + warns', () => {
    const nodes = parseCitationMarkers('See [5] here', [cite('d', 1)], { formatAriaLabel });

    const container = renderNodes(nodes);
    expect(countPills(container)).toBe(0);
    expect(container.textContent).toBe('See [5] here');
    expect(warnSpy).toHaveBeenCalledTimes(1);
  });

  // ── Scenario 4: [N,M] both valid renders 2 pills back-to-back ─────────
  it('4. [N,M] valid mixed renders 2 pills back-to-back', () => {
    const citations = [cite('doc-1', 14), cite('doc-2', 21)];
    const nodes = parseCitationMarkers('Sources [1,2] confirm', citations, { formatAriaLabel });

    const container = renderNodes(nodes);
    expect(countPills(container)).toBe(2);
    expect(container.textContent).toContain('Sources ');
    expect(container.textContent).toContain(' confirm');
    // Pills should appear adjacent (no separator between them)
    const pills = container.querySelectorAll('[data-slot="kb-globale-citation-pill"]');
    expect(pills[0].nextSibling).toBe(pills[1]);
  });

  // ── Scenario 5: [1,99,2] mixed valid/invalid — render valid only ──────
  it('5. [1,99,2] mixed valid/invalid skips invalid, renders valid', () => {
    const citations = [cite('doc-1', 14), cite('doc-2', 21)];
    const nodes = parseCitationMarkers('See [1,99,2]', citations, { formatAriaLabel });

    const container = renderNodes(nodes);
    expect(countPills(container)).toBe(2); // pills for 1 and 2; 99 skipped
    expect(warnSpy).toHaveBeenCalledTimes(1); // warn fires for [99]
  });

  // ── Scenario 6: [ref:docId:page] copyright marker is preserved ────────
  it('6. [ref:abc:14] copyright marker is rendered as literal text (excluded by regex)', () => {
    const citations = [cite('doc-1', 14)];
    const nodes = parseCitationMarkers('Per [ref:abc:14], vedi [1].', citations, {
      formatAriaLabel,
    });

    const container = renderNodes(nodes);
    expect(container.textContent).toContain('[ref:abc:14]');
    expect(countPills(container)).toBe(1); // only [1] becomes a pill
    expect(warnSpy).not.toHaveBeenCalled();
  });

  // ── Scenario 7: [text](url) markdown link preserved ───────────────────
  it('7. [link](url) markdown link is rendered as literal text', () => {
    const citations = [cite('doc-1', 14)];
    const nodes = parseCitationMarkers(
      'Vedi [pagina ufficiale](https://example.com) e [1].',
      citations,
      { formatAriaLabel }
    );

    const container = renderNodes(nodes);
    expect(container.textContent).toContain('[pagina ufficiale](https://example.com)');
    expect(countPills(container)).toBe(1);
    expect(warnSpy).not.toHaveBeenCalled();
  });

  // ── Scenario 8: empty input ───────────────────────────────────────────
  it('8. empty input "" returns a single empty text node array', () => {
    const nodes = parseCitationMarkers('', [], { formatAriaLabel });

    expect(nodes).toHaveLength(1);
    expect(nodes[0]).toBe('');
  });

  // ── Scenario 9: no markers in text ────────────────────────────────────
  it('9. text without any markers returns the original text as a single node', () => {
    const text = 'La classe Scout inizia con tre abilità signature.';
    const citations = [cite('doc-1', 14)];
    const nodes = parseCitationMarkers(text, citations, { formatAriaLabel });

    expect(nodes).toHaveLength(1);
    expect(nodes[0]).toBe(text);
  });

  // ── Scenario 10: markers at start and end of text ─────────────────────
  it('10. markers at start and end are rendered correctly', () => {
    const citations = [cite('doc-1', 14)];
    const nodes1 = parseCitationMarkers('[1] inizia il testo', citations, { formatAriaLabel });
    const nodes2 = parseCitationMarkers('il testo finisce [1]', citations, { formatAriaLabel });

    const c1 = renderNodes(nodes1);
    const c2 = renderNodes(nodes2);
    expect(countPills(c1)).toBe(1);
    expect(c1.textContent?.endsWith(' inizia il testo')).toBe(true);
    expect(countPills(c2)).toBe(1);
    expect(c2.textContent?.startsWith('il testo finisce ')).toBe(true);
  });

  // ── Scenario 11: Italian diacritics preserved with markers ────────────
  it('11. Italian diacritics around markers are preserved (à, è, ù, ò)', () => {
    const text = 'La regolarità delle abilità è confermata [1], però la durabilità no [2].';
    const citations = [cite('doc-1', 14), cite('doc-2', 21)];
    const nodes = parseCitationMarkers(text, citations, { formatAriaLabel });

    const container = renderNodes(nodes);
    expect(container.textContent).toContain('regolarità');
    expect(container.textContent).toContain('abilità');
    expect(container.textContent).toContain('però');
    expect(countPills(container)).toBe(2);
  });

  // ── Scenario 12: adjacent markers [1][2] render 2 pills no separator ──
  it('12. adjacent [1][2] (no space between) renders 2 pills with no text separator', () => {
    const citations = [cite('doc-1', 14), cite('doc-2', 21)];
    const nodes = parseCitationMarkers('Sources [1][2] confirm', citations, { formatAriaLabel });

    const container = renderNodes(nodes);
    expect(countPills(container)).toBe(2);
    const pills = container.querySelectorAll('[data-slot="kb-globale-citation-pill"]');
    expect(pills[0].nextSibling).toBe(pills[1]); // no text between
  });

  // ── Scenario 13: repeated [1][1][1] renders 3 pills + warns ONCE ──────
  it('13. repeated valid [1][1][1] renders 3 pills (no warn — index is valid)', () => {
    const citations = [cite('doc-1', 14)];
    const nodes = parseCitationMarkers('See [1][1][1] here', citations, { formatAriaLabel });

    const container = renderNodes(nodes);
    expect(countPills(container)).toBe(3);
    expect(warnSpy).not.toHaveBeenCalled(); // valid index, no warning
  });

  // ── Scenario 13b: repeated INVALID [99][99][99] warns ONCE (dedup) ────
  it('13b. repeated invalid [99][99][99] warns only ONCE (warn dedup per call)', () => {
    const citations = [cite('doc-1', 14)];
    const nodes = parseCitationMarkers('Bad [99][99][99]', citations, { formatAriaLabel });

    const container = renderNodes(nodes);
    expect(countPills(container)).toBe(0);
    expect(container.textContent).toBe('Bad [99][99][99]');
    expect(warnSpy).toHaveBeenCalledTimes(1); // dedup: warn fires once per invalid index per call
  });

  // ── Scenario 14: malformed inputs do not crash ────────────────────────
  it('14. malformed "[1 unclosed" + unicode "[１]" pass through as literals, no crash', () => {
    const citations = [cite('doc-1', 14)];
    expect(() =>
      parseCitationMarkers('Vedi [1 e [１]', citations, { formatAriaLabel })
    ).not.toThrow();

    const nodes = parseCitationMarkers('Vedi [1 e [１]', citations, { formatAriaLabel });
    const container = renderNodes(nodes);
    expect(countPills(container)).toBe(0); // no closing bracket on "[1"; "[１]" is unicode not ASCII
    expect(container.textContent).toContain('[1');
    expect(container.textContent).toContain('[１]');
    expect(warnSpy).not.toHaveBeenCalled(); // LLM noise — silent skip
  });

  // ── Type contract: returned array nodes are strings or ReactElements ──
  it('returned array consists only of strings (text) and ReactElements (pills)', () => {
    const citations = [cite('doc-1', 14)];
    const nodes = parseCitationMarkers('Hello [1] world', citations, { formatAriaLabel });

    for (const node of nodes) {
      const isText = typeof node === 'string';
      const isElement = isValidElement(node);
      expect(isText || isElement).toBe(true);
    }
  });

  // ── onCitationClick forwarding ─────────────────────────────────────────
  it('forwards onCitationClick to each rendered pill', () => {
    const onClick = vi.fn();
    const citations = [cite('doc-x', 7)];
    const nodes = parseCitationMarkers('Vedi [1]', citations, {
      formatAriaLabel,
      onCitationClick: onClick,
    });
    const container = renderNodes(nodes);
    const pill = container.querySelector('[data-slot="kb-globale-citation-pill"]');
    expect(pill).not.toBeNull();
    (pill as HTMLButtonElement).click();
    expect(onClick).toHaveBeenCalledWith({ docId: 'doc-x', page: 7 });
  });
});
