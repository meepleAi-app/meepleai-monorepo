#!/usr/bin/env tsx
/**
 * WS-C Phase 2 — Verify mockup HTML pin policy (AC-C.8).
 *
 * Scans every `admin-mockups/design_files/*.html` and asserts:
 *   - Each declared `<script>` from `mockup-pin-policy.json` is present with
 *     the exact URL AND matching SRI integrity hash.
 *   - Each declared preconnect link is present (with crossorigin if required).
 *
 * Why this matters: the conformity gate (Phase 3) compares pixel diff between
 * mockup screenshot and live route. If a mockup file accidentally bumps React
 * (e.g. 18.3.1 → 19.0.0) or loses preconnect, the rendered output drifts and
 * baselines invalidate silently. This script catches that on CI before merge.
 *
 * Exit codes:
 *   0 — all mockups conform
 *   1 — at least one violation found (details printed)
 *
 * Usage:
 *   pnpm tsx scripts/verify-mockup-pins.ts             # verify
 *   pnpm tsx scripts/verify-mockup-pins.ts --json      # machine-readable
 *
 * Refs: #1069 (WS-C), #1066 (umbrella), AC-C.8.
 */
import { readFileSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadOwnership } from './conformity-ownership';

interface ScriptPin {
  name: string;
  url: string;
  integrity: string;
}

interface PreconnectPin {
  href: string;
  crossorigin?: boolean;
}

interface PinPolicy {
  version: 1;
  scripts: ScriptPin[];
  preconnects: PreconnectPin[];
}

export interface Violation {
  file: string;
  type: 'missing-script' | 'wrong-integrity' | 'missing-preconnect' | 'missing-crossorigin';
  expected: string;
  detail?: string;
}

const HERE = dirname(fileURLToPath(import.meta.url));
const DEFAULT_POLICY_PATH = join(HERE, 'mockup-pin-policy.json');
const DEFAULT_MOCKUPS_DIR = resolve(HERE, '../../../admin-mockups/design_files');
const DEFAULT_OWNERSHIP_PATH = resolve(
  HERE,
  '../e2e/visual-conformity/mockup-ownership.bootstrap.json'
);

export function loadPolicy(path: string): PinPolicy {
  const raw = readFileSync(path, 'utf8');
  const parsed = JSON.parse(raw) as PinPolicy;
  if (parsed.version !== 1) {
    throw new Error(`${path}: unsupported policy version ${String(parsed.version)}`);
  }
  if (!Array.isArray(parsed.scripts) || parsed.scripts.length === 0) {
    throw new Error(`${path}: scripts must be non-empty`);
  }
  if (!Array.isArray(parsed.preconnects)) {
    throw new Error(`${path}: preconnects must be an array`);
  }
  return parsed;
}

