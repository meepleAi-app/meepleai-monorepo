#!/usr/bin/env node
// CLI entry — validates .github/release-gates.yml against schema v1.
// Exit code 0 if valid, 1 if invalid, 2 on I/O / yaml-parse failure.
// Usage:
//   node scripts/release-gate/validate.mjs             # default .github/release-gates.yml
//   node scripts/release-gate/validate.mjs path/to.yml # explicit path

import path from "node:path";
import { fileURLToPath } from "node:url";
import { existsSync } from "node:fs";

import { validateGatesFile } from "./lib/validate.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(__dirname, "..", "..");
const DEFAULT_PATH = path.join(REPO_ROOT, ".github", "release-gates.yml");

const explicit = process.argv[2];
const target = explicit ? path.resolve(process.cwd(), explicit) : DEFAULT_PATH;

if (!existsSync(target)) {
  console.error(`[release-gate:validate] FAIL — file not found: ${target}`);
  process.exit(2);
}

const result = validateGatesFile(target);

if (result.ok) {
  const checkCount = result.parsed?.checks?.length ?? 0;
  const fpCount = result.parsed?.fallback_patterns?.length ?? 0;
  console.log(
    `[release-gate:validate] OK — ${target}: ${checkCount} checks + ${fpCount} fallback patterns valid (schema v${result.parsed?.version}).`
  );
  process.exit(0);
}

console.error(`[release-gate:validate] FAIL — ${target}: ${result.errors.length} error(s)`);
for (const err of result.errors) {
  console.error(`  - ${err}`);
}
process.exit(1);
