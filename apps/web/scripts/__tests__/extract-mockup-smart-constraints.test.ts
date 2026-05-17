/**
 * Tests for WS-E.1 SMART constraint extractor (refs #1071, umbrella #1066).
 */
import { describe, it, expect } from 'vitest';
import {
  extractSmartConstraints,
  classifyMetricType,
  type ConstraintRecord,
} from '../extract-mockup-smart-constraints';

function commentBlock(body: string): string {
  return `<!doctype html><html><head><!--\n  Persona: x\n  ${body}\n  Sorgente: y\n--></head></html>`;
}

describe('extractSmartConstraints', () => {
  it('parses simple bullet list (chat-tab style)', () => {
    const html = commentBlock(`Vincoli SMART (da spec G1+G5):
    - 90% query con citazione esplicita (pagina+sezione)
    - confidence < 70% → marker incertezza obbligatorio
    - latency P95 ≤ 10s con spinner accettabile
    - chat handoff <3s (G2 — non coperto qui ma compatibile con UX)`);

    const constraints = extractSmartConstraints(html, 'chat-tab.html');

    expect(constraints).toHaveLength(4);
    expect(constraints[0]).toMatchObject({
      mockup: 'chat-tab.html',
      id: null,
      text: expect.stringContaining('90% query'),
      metric_type: 'coverage',
    });
    expect(constraints[1].metric_type).toBe('confidence');
    expect(constraints[2].metric_type).toBe('latency');
  });

  it('parses ID-prefixed list (pdf-viewer style)', () => {
    const html = commentBlock(`Vincoli SMART (da spec G4 v3):
    - G4.2: tab "PDF originale" sempre visibile (anche non-owner)
    - G4.3: upsell render <100ms (no fetch)
    - G4.4: PDF caricato + seek to pageNumber < 3s su 4G
    - G4.5: niente download button visibile, no link diretto a /download endpoint`);

    const constraints = extractSmartConstraints(html, 'pdf-viewer.html');

    expect(constraints).toHaveLength(4);
    expect(constraints[0].id).toBe('G4.2');
    expect(constraints[0].text).toContain('tab "PDF originale" sempre visibile');
    expect(constraints[1].id).toBe('G4.3');
    expect(constraints[1].metric_type).toBe('performance');
    expect(constraints[2].id).toBe('G4.4');
    expect(constraints[2].metric_type).toBe('latency');
    expect(constraints[3].id).toBe('G4.5');
  });

  it('returns empty array when no SMART block exists', () => {
    const html = '<html><body>nothing here</body></html>';
    expect(extractSmartConstraints(html, 'x.html')).toEqual([]);
  });

  it('returns empty array when comment exists but no SMART block', () => {
    const html = commentBlock('Mockup: nothing interesting');
    expect(extractSmartConstraints(html, 'x.html')).toEqual([]);
  });

  it('stops at first non-bullet line after SMART header', () => {
    const html = commentBlock(`Vincoli SMART:
    - first constraint with citazione
    - second constraint with latency
  Sorgente: ignored`);
    const constraints = extractSmartConstraints(html, 'x.html');
    expect(constraints).toHaveLength(2);
  });

  it('extracts target_value when numeric quantity present', () => {
    const html = commentBlock(`Vincoli SMART:
    - latency P95 ≤ 10s con spinner accettabile
    - upsell render <100ms (no fetch)
    - 90% query con citazione esplicita`);
    const constraints = extractSmartConstraints(html, 'x.html');
    expect(constraints[0].target_value).toEqual({ comparator: '≤', value: 10, unit: 's' });
    expect(constraints[1].target_value).toEqual({ comparator: '<', value: 100, unit: 'ms' });
    expect(constraints[2].target_value).toEqual({ comparator: '≥', value: 90, unit: '%' });
  });

  it('leaves target_value null for qualitative constraints', () => {
    const html = commentBlock(`Vincoli SMART:
    - niente download button visibile, no link diretto a /download endpoint`);
    const constraints = extractSmartConstraints(html, 'x.html');
    expect(constraints[0].target_value).toBeNull();
    expect(constraints[0].metric_type).toBe('ux-constraint');
  });

  it('records applies_to as the mockup filename', () => {
    const html = commentBlock(`Vincoli SMART:
    - some text`);
    const constraints = extractSmartConstraints(html, 'foo.html');
    expect(constraints[0].applies_to).toEqual(['foo.html']);
  });
});

describe('classifyMetricType', () => {
  it.each([
    ['90% query con citazione esplicita', 'coverage'],
    ['confidence < 70% marker', 'confidence'],
    ['latency P95 ≤ 10s', 'latency'],
    ['render <100ms', 'performance'],
    ['WCAG 2.1 AA contrast', 'accessibility'],
    ['a11y compliance', 'accessibility'],
    ['niente download button visibile', 'ux-constraint'],
    ['tab sempre visibile', 'ux-constraint'],
  ])('classifies "%s" as %s', (text, expected) => {
    expect(classifyMetricType(text)).toBe(expected);
  });

  it('falls back to "other" when no keyword matches', () => {
    expect(classifyMetricType('completely unrelated requirement xyz')).toBe('other');
  });
});

describe('schema shape', () => {
  it('returns ConstraintRecord with all canonical fields', () => {
    const html = commentBlock(`Vincoli SMART:
    - G1.1: 90% query con citazione esplicita`);
    const c: ConstraintRecord = extractSmartConstraints(html, 'x.html')[0];
    expect(Object.keys(c).sort()).toEqual(
      ['applies_to', 'id', 'metric_type', 'mockup', 'target_value', 'text'].sort()
    );
  });
});