export function verifyMockup(file: string, html: string, policy: PinPolicy): Violation[] {
  const violations: Violation[] = [];

  for (const pin of policy.scripts) {
    // Conditional validation: if the mockup doesn't load this script at all,
    // it's not subject to the pin (e.g. Nanolith mockup is pure CSS).
    // We DO catch the case where a sibling version is loaded (e.g. react@19).
    const escaped = pin.url.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const exactTagRegex = new RegExp(`<script[^>]*\\bsrc=["']${escaped}["'][^>]*>`, 'i');
    const exactMatch = html.match(exactTagRegex);

    if (!exactMatch) {
      // Look for a sibling version of the same package (e.g. react@19.x).
      // Package name is the URL segment between `npm/` or `unpkg.com/` and `@`.
      const pkgMatch = pin.url.match(/(?:unpkg\.com|cdn\.jsdelivr\.net\/npm)\/([^@/]+)@/);
      if (pkgMatch) {
        const pkg = pkgMatch[1];
        const siblingRegex = new RegExp(
          `<script[^>]*\\bsrc=["'][^"']*\\b${pkg.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}@[^"']+["'][^>]*>`,
          'i'
        );
        if (siblingRegex.test(html)) {
          violations.push({
            file,
            type: 'missing-script',
            expected: pin.url,
            detail: `mockup loads a different version of "${pkg}"; expected pinned ${pin.url}`,
          });
        }
      }
      continue;
    }

    const integrityMatch = exactMatch[0].match(/\bintegrity=["']([^"']+)["']/i);
    if (!integrityMatch || integrityMatch[1] !== pin.integrity) {
      violations.push({
        file,
        type: 'wrong-integrity',
        expected: pin.integrity,
        detail: `expected integrity="${pin.integrity}", got ${integrityMatch ? `"${integrityMatch[1]}"` : '(missing)'}`,
      });
    }
  }

  for (const pre of policy.preconnects) {
    const escaped = pre.href.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const linkRegex = new RegExp(
      `<link[^>]*\\brel=["']preconnect["'][^>]*\\bhref=["']${escaped}["'][^>]*>|<link[^>]*\\bhref=["']${escaped}["'][^>]*\\brel=["']preconnect["'][^>]*>`,
      'i'
    );
    const match = html.match(linkRegex);
    if (!match) {
      violations.push({
        file,
        type: 'missing-preconnect',
        expected: pre.href,
        detail: `<link rel="preconnect" href="${pre.href}"> not found`,
      });
      continue;
    }
    if (pre.crossorigin && !/\bcrossorigin\b/.test(match[0])) {
      violations.push({
        file,
        type: 'missing-crossorigin',
        expected: `${pre.href} (crossorigin required)`,
        detail: `<link rel="preconnect" href="${pre.href}"> missing crossorigin attribute`,
      });
    }
  }

  return violations;
}

/**
 * Returns the absolute paths of mockups referenced by the ownership map.
 *
 * Scope rationale: only mockups in the ownership map are consumed by the
 * conformity gate, so they're the only ones whose pin drift would invalidate
 * a committed baseline. Non-ownership mockups (e.g. PDF viewer modal, design
 * exploration files) often use plain HTML/CSS without React; verifying them
 * against the React pin policy would produce false positives.
 */
export function listOwnedMockups(ownershipPath: string, mockupsDir: string): string[] {
  const ownership = loadOwnership(ownershipPath);
  return ownership.routes.map(r => join(mockupsDir, r.mockup)).sort();
}

export function runCli(
  opts: {
    policy?: string;
    mockups?: string;
    ownership?: string;
    json?: boolean;
  } = {}
): {
  ok: boolean;
  violations: Violation[];
  scannedCount: number;
} {
  const policyPath = opts.policy ?? DEFAULT_POLICY_PATH;
  const mockupsDir = opts.mockups ?? DEFAULT_MOCKUPS_DIR;
  const ownershipPath = opts.ownership ?? DEFAULT_OWNERSHIP_PATH;
  const policy = loadPolicy(policyPath);
  const files = listOwnedMockups(ownershipPath, mockupsDir);
  const violations: Violation[] = [];
  for (const file of files) {
    const html = readFileSync(file, 'utf8');
    violations.push(...verifyMockup(file, html, policy));
  }
  return { ok: violations.length === 0, violations, scannedCount: files.length };
}

// CLI entry. Skipped under vitest (process.argv[1] is the runner, not this file).
const invokedAsCli =
  typeof process !== 'undefined' &&
  process.argv[1] &&
  fileURLToPath(import.meta.url) === resolve(process.argv[1]);

if (invokedAsCli) {
  const args = process.argv.slice(2);
  const json = args.includes('--json');
  const result = runCli({ json });
  if (json) {
    console.log(JSON.stringify(result, null, 2));
  } else {
    if (result.ok) {
      console.log(`✓ ${result.scannedCount} mockups conform to pin policy`);
    } else {
      console.error(
        `✗ ${result.violations.length} pin violations across ${result.scannedCount} mockups:\n`
      );
      for (const v of result.violations) {
        console.error(`  ${v.file}`);
        console.error(`    ${v.type}: ${v.detail ?? v.expected}`);
      }
    }
  }
  process.exit(result.ok ? 0 : 1);
}
