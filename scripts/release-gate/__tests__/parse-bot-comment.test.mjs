import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import { parseBotComment, pickLatestBotComment } from "../lib/parse-bot-comment.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const sampleFixture = readFileSync(
  path.join(__dirname, "fixtures", "sample-bot-comment.md"),
  "utf8"
);

describe("parseBotComment — happy path", () => {
  it("extracts all failure rows from the markdown table", () => {
    const rows = parseBotComment(sampleFixture);
    expect(rows).toHaveLength(4);
  });

  it("captures check_name, severity, owner, notes per row", () => {
    const rows = parseBotComment(sampleFixture);
    expect(rows[0]).toMatchObject({
      check_name: "Frontend - A11y E2E",
      severity: "warning",
      owner: "qa",
    });
    expect(rows[0].notes).toContain("#1094");
  });

  it("preserves severity tier ordering as-listed", () => {
    const rows = parseBotComment(sampleFixture);
    expect(rows.map((r) => r.severity)).toEqual([
      "warning",
      "warning",
      "warning",
      "informational",
    ]);
  });
});

describe("parseBotComment — edge cases (test matrix #3, #4, #10, #12)", () => {
  it("returns null when SIGNATURE_HEADER is absent (test matrix #4: not a bot comment)", () => {
    const md = "## Some random PR comment\n\nNot from the bot.";
    expect(parseBotComment(md)).toBeNull();
  });

  it("returns [] when bot comment shows 'No failed checks classified' (test matrix #2)", () => {
    const md = `<!-- release-gate-bot:v1 -->

## Release-gate Classification

Commit \`abc1234\` · Generated 2026-05-22T10:00:00Z

✅ No failed checks classified.`;
    expect(parseBotComment(md)).toEqual([]);
  });

  it("returns [] when bot fallback (error) comment is encountered (test matrix #3 malformed)", () => {
    const md = `<!-- release-gate-bot:v1 -->

## Release-gate Classification

Commit \`abc1234\` · Generated 2026-05-22T10:00:00Z

⚠️ **release-gate bot failed — manual triage required.**

- Phase: \`fetch-check-runs\`
- Error: \`API rate limit exceeded\``;
    expect(parseBotComment(md)).toEqual([]);
  });

  it("returns [] when signature found but table is malformed / missing rows", () => {
    const md = `<!-- release-gate-bot:v1 -->

## Release-gate Classification

Commit \`abc1234\` · Generated 2026-05-22T10:00:00Z

**Verdict**: ⚠️ WARNING

0 blocker · 3 warning · 0 informational

| Check | Severity | Owner | Override path | Notes |
|---|---|---|---|---|`;
    expect(parseBotComment(md)).toEqual([]);
  });

  it("unescapes pipe characters inside check_name (escaped via \\| by formatComment)", () => {
    const md = `<!-- release-gate-bot:v1 -->

## Release-gate Classification

| Check | Severity | Owner | Override path | Notes |
|---|---|---|---|---|
| \`Backend - Weird \\| Pipe\` | ⚠️ warning | qa | exception-comment | x |`;
    const rows = parseBotComment(md);
    expect(rows).toHaveLength(1);
    expect(rows[0].check_name).toBe("Backend - Weird | Pipe");
  });

  it("unescapes backtick characters inside check_name (escaped via \\` by formatComment)", () => {
    const md = `<!-- release-gate-bot:v1 -->

## Release-gate Classification

| Check | Severity | Owner | Override path | Notes |
|---|---|---|---|---|
| \`Backend \\\`tick\\\` test\` | ⚠️ warning | qa | exception-comment | y |`;
    const rows = parseBotComment(md);
    expect(rows).toHaveLength(1);
    expect(rows[0].check_name).toBe("Backend `tick` test");
  });

  it("strips trailing 🆕 unknown tag and flags is_unknown=true", () => {
    const md = `<!-- release-gate-bot:v1 -->

## Release-gate Classification

| Check | Severity | Owner | Override path | Notes |
|---|---|---|---|---|
| \`New-Check-Name\` 🆕 | ⚠️ warning | unknown | exception-comment | New check detected |`;
    const rows = parseBotComment(md);
    expect(rows).toHaveLength(1);
    expect(rows[0].check_name).toBe("New-Check-Name");
    expect(rows[0].is_unknown).toBe(true);
  });

  it("normalizes severity tokens (strips emoji prefix)", () => {
    const md = `<!-- release-gate-bot:v1 -->

| Check | Severity | Owner | Override path | Notes |
|---|---|---|---|---|
| \`X\` | ❌ blocker | backend-dev | fix-forward | a |
| \`Y\` | ⚠️ warning | qa | baseline-update | b |
| \`Z\` | ℹ️ informational | devops | exception-comment | c |`;
    const rows = parseBotComment(md);
    expect(rows.map((r) => r.severity)).toEqual([
      "blocker",
      "warning",
      "informational",
    ]);
  });
});

describe("pickLatestBotComment — multi-comment ordering (test matrix #12)", () => {
  it("returns null when no bot comment is present", () => {
    const comments = [
      { id: 1, body: "## Random comment", updated_at: "2026-05-22T10:00:00Z" },
      { id: 2, body: "Another comment", updated_at: "2026-05-22T11:00:00Z" },
    ];
    expect(pickLatestBotComment(comments)).toBeNull();
  });

  it("returns the only bot comment when one exists", () => {
    const comments = [
      { id: 1, body: "## Random comment", updated_at: "2026-05-22T10:00:00Z" },
      {
        id: 2,
        body: "<!-- release-gate-bot:v1 -->\n## Classification",
        updated_at: "2026-05-22T11:00:00Z",
      },
    ];
    const picked = pickLatestBotComment(comments);
    expect(picked).not.toBeNull();
    expect(picked.id).toBe(2);
  });

  it("returns the latest by updated_at when multiple bot comments exist", () => {
    const comments = [
      {
        id: 1,
        body: "<!-- release-gate-bot:v1 -->\n## Older",
        updated_at: "2026-05-20T10:00:00Z",
      },
      {
        id: 2,
        body: "<!-- release-gate-bot:v1 -->\n## Newer",
        updated_at: "2026-05-22T10:00:00Z",
      },
      {
        id: 3,
        body: "<!-- release-gate-bot:v1 -->\n## Middle",
        updated_at: "2026-05-21T10:00:00Z",
      },
    ];
    const picked = pickLatestBotComment(comments);
    expect(picked.id).toBe(2);
  });

  it("ignores comments not starting with the signature (substring not enough)", () => {
    const comments = [
      {
        id: 1,
        body: "Some preamble\n<!-- release-gate-bot:v1 -->\n## Inline",
        updated_at: "2026-05-22T10:00:00Z",
      },
    ];
    expect(pickLatestBotComment(comments)).toBeNull();
  });
});
