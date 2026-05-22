// Pure classification logic — no I/O side effects except loadGates (file read).
// Tested by __tests__/classify.test.mjs.

import { readFileSync } from "node:fs";
import yaml from "js-yaml";

export function loadGates(filePath) {
  const raw = readFileSync(filePath, "utf8");
  const parsed = yaml.load(raw);
  if (!parsed || typeof parsed !== "object") {
    throw new Error(`release-gates yaml at ${filePath} did not parse to an object`);
  }
  return parsed;
}

// Compile a pattern that may use Perl-style `(?i)` inline case-insensitive flag.
// JavaScript RegExp does not support inline flags, so strip and convert.
export function compileRegex(pattern) {
  let flags = "";
  let p = pattern;
  if (p.startsWith("(?i)")) {
    flags = "i";
    p = p.slice(4);
  }
  return new RegExp(p, flags);
}

export function classify(checkName, gates) {
  if (checkName === null || checkName === undefined || typeof checkName !== "string" || checkName.trim() === "") {
    throw new Error("classify: check_name is required and must be a non-empty string");
  }
  if (!gates || typeof gates !== "object") {
    throw new Error("classify: gates object is required");
  }

  const name = checkName.trim();

  // 1. Exact match wins (AC-2)
  if (Array.isArray(gates.checks)) {
    const exact = gates.checks.find((c) => c.check_name === name);
    if (exact) {
      return {
        severity: exact.severity,
        owner: exact.owner,
        override_path: exact.override_path,
        pre_existing_in_main_dev: Boolean(exact.pre_existing_in_main_dev),
        notes: exact.notes ?? null,
        source: "exact",
        matched_by: exact.check_name,
        is_unknown: false,
      };
    }
  }

  // 2. Fallback patterns (AC-9), first match wins
  if (Array.isArray(gates.fallback_patterns)) {
    for (const fp of gates.fallback_patterns) {
      let re;
      try {
        re = compileRegex(fp.pattern);
      } catch {
        // skip invalid regex (validator should have caught this)
        continue;
      }
      if (re.test(name)) {
        return {
          severity: fp.severity,
          owner: fp.owner,
          override_path: fp.override_path,
          pre_existing_in_main_dev: false,
          notes: fp.notes ?? null,
          source: "fallback",
          matched_by: fp.pattern,
          is_unknown: false,
        };
      }
    }
  }

  // 3. Unknown fallback (AC-6)
  const fb = gates.bot?.fallback_unknown ?? {
    severity: "warning",
    owner: "unknown",
    override_path: "exception-comment",
  };
  return {
    severity: fb.severity,
    owner: fb.owner,
    override_path: fb.override_path,
    pre_existing_in_main_dev: false,
    notes: null,
    source: "unknown",
    matched_by: null,
    is_unknown: true,
  };
}
