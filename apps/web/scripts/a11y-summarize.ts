/**
 * a11y-summarize — classify Playwright a11y suite failures and emit
 * a GitHub-Checks-friendly summary + workflow annotations.
 *
 * Spec: issue #1698. Reads the Playwright JSON reporter output, distinguishes
 * axe AA violations from infra flakes (timeouts, fixture errors, non-axe
 * assertions), and produces:
 *   - a deterministic markdown summary for `$GITHUB_STEP_SUMMARY`
 *   - one `::error::` / `::warning::` workflow annotation per failure
 *   - an exit code: 0 (all green), 1 (flake only), 2 (any axe — dominant)
 *
 * The classification heuristic looks for `"impact":"(minor|moderate|serious|critical)"`
 * in the Playwright error message. This pattern is emitted by axe-core's
 * violation array and does not appear in Playwright's own timeout / fixture
 * errors, so it is a reliable disambiguator without parsing the full
 * violation JSON.
 *
 * Mixed runs report the dominant class (axe wins over flake — AC #5) but
 * surface counts for both. See issue #1698 comment for Scenarios A/B/C/D.
 */

export type FailClass = 'axe' | 'flake';

export type AxeImpact = 'minor' | 'moderate' | 'serious' | 'critical';

export interface AxeViolation {
  route: string;
  rule: string;
  selector: string;
  impact: AxeImpact;
}

export interface PlaywrightFlake {
  spec: string;
  line: number;
  failureType: string;
  messagePreview: string;
}

export interface SummarizeResult {
  exitCode: 0 | 1 | 2;
  summary: string;
  annotations: string[];
  dominantClass: FailClass | 'none';
  axeViolations: AxeViolation[];
  flakes: PlaywrightFlake[];
  totalTests: number;
  passedTests: number;
  durationMs: number;
}

