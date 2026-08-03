#!/usr/bin/env node
// Issue #3498 lint gate: fail if a `test.fixme` re-appears in the cover E2E specs. The R2-strict
// cover assertion must stay a REAL-LOAD test (smoke-real-backend/cover-r2-strict.spec.ts), never a
// mocked `.fixme` false-green. Wire into CI (dev-fast lint step).
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';

const ROOTS = ['e2e/library', 'e2e/smoke-real-backend'];
const FIXME = /test\.fixme\s*\(/;

function walk(dir) {
  const out = [];
  for (const name of readdirSync(dir)) {
    const p = join(dir, name);
    if (statSync(p).isDirectory()) out.push(...walk(p));
    else if (/cover.*\.spec\.tsx?$/.test(name)) out.push(p);
  }
  return out;
}

const offenders = [];
for (const root of ROOTS) {
  let files = [];
  try {
    files = walk(root);
  } catch {
    // root may not exist yet — skip.
  }
  for (const file of files) {
    if (FIXME.test(readFileSync(file, 'utf8'))) offenders.push(file);
  }
}

if (offenders.length > 0) {
  console.error(
    `\n✖ no-cover-fixme (#3498): cover E2E specs must not use test.fixme — use the real-load ` +
      `assertion in smoke-real-backend/cover-r2-strict.spec.ts instead.\n  ` +
      offenders.join('\n  ') +
      '\n'
  );
  process.exit(1);
}
console.log('✓ no-cover-fixme: no test.fixme in cover E2E specs');
