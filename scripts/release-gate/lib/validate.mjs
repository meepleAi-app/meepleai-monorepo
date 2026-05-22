// Schema validator for .github/release-gates.yml
// Tested by __tests__/validate.test.mjs.

import { readFileSync } from "node:fs";
import yaml from "js-yaml";
import { compileRegex } from "./classify.mjs";

const SUPPORTED_VERSION = 1;
const SEVERITIES = new Set(["blocker", "warning", "informational"]);
const OWNERS = new Set(["backend-dev", "frontend-dev", "qa", "devops", "unknown"]);
const OVERRIDE_PATHS = new Set(["fix-forward", "revert", "exception-comment", "baseline-update"]);

export function validateGates(gates) {
  const errors = [];

  if (!gates || typeof gates !== "object") {
    errors.push("root: gates must be an object");
    return { ok: false, errors };
  }

  if (gates.version !== SUPPORTED_VERSION) {
    errors.push(`version: unsupported version ${gates.version}, expected ${SUPPORTED_VERSION}`);
  }

  if (!Array.isArray(gates.checks)) {
    errors.push("checks: must be an array (use [] if empty)");
  } else {
    const seenNames = new Set();
    gates.checks.forEach((c, i) => {
      const prefix = `checks[${i}]`;
      if (typeof c.check_name !== "string" || c.check_name.trim() === "") {
        errors.push(`${prefix}.check_name: required non-empty string`);
      } else if (seenNames.has(c.check_name)) {
        errors.push(`${prefix}.check_name: duplicate entry "${c.check_name}"`);
      } else {
        seenNames.add(c.check_name);
      }
      if (!SEVERITIES.has(c.severity)) {
        errors.push(`${prefix}.severity: must be one of [${[...SEVERITIES].join(",")}], got "${c.severity}"`);
      }
      if (!OWNERS.has(c.owner)) {
        errors.push(`${prefix}.owner: must be one of [${[...OWNERS].join(",")}], got "${c.owner}"`);
      }
      if (!OVERRIDE_PATHS.has(c.override_path)) {
        errors.push(`${prefix}.override_path: must be one of [${[...OVERRIDE_PATHS].join(",")}], got "${c.override_path}"`);
      }
      if (typeof c.pre_existing_in_main_dev !== "boolean") {
        errors.push(`${prefix}.pre_existing_in_main_dev: required boolean`);
      }
      if (c.notes !== undefined && typeof c.notes !== "string") {
        errors.push(`${prefix}.notes: must be a string when present`);
      }
    });
  }

  if (gates.fallback_patterns !== undefined) {
    if (!Array.isArray(gates.fallback_patterns)) {
      errors.push("fallback_patterns: must be an array when present");
    } else {
      gates.fallback_patterns.forEach((fp, i) => {
        const prefix = `fallback_patterns[${i}]`;
        if (typeof fp.pattern !== "string" || fp.pattern === "") {
          errors.push(`${prefix}.pattern: required non-empty string`);
        } else {
          try {
            compileRegex(fp.pattern);
          } catch (err) {
            errors.push(`${prefix}.pattern: invalid regex "${fp.pattern}" — ${err.message}`);
          }
        }
        if (!SEVERITIES.has(fp.severity)) {
          errors.push(`${prefix}.severity: must be one of [${[...SEVERITIES].join(",")}], got "${fp.severity}"`);
        }
        if (!OWNERS.has(fp.owner)) {
          errors.push(`${prefix}.owner: must be one of [${[...OWNERS].join(",")}], got "${fp.owner}"`);
        }
        if (!OVERRIDE_PATHS.has(fp.override_path)) {
          errors.push(
            `${prefix}.override_path: must be one of [${[...OVERRIDE_PATHS].join(",")}], got "${fp.override_path}"`
          );
        }
      });
    }
  }

  if (!gates.bot || typeof gates.bot !== "object") {
    errors.push("bot: required object");
  } else {
    if (typeof gates.bot.signature_header !== "string" || !gates.bot.signature_header.startsWith("<!--")) {
      errors.push('bot.signature_header: required string starting with "<!--"');
    }
    const ve = gates.bot.verdict_emoji ?? {};
    for (const key of ["blocker", "warning", "informational", "unknown"]) {
      if (typeof ve[key] !== "string" || ve[key] === "") {
        errors.push(`bot.verdict_emoji.${key}: required non-empty string`);
      }
    }
    const fb = gates.bot.fallback_unknown ?? {};
    if (!SEVERITIES.has(fb.severity)) {
      errors.push(`bot.fallback_unknown.severity: must be a valid severity, got "${fb.severity}"`);
    }
    if (!OWNERS.has(fb.owner)) {
      errors.push(`bot.fallback_unknown.owner: must be a valid owner, got "${fb.owner}"`);
    }
    if (!OVERRIDE_PATHS.has(fb.override_path)) {
      errors.push(`bot.fallback_unknown.override_path: must be a valid override_path, got "${fb.override_path}"`);
    }

    // Phase 2c (#1446) — optional sub-key; validate only if present.
    if (gates.bot.phase2c !== undefined) {
      const p2c = gates.bot.phase2c;
      if (!p2c || typeof p2c !== "object") {
        errors.push("bot.phase2c: must be an object when present");
      } else {
        if (p2c.enabled !== undefined && typeof p2c.enabled !== "boolean") {
          errors.push(`bot.phase2c.enabled: must be boolean when present, got "${typeof p2c.enabled}"`);
        }
        if (p2c.escalation_threshold_weeks !== undefined) {
          const v = p2c.escalation_threshold_weeks;
          if (!Number.isInteger(v) || v < 1 || v > 52) {
            errors.push(
              `bot.phase2c.escalation_threshold_weeks: must be an integer 1..52, got "${v}"`
            );
          }
        }
        if (p2c.slack_webhook_env !== undefined && typeof p2c.slack_webhook_env !== "string") {
          errors.push("bot.phase2c.slack_webhook_env: must be a string when present");
        }
      }
    }
  }

  return { ok: errors.length === 0, errors };
}

export function validateGatesFile(filePath) {
  let raw;
  try {
    raw = readFileSync(filePath, "utf8");
  } catch (err) {
    return { ok: false, errors: [`${filePath}: ${err.message}`] };
  }
  let parsed;
  try {
    parsed = yaml.load(raw);
  } catch (err) {
    return { ok: false, errors: [`${filePath}: yaml parse error — ${err.message}`] };
  }
  const result = validateGates(parsed);
  return { ...result, parsed };
}