const AXE_IMPACT_PATTERN = /"impact"\s*:\s*"(minor|moderate|serious|critical)"/;
const AXE_RULE_PATTERN = /"id"\s*:\s*"([a-z][a-z0-9-]+)"/;
// Matches both pure JSON (`"target":["sel"]`) and Vitest/Jest deep-equality
// diff output (`"target": Array [\n+   "sel",\n+ ],`). The `[^"]*` chunk
// greedily eats whitespace, newlines, and diff `+` prefixes between the
// opening bracket and the first quoted selector.
const AXE_NODES_TARGET_PATTERN = /"target"\s*:\s*(?:Array\s+)?\[[^"]*"([^"]+)"/;

export function classify(errorMessage: string): FailClass {
  return AXE_IMPACT_PATTERN.test(errorMessage) ? 'axe' : 'flake';
}

interface PlaywrightTestError {
  message?: string;
  stack?: string;
  location?: { file?: string; line?: number; column?: number };
}

interface PlaywrightTestResult {
  status?: string;
  duration?: number;
  errors?: PlaywrightTestError[];
}

interface PlaywrightTestEntry {
  results?: PlaywrightTestResult[];
  projectName?: string;
}

interface PlaywrightSpecEntry {
  title?: string;
  ok?: boolean;
  tests?: PlaywrightTestEntry[];
  file?: string;
  line?: number;
}

interface PlaywrightSuiteEntry {
  title?: string;
  file?: string;
  specs?: PlaywrightSpecEntry[];
  suites?: PlaywrightSuiteEntry[];
}

interface PlaywrightReportShape {
  suites?: PlaywrightSuiteEntry[];
  stats?: {
    expected?: number;
    unexpected?: number;
    skipped?: number;
    flaky?: number;
    duration?: number;
  };
}

function flattenSpecs(suites: PlaywrightSuiteEntry[]): Array<{ spec: PlaywrightSpecEntry; suiteFile: string }> {
  const out: Array<{ spec: PlaywrightSpecEntry; suiteFile: string }> = [];
  for (const suite of suites) {
    const suiteFile = suite.file ?? suite.title ?? '';
    for (const spec of suite.specs ?? []) {
      out.push({ spec, suiteFile });
    }
    if (suite.suites) {
      out.push(...flattenSpecs(suite.suites));
    }
  }
  return out;
}

function extractRoute(specTitle: string): string {
  // axe specs name themselves after the route under test; e.g.
  // "/library (games tab) — color-contrast" or "Library page accessibility"
  const slashMatch = specTitle.match(/(\/[a-z0-9/[\]_-]+)/i);
  if (slashMatch) return slashMatch[1];
  return specTitle;
}

function extractAxeDetails(message: string, fallbackRoute: string): { rule: string; selector: string; impact: AxeImpact } {
  const ruleMatch = message.match(AXE_RULE_PATTERN);
  const impactMatch = message.match(AXE_IMPACT_PATTERN);
  const targetMatch = message.match(AXE_NODES_TARGET_PATTERN);
  return {
    rule: ruleMatch?.[1] ?? 'unknown-rule',
    selector: targetMatch?.[1] ?? 'unknown-selector',
    impact: (impactMatch?.[1] as AxeImpact) ?? 'moderate',
  };
}

function extractFlakeFailureType(message: string): string {
  if (/Test timeout of \d+ms exceeded|TimeoutError|Timeout \d+ms exceeded/.test(message)) {
    return 'Playwright timeout';
  }
  if (/locator\.\w+/.test(message)) {
    return 'Locator failure';
  }
  if (/Error: page\./.test(message)) {
    return 'Page operation failure';
  }
  return 'Assertion failure';
}

function previewMessage(message: string, maxLen = 120): string {
  const firstLine = message.split('\n')[0]?.trim() ?? '';
  return firstLine.length > maxLen ? `${firstLine.slice(0, maxLen - 1)}…` : firstLine;
}

function dedup<T>(items: T[], key: (t: T) => string): T[] {
  const seen = new Set<string>();
  const out: T[] = [];
  for (const it of items) {
    const k = key(it);
    if (!seen.has(k)) {
      seen.add(k);
      out.push(it);
    }
  }
  return out;
}

function sortAxe(violations: AxeViolation[]): AxeViolation[] {
  return [...violations].sort((a, b) => {
    if (a.route !== b.route) return a.route.localeCompare(b.route);
    if (a.rule !== b.rule) return a.rule.localeCompare(b.rule);
    return a.selector.localeCompare(b.selector);
  });
}

function sortFlakes(flakes: PlaywrightFlake[]): PlaywrightFlake[] {
  return [...flakes].sort((a, b) => {
    if (a.spec !== b.spec) return a.spec.localeCompare(b.spec);
    if (a.line !== b.line) return a.line - b.line;
    return a.failureType.localeCompare(b.failureType);
  });
}

function renderSummary(result: Omit<SummarizeResult, 'summary' | 'annotations' | 'exitCode'>): string {
  const { axeViolations, flakes, totalTests, passedTests, durationMs, dominantClass } = result;
  const durationStr = formatDuration(durationMs);

  if (dominantClass === 'none') {
    return [
      `## 🟢 Frontend A11y E2E — ${totalTests} tests passed`,
      '',
      `- ✅ axe AA violations: 0`,
      `- ✅ Playwright failures: 0`,
      `- Duration: ${durationStr}`,
      '',
    ].join('\n');
  }

  const lines: string[] = [];
  const axeCount = axeViolations.length;
  const flakeCount = flakes.length;

  if (dominantClass === 'axe') {
    const flakeSuffix = flakeCount > 0 ? ` (+ ${flakeCount} flake${flakeCount === 1 ? '' : 's'})` : '';
    lines.push(`## 🔴 Frontend A11y E2E — axe AA violations detected${flakeSuffix}`);
    lines.push('');
    lines.push(`**Class**: axe (dominant${flakeCount > 0 ? ' — axe wins over flake per AC #5' : ''})`);
    lines.push(`**Total**: ${axeCount} axe violation${axeCount === 1 ? '' : 's'}, ${flakeCount} flake${flakeCount === 1 ? '' : 's'}`);
    lines.push('');
    lines.push(flakeCount > 0 ? '### axe violations' : '');
    lines.push('| Route | Rule | Selector | Impact |');
    lines.push('|---|---|---|---|');
    for (const v of axeViolations) {
      lines.push(`| ${v.route} | ${v.rule} | \`${v.selector}\` | ${v.impact} |`);
    }
    if (flakeCount > 0) {
      lines.push('');
      lines.push('### Infrastructure failures (non-blocking signal but still failing)');
      lines.push('| Spec | Failure type |');
      lines.push('|---|---|');
      for (const f of flakes) {
        lines.push(`| \`${f.spec}:${f.line}\` | ${f.failureType} |`);
      }
    }
    lines.push('');
    lines.push(`Full report: artifact \`playwright-report-a11y-\${run_number}\`.`);
    lines.push('');
  } else {
    // flake dominant
    lines.push(`## ⚠️ Frontend A11y E2E — infrastructure failure (no axe violations)`);
    lines.push('');
    lines.push(`**Class**: flake (dominant)`);
    lines.push(`**Total**: 0 axe violations, ${flakeCount} flake${flakeCount === 1 ? '' : 's'}`);
    lines.push('');
    lines.push('| Spec | Failure type | Message preview |');
    lines.push('|---|---|---|');
    for (const f of flakes) {
      lines.push(`| \`${f.spec}:${f.line}\` | ${f.failureType} | ${f.messagePreview} |`);
    }
    lines.push('');
    lines.push('This is a test-infrastructure issue, NOT an a11y regression. PR author can request CI re-run; test owner investigates.');
    lines.push('');
  }

  lines.push(`- Tests: ${passedTests}/${totalTests} passed`);
  lines.push(`- Duration: ${durationStr}`);
  lines.push('');

  return lines.filter((l, i, arr) => !(l === '' && arr[i - 1] === '')).join('\n');
}

function renderAnnotations(axe: AxeViolation[], flakes: PlaywrightFlake[]): string[] {
  const out: string[] = [];
  for (const v of axe) {
    out.push(`::error title=A11y axe AA violation::${v.route} | rule=${v.rule} | impact=${v.impact} | selector=${v.selector}`);
  }
  for (const f of flakes) {
    out.push(`::warning file=${f.spec},line=${f.line},title=A11y test infrastructure flake::${f.failureType}: ${f.messagePreview}`);
  }
  return out;
}

function formatDuration(ms: number): string {
  if (ms < 1000) return `${ms}ms`;
  const totalSeconds = Math.round(ms / 1000);
  const m = Math.floor(totalSeconds / 60);
  const s = totalSeconds % 60;
  return m > 0 ? `${m}m ${s}s` : `${s}s`;
}

export function summarize(report: unknown): SummarizeResult {
  if (typeof report !== 'object' || report === null || !('suites' in report)) {
    throw new TypeError('a11y-summarize: input is not a Playwright JSON report (missing "suites" key)');
  }
  const rep = report as PlaywrightReportShape;
  const specs = flattenSpecs(rep.suites ?? []);

  const rawAxe: AxeViolation[] = [];
  const rawFlakes: PlaywrightFlake[] = [];
  let totalTests = 0;
  let passedTests = 0;

  for (const { spec, suiteFile } of specs) {
    for (const t of spec.tests ?? []) {
      totalTests += 1;
      const lastResult = t.results?.[t.results.length - 1];
      if (!lastResult) continue;
      if (lastResult.status === 'passed') {
        passedTests += 1;
        continue;
      }
      for (const err of lastResult.errors ?? []) {
        const msg = err.message ?? '';
        const cls = classify(msg);
        const route = extractRoute(spec.title ?? '');
        if (cls === 'axe') {
          const details = extractAxeDetails(msg, route);
          rawAxe.push({ route, ...details });
        } else {
          rawFlakes.push({
            spec: err.location?.file ?? spec.file ?? suiteFile,
            line: err.location?.line ?? spec.line ?? 0,
            failureType: extractFlakeFailureType(msg),
            messagePreview: previewMessage(msg),
          });
        }
      }
    }
  }

  const axeViolations = sortAxe(dedup(rawAxe, v => `${v.route}|${v.rule}|${v.selector}`));
  const flakes = sortFlakes(dedup(rawFlakes, f => `${f.spec}|${f.line}|${f.failureType}|${f.messagePreview}`));

  const dominantClass: FailClass | 'none' =
    axeViolations.length > 0 ? 'axe' : flakes.length > 0 ? 'flake' : 'none';

  const exitCode: 0 | 1 | 2 =
    axeViolations.length > 0 ? 2 : flakes.length > 0 ? 1 : 0;

  const stats = rep.stats ?? {};
  const durationMs = stats.duration ?? 0;
  // Prefer stats.expected for totalTests (Playwright's own count) when it's
  // higher than what we walked (catches setup/teardown skipped tests).
  const totalFromStats = stats.expected ?? 0;
  const total = Math.max(totalTests, totalFromStats);
  const passed = Math.max(0, total - axeViolations.length - flakes.length);
  // Note: passedTests above tracks results-level; total is suite-level. The
  // simple difference is sufficient for summary text purposes.

  const partial = {
    dominantClass,
    axeViolations,
    flakes,
    totalTests: total,
    passedTests: passed,
    durationMs,
  };
  const summary = renderSummary(partial);
  const annotations = renderAnnotations(axeViolations, flakes);

  return {
    exitCode,
    summary,
    annotations,
    ...partial,
  };
}
