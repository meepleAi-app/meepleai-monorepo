#!/usr/bin/env tsx
/**
 * WS-E.1 — SMART constraint extractor from mockup HTML comments.
 *
 * Mockups embed acceptance constraints in their leading HTML comment under
 * a `Vincoli SMART:` heading, e.g. (sp4-game-chat-tab.html):
 *
 *     Vincoli SMART (da spec G1+G5):
 *       - 90% query con citazione esplicita (pagina+sezione)
 *       - confidence < 70% → marker incertezza obbligatorio
 *       - latency P95 ≤ 10s con spinner accettabile
 *       - chat handoff <3s (G2 — non coperto qui ma compatibile con UX)
 *
 * Or with ID prefix (sp4-citation-pdf-viewer.html):
 *
 *     Vincoli SMART (da spec G4 v3):
 *       - G4.2: tab "PDF originale" sempre visibile (anche non-owner)
 *       - G4.3: upsell render <100ms (no fetch)
 *
 * This script extracts each bullet as a `ConstraintRecord`. WS-E.2 will diff
 * extraction snapshots against the previous run and auto-file `mockup-spec-debt`
 * issues for constraints lacking implementation markers.
 *
 * Refs: #1071 (WS-E), #1066 (umbrella), AC-E.1 + AC-E.4 taxonomy.
 */
import { readFileSync, readdirSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join, resolve, basename } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const DEFAULT_MOCKUPS_DIR = resolve(HERE, '../../../admin-mockups/design_files');
const DEFAULT_OUTPUT_PATH = resolve(
  HERE,
  '../../../docs/for-developers/audits/mockup-smart-constraints.json'
);

/**
 * AC-E.4 taxonomy. Extensible; classify-keyword map below drives auto-tagging.
 *
 *  - `citation`        — citation chip / source provenance requirements
 *  - `confidence`      — uncertainty markers / confidence indicators
 *  - `latency`         — wall-clock end-to-end timing (user-perceived)
 *  - `performance`     — rendering / paint / frame timing (sub-perceptual)
 *  - `coverage`        — percentage of cases that must satisfy a criterion
 *  - `accessibility`   — WCAG / a11y attributes / contrast / keyboard nav
 *  - `ux-constraint`   — UI element visibility / interaction shape (qualitative)
 *  - `other`           — unclassified; fall-through bucket, manual review
 */
export type MetricType =
  | 'citation'
  | 'confidence'
  | 'latency'
  | 'performance'
  | 'coverage'
  | 'accessibility'
  | 'ux-constraint'
  | 'other';

export interface TargetValue {
  comparator: '<' | '≤' | '=' | '≥' | '>';
  value: number;
  unit: string;
}

export interface ConstraintRecord {
  /** Source mockup filename (basename). */
  mockup: string;
  /** Spec ID extracted from `G4.2: ...` prefix; null when bullet has no id. */
  id: string | null;
  /** Raw constraint text (after id stripping). */
  text: string;
  /** Best-effort classification per taxonomy (AC-E.4). */
  metric_type: MetricType;
  /** Parsed numeric target if a quantitative threshold is detectable. */
  target_value: TargetValue | null;
  /** Mockup files this constraint applies to (initially just the source mockup). */
  applies_to: string[];
}

const SMART_HEADER_RE = /^[\s>*]*Vincoli SMART\b[^\n]*$/im;
const COMMENT_RE = /<!--([\s\S]*?)-->/g;
const BULLET_RE = /^\s*-\s+(.+)$/;
const ID_PREFIX_RE = /^([A-Z]\d+(?:\.\d+)*)\s*:\s*(.*)$/;
const QUANT_RE = /(<=|>=|≤|≥|<|>|=)\s*(\d+(?:[.,]\d+)?)\s*(ms|s|min|h|%|fps|px|kb|mb|gb)\b/i;
// Leading percentage form: "90% query..." — note no \b after %, since both `%`
// and the following whitespace are non-word characters (no boundary in regex).
const PCT_PREFIX_RE = /^(\d+(?:[.,]\d+)?)\s*%/;

/**
 * Classification order is INTENTIONAL — earlier rules win. Quantitative
 * percentage-coverage and accessibility take priority over softer keyword
 * matches (citation/confidence/ux). Update tests in lockstep when reordering.
 */
const KEYWORD_TYPE_MAP: Array<{ re: RegExp; type: MetricType }> = [
  { re: /\bWCAG\b|\ba11y\b|\baccessibility\b|\bcontrast\b|\bkeyboard\b/i, type: 'accessibility' },
  // Coverage: leading percentage like "90% query..." (any noun after).
  { re: /^\s*\d+(?:[.,]\d+)?\s*%\s+\S+/i, type: 'coverage' },
  {
    re: /\bconfidence\b|\buncertain|marker\s+(incertezza|di\s+incertezza|uncertainty)/i,
    type: 'confidence',
  },
  { re: /\b(citation|citazione|source|sorgente)\b/i, type: 'citation' },
  // Latency: explicit keywords OR P95-style percentile OR seconds-with-words.
  { re: /\b(latency|handoff|TTFB|SLA)\b|\bP\d+\b/i, type: 'latency' },
  { re: /[<≤=≥>]\s*\d+(?:[.,]\d+)?\s*s\b/i, type: 'latency' },
  { re: /\b(render|paint|frame|repaint|FPS|fps)\b|[<≤=≥>]\s*\d+\s*ms\b/i, type: 'performance' },
  {
    re: /\b(sempre\s+visibile|niente|no\s+download|hidden|visible|disabled|enabled)\b/i,
    type: 'ux-constraint',
  },
];

