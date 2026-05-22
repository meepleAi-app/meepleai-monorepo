import { describe, it, expect, beforeAll } from "vitest";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readFileSync } from "node:fs";
import yaml from "js-yaml";

import { classify, loadGates } from "../lib/classify.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const fixturePath = path.join(__dirname, "fixtures", "release-gates-valid.yml");

let gates;
beforeAll(() => {
  gates = loadGates(fixturePath);
});

describe("loadGates", () => {
  it("parses valid yaml and returns gates object with checks + fallback_patterns + bot", () => {
    expect(gates).toBeDefined();
    expect(gates.version).toBe(1);
    expect(Array.isArray(gates.checks)).toBe(true);
    expect(gates.checks).toHaveLength(3);
    expect(Array.isArray(gates.fallback_patterns)).toBe(true);
    expect(gates.fallback_patterns).toHaveLength(2);
    expect(gates.bot.signature_header).toBe("<!-- release-gate-bot:v1 -->");
  });
});

describe("classify — exact match (AC-2)", () => {
  it("classifies known blocker by exact check_name", () => {
    const result = classify("Backend - Unit Tests", gates);
    expect(result.severity).toBe("blocker");
    expect(result.owner).toBe("backend-dev");
    expect(result.override_path).toBe("fix-forward");
    expect(result.source).toBe("exact");
    expect(result.matched_by).toBe("Backend - Unit Tests");
  });

  it("classifies known warning by exact check_name", () => {
    const result = classify("Frontend - A11y E2E", gates);
    expect(result.severity).toBe("warning");
    expect(result.owner).toBe("qa");
    expect(result.override_path).toBe("baseline-update");
    expect(result.source).toBe("exact");
  });

  it("classifies known informational by exact check_name", () => {
    const result = classify("Lychee Link Check", gates);
    expect(result.severity).toBe("informational");
    expect(result.owner).toBe("devops");
    expect(result.override_path).toBe("exception-comment");
    expect(result.source).toBe("exact");
  });

  it("attaches notes from matched entry", () => {
    const result = classify("Backend - Unit Tests", gates);
    expect(result.notes).toBe("xUnit suite.");
  });

  it("flags pre_existing_in_main_dev correctly", () => {
    expect(classify("Frontend - A11y E2E", gates).pre_existing_in_main_dev).toBe(true);
    expect(classify("Backend - Unit Tests", gates).pre_existing_in_main_dev).toBe(false);
  });
});

describe("classify — fallback regex (AC-9)", () => {
  it("matches Backend - prefix fallback for unknown Backend integration", () => {
    const result = classify("Backend - Integration (NewBC)", gates);
    expect(result.severity).toBe("blocker");
    expect(result.owner).toBe("backend-dev");
    expect(result.override_path).toBe("fix-forward");
    expect(result.source).toBe("fallback");
    expect(result.matched_by).toBe("^Backend - ");
  });

  it("matches case-insensitive security pattern", () => {
    const codeqlResult = classify("CodeQL", gates);
    expect(codeqlResult.severity).toBe("blocker");
    expect(codeqlResult.owner).toBe("devops");
    expect(codeqlResult.override_path).toBe("revert");
    expect(codeqlResult.source).toBe("fallback");

    const securityResult = classify("Security Scan (Snyk)", gates);
    expect(securityResult.severity).toBe("blocker");
    expect(securityResult.source).toBe("fallback");
  });

  it("first matching pattern wins", () => {
    // "Backend - " matches before "(?i)security" even if name has "Security" in it
    const result = classify("Backend - Security Smoke", gates);
    expect(result.matched_by).toBe("^Backend - ");
    expect(result.severity).toBe("blocker");
    expect(result.owner).toBe("backend-dev");
  });
});

describe("classify — unknown fallback (AC-6)", () => {
  it("returns default fallback for unknown check name", () => {
    const result = classify("Brand New Workflow Check", gates);
    expect(result.severity).toBe("warning");
    expect(result.owner).toBe("unknown");
    expect(result.override_path).toBe("exception-comment");
    expect(result.source).toBe("unknown");
    expect(result.matched_by).toBeNull();
  });

  it("flags unknowns so they can be counted in summary", () => {
    const result = classify("Brand New Workflow Check", gates);
    expect(result.is_unknown).toBe(true);
  });

  it("known checks are not flagged as unknown", () => {
    const result = classify("Backend - Unit Tests", gates);
    expect(result.is_unknown).toBe(false);
  });
});

describe("classify — exact match has priority over fallback patterns", () => {
  it("exact check_name beats prefix pattern even if pattern would also match", () => {
    // "Backend - Unit Tests" matches BOTH exact entry AND "^Backend - " fallback
    // Exact must win.
    const result = classify("Backend - Unit Tests", gates);
    expect(result.source).toBe("exact");
    expect(result.matched_by).toBe("Backend - Unit Tests");
  });
});

describe("classify — input validation", () => {
  it("throws on empty check_name", () => {
    expect(() => classify("", gates)).toThrow(/check_name/);
    expect(() => classify(null, gates)).toThrow(/check_name/);
    expect(() => classify(undefined, gates)).toThrow(/check_name/);
  });

  it("throws on missing gates argument", () => {
    expect(() => classify("Backend - Unit Tests", null)).toThrow(/gates/);
    expect(() => classify("Backend - Unit Tests", undefined)).toThrow(/gates/);
  });

  it("trims whitespace in check_name", () => {
    const result = classify("  Backend - Unit Tests  ", gates);
    expect(result.severity).toBe("blocker");
    expect(result.source).toBe("exact");
  });
});
