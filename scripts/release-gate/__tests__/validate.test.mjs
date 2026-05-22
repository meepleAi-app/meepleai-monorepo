import { describe, it, expect } from "vitest";
import path from "node:path";
import { fileURLToPath } from "node:url";

import { validateGates, validateGatesFile } from "../lib/validate.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const fixturesDir = path.join(__dirname, "fixtures");

describe("validateGates (in-memory)", () => {
  it("accepts a minimal valid gate", () => {
    const gates = {
      version: 1,
      checks: [
        {
          check_name: "X",
          severity: "warning",
          owner: "devops",
          override_path: "exception-comment",
          pre_existing_in_main_dev: false,
        },
      ],
      bot: {
        signature_header: "<!-- release-gate-bot:v1 -->",
        verdict_emoji: { blocker: "X", warning: "X", informational: "X", unknown: "X" },
        fallback_unknown: { severity: "warning", owner: "unknown", override_path: "exception-comment" },
      },
    };
    const result = validateGates(gates);
    expect(result.ok).toBe(true);
    expect(result.errors).toHaveLength(0);
  });

  it("rejects wrong version", () => {
    const result = validateGates({ version: 99, checks: [] });
    expect(result.ok).toBe(false);
    expect(result.errors.some((e) => /version/.test(e))).toBe(true);
  });

  it("rejects bad severity enum", () => {
    const gates = {
      version: 1,
      checks: [
        {
          check_name: "X",
          severity: "catastrophic",
          owner: "devops",
          override_path: "exception-comment",
          pre_existing_in_main_dev: false,
        },
      ],
      bot: {
        signature_header: "<!-- release-gate-bot:v1 -->",
        verdict_emoji: { blocker: "X", warning: "X", informational: "X", unknown: "X" },
        fallback_unknown: { severity: "warning", owner: "unknown", override_path: "exception-comment" },
      },
    };
    const result = validateGates(gates);
    expect(result.ok).toBe(false);
    expect(result.errors.some((e) => /severity/.test(e))).toBe(true);
  });

  it("rejects bad owner enum", () => {
    const gates = {
      version: 1,
      checks: [
        {
          check_name: "X",
          severity: "warning",
          owner: "not-a-team",
          override_path: "exception-comment",
          pre_existing_in_main_dev: false,
        },
      ],
      bot: {
        signature_header: "<!-- release-gate-bot:v1 -->",
        verdict_emoji: { blocker: "X", warning: "X", informational: "X", unknown: "X" },
        fallback_unknown: { severity: "warning", owner: "unknown", override_path: "exception-comment" },
      },
    };
    const result = validateGates(gates);
    expect(result.ok).toBe(false);
    expect(result.errors.some((e) => /owner/.test(e))).toBe(true);
  });

  it("rejects bad override_path enum", () => {
    const gates = {
      version: 1,
      checks: [
        {
          check_name: "X",
          severity: "warning",
          owner: "devops",
          override_path: "yolo",
          pre_existing_in_main_dev: false,
        },
      ],
      bot: {
        signature_header: "<!-- release-gate-bot:v1 -->",
        verdict_emoji: { blocker: "X", warning: "X", informational: "X", unknown: "X" },
        fallback_unknown: { severity: "warning", owner: "unknown", override_path: "exception-comment" },
      },
    };
    const result = validateGates(gates);
    expect(result.ok).toBe(false);
    expect(result.errors.some((e) => /override_path/.test(e))).toBe(true);
  });

  it("rejects missing required field", () => {
    const gates = {
      version: 1,
      checks: [
        {
          check_name: "X",
          severity: "warning",
          // owner missing
          override_path: "exception-comment",
          pre_existing_in_main_dev: false,
        },
      ],
      bot: {
        signature_header: "<!-- release-gate-bot:v1 -->",
        verdict_emoji: { blocker: "X", warning: "X", informational: "X", unknown: "X" },
        fallback_unknown: { severity: "warning", owner: "unknown", override_path: "exception-comment" },
      },
    };
    const result = validateGates(gates);
    expect(result.ok).toBe(false);
    expect(result.errors.some((e) => /owner/.test(e))).toBe(true);
  });

  it("rejects duplicate check_name entries", () => {
    const gates = {
      version: 1,
      checks: [
        {
          check_name: "X",
          severity: "warning",
          owner: "devops",
          override_path: "exception-comment",
          pre_existing_in_main_dev: false,
        },
        {
          check_name: "X",
          severity: "blocker",
          owner: "backend-dev",
          override_path: "fix-forward",
          pre_existing_in_main_dev: false,
        },
      ],
      bot: {
        signature_header: "<!-- release-gate-bot:v1 -->",
        verdict_emoji: { blocker: "X", warning: "X", informational: "X", unknown: "X" },
        fallback_unknown: { severity: "warning", owner: "unknown", override_path: "exception-comment" },
      },
    };
    const result = validateGates(gates);
    expect(result.ok).toBe(false);
    expect(result.errors.some((e) => /duplicate/i.test(e))).toBe(true);
  });

  it("accepts a valid bot.phase2c config block (#1446)", () => {
    const gates = {
      version: 1,
      checks: [],
      bot: {
        signature_header: "<!-- release-gate-bot:v1 -->",
        verdict_emoji: { blocker: "X", warning: "X", informational: "X", unknown: "X" },
        fallback_unknown: { severity: "warning", owner: "unknown", override_path: "exception-comment" },
        phase2c: {
          enabled: true,
          escalation_threshold_weeks: 4,
          slack_webhook_env: "SLACK_GITNOTIFY_WEBHOOK_URL",
        },
      },
    };
    const result = validateGates(gates);
    expect(result.ok).toBe(true);
  });

  it("rejects bot.phase2c.enabled with non-boolean type (#1446)", () => {
    const gates = {
      version: 1,
      checks: [],
      bot: {
        signature_header: "<!-- release-gate-bot:v1 -->",
        verdict_emoji: { blocker: "X", warning: "X", informational: "X", unknown: "X" },
        fallback_unknown: { severity: "warning", owner: "unknown", override_path: "exception-comment" },
        phase2c: {
          enabled: "yes", // wrong type
        },
      },
    };
    const result = validateGates(gates);
    expect(result.ok).toBe(false);
    expect(result.errors.some((e) => /phase2c\.enabled/.test(e))).toBe(true);
  });

  it("rejects bot.phase2c.escalation_threshold_weeks out of 1..52 range (#1446)", () => {
    const gates = {
      version: 1,
      checks: [],
      bot: {
        signature_header: "<!-- release-gate-bot:v1 -->",
        verdict_emoji: { blocker: "X", warning: "X", informational: "X", unknown: "X" },
        fallback_unknown: { severity: "warning", owner: "unknown", override_path: "exception-comment" },
        phase2c: {
          enabled: true,
          escalation_threshold_weeks: 100, // > 52
        },
      },
    };
    const result = validateGates(gates);
    expect(result.ok).toBe(false);
    expect(result.errors.some((e) => /escalation_threshold_weeks/.test(e))).toBe(true);
  });

  it("rejects invalid fallback_patterns regex", () => {
    const gates = {
      version: 1,
      checks: [],
      fallback_patterns: [
        {
          pattern: "[invalid regex (",
          severity: "warning",
          owner: "devops",
          override_path: "exception-comment",
        },
      ],
      bot: {
        signature_header: "<!-- release-gate-bot:v1 -->",
        verdict_emoji: { blocker: "X", warning: "X", informational: "X", unknown: "X" },
        fallback_unknown: { severity: "warning", owner: "unknown", override_path: "exception-comment" },
      },
    };
    const result = validateGates(gates);
    expect(result.ok).toBe(false);
    expect(result.errors.some((e) => /regex|pattern/i.test(e))).toBe(true);
  });
});