export function classifyMetricType(text: string): MetricType {
  for (const { re, type } of KEYWORD_TYPE_MAP) {
    if (re.test(text)) return type;
  }
  return 'other';
}

function parseTargetValue(text: string): TargetValue | null {
  // Leading percentage form: "90% query..." → coverage threshold.
  const pct = text.match(PCT_PREFIX_RE);
  if (pct) {
    return { comparator: '≥', value: Number(pct[1].replace(',', '.')), unit: '%' };
  }
  // Inline quantitative form: "<100ms", "≤10s", "= 50px".
  const q = text.match(QUANT_RE);
  if (q) {
    const compMap: Record<string, TargetValue['comparator']> = {
      '<': '<',
      '<=': '≤',
      '≤': '≤',
      '>': '>',
      '>=': '≥',
      '≥': '≥',
      '=': '=',
    };
    return {
      comparator: compMap[q[1]],
      value: Number(q[2].replace(',', '.')),
      unit: q[3].toLowerCase(),
    };
  }
  return null;
}

export function extractSmartConstraints(html: string, mockup: string): ConstraintRecord[] {
  const out: ConstraintRecord[] = [];
  for (const match of html.matchAll(COMMENT_RE)) {
    const body = match[1];
    const headerMatch = body.match(SMART_HEADER_RE);
    if (!headerMatch) continue;

    const lines = body.split('\n');
    const headerIdx = lines.findIndex(line => SMART_HEADER_RE.test(line));
    if (headerIdx === -1) continue;

    // Collect contiguous bullets after the header.
    for (let i = headerIdx + 1; i < lines.length; i++) {
      const bulletMatch = lines[i].match(BULLET_RE);
      if (!bulletMatch) {
        // Blank line OR another label OR end of bullet block → stop.
        if (lines[i].trim() === '') continue;
        // Non-bullet, non-blank → end of SMART block.
        break;
      }
      const rawText = bulletMatch[1].trim();

      // Try ID prefix (G4.2: ...).
      let id: string | null = null;
      let text = rawText;
      const idMatch = rawText.match(ID_PREFIX_RE);
      if (idMatch) {
        id = idMatch[1];
        text = idMatch[2].trim();
      }

      out.push({
        mockup,
        id,
        text,
        metric_type: classifyMetricType(text),
        target_value: parseTargetValue(text),
        applies_to: [mockup],
      });
    }
  }
  return out;
}

export interface ExtractionReport {
  generatedAt: string;
  totalMockups: number;
  totalConstraints: number;
  byMetricType: Partial<Record<MetricType, number>>;
  constraints: ConstraintRecord[];
}

export function runExtraction(dir: string, now: Date = new Date()): ExtractionReport {
  const files = readdirSync(dir)
    .filter(f => f.endsWith('.html'))
    .map(f => join(dir, f))
    .sort();

  const allConstraints: ConstraintRecord[] = [];
  for (const file of files) {
    const html = readFileSync(file, 'utf8');
    allConstraints.push(...extractSmartConstraints(html, basename(file)));
  }

  const byMetricType: Partial<Record<MetricType, number>> = {};
  for (const c of allConstraints) {
    byMetricType[c.metric_type] = (byMetricType[c.metric_type] ?? 0) + 1;
  }

  return {
    generatedAt: now.toISOString().replace(/\.\d+Z$/, 'Z'),
    totalMockups: files.length,
    totalConstraints: allConstraints.length,
    byMetricType,
    constraints: allConstraints,
  };
}

const invokedAsCli =
  typeof process !== 'undefined' &&
  process.argv[1] &&
  fileURLToPath(import.meta.url) === resolve(process.argv[1]);

if (invokedAsCli) {
  const dir =
    process.argv.find(a => a.startsWith('--mockups='))?.slice('--mockups='.length) ??
    DEFAULT_MOCKUPS_DIR;
  const out =
    process.argv.find(a => a.startsWith('--out='))?.slice('--out='.length) ?? DEFAULT_OUTPUT_PATH;

  const report = runExtraction(dir);
  mkdirSync(dirname(out), { recursive: true });
  writeFileSync(out, JSON.stringify(report, null, 2) + '\n', 'utf8');

  console.log(`✓ Wrote ${out}`);
  console.log(
    `  ${report.totalMockups} mockups, ${report.totalConstraints} constraints extracted.`
  );
  const buckets = Object.entries(report.byMetricType)
    .sort((a, b) => (b[1] ?? 0) - (a[1] ?? 0))
    .map(([k, v]) => `${k}=${v}`)
    .join(' ');
  if (buckets) console.log(`  By metric_type: ${buckets}`);
}