describe("validateGatesFile (file I/O)", () => {
  it("accepts the canonical valid fixture", () => {
    const result = validateGatesFile(path.join(fixturesDir, "release-gates-valid.yml"));
    expect(result.ok).toBe(true);
    expect(result.errors).toHaveLength(0);
  });

  it("rejects invalid-version fixture", () => {
    const result = validateGatesFile(path.join(fixturesDir, "release-gates-invalid-version.yml"));
    expect(result.ok).toBe(false);
  });

  it("rejects invalid-severity fixture", () => {
    const result = validateGatesFile(path.join(fixturesDir, "release-gates-invalid-severity.yml"));
    expect(result.ok).toBe(false);
    expect(result.errors.some((e) => /severity/.test(e))).toBe(true);
  });

  it("rejects missing-field fixture", () => {
    const result = validateGatesFile(path.join(fixturesDir, "release-gates-missing-field.yml"));
    expect(result.ok).toBe(false);
    expect(result.errors.some((e) => /owner/.test(e))).toBe(true);
  });

  it("returns parse error on missing file", () => {
    const result = validateGatesFile(path.join(fixturesDir, "nonexistent.yml"));
    expect(result.ok).toBe(false);
    expect(result.errors[0]).toMatch(/ENOENT|not found|no such file/i);
  });

  it("validates the project's authoritative .github/release-gates.yml", () => {
    // The actual file in the repo MUST be valid.
    const repoYamlPath = path.join(__dirname, "..", "..", "..", ".github", "release-gates.yml");
    const result = validateGatesFile(repoYamlPath);
    expect(result.ok).toBe(true);
    expect(result.errors).toHaveLength(0);
  });
});
