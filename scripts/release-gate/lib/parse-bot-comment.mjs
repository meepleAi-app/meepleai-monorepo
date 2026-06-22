// Pure parser - extracts classified failure rows from a release-gate bot
// comment (markdown produced by lib/format.mjs::formatComment).
// Tested by __tests__/parse-bot-comment.test.mjs.

import { SIGNATURE_HEADER } from "./format.mjs";

// Strip leading emoji + whitespace, normalize to lowercased severity token.
// Accepts cells like "[red-X] blocker", "[triangle] warning", "[info]
// informational" produced by format.mjs, or bare severity tokens.
function normalizeSeverity(cell) {
  const lower = cell.toLowerCase();
  if (lower.includes("blocker")) return "blocker";
  if (lower.includes("warning")) return "warning";
  if (lower.includes("informational")) return "informational";
  return "unknown";
}

// Reverse the escaping applied by format.mjs::escapeCell:
//   backtick -> escaped backtick
//   pipe     -> escaped pipe
function unescapeCell(value) {
  return String(value ?? "")
    .replace(/\\\|/g, "|")
    .replace(/\\`/g, "`")
    .trim();
}

// "New check detected" tag emitted by format.mjs after the check name cell.
// Use Unicode escape (U+1F195) so this source file stays pure ASCII for
// portability across CI tooling.
const UNKNOWN_TAG = "\u{1F195}";
const SUCCESS_TICK = "✅"; // U+2705 "white heavy check mark" used by format.mjs

// Strip the surrounding backticks from a check_name cell, then unescape inner
// content. Detect and strip the "new" unknown tag, returning is_unknown flag.
function parseCheckNameCell(cell) {
  const raw = cell.trim();
  const pattern = new RegExp(
    `^\`([^\`]*(?:\\\\\`[^\`]*)*)\`\\s*(${UNKNOWN_TAG})?$`
  );
  const m = raw.match(pattern);
  if (!m) {
    return { check_name: unescapeCell(raw), is_unknown: false };
  }
  return {
    check_name: unescapeCell(m[1]),
    is_unknown: Boolean(m[2]),
  };
}

/**
 * Parse a release-gate bot comment markdown body and extract classified
 * failure rows.
 *
 * @param {string} markdown - Comment body
 * @returns {Array<{check_name, severity, owner, override_path, notes, is_unknown}> | null}
 *   - null if the markdown does not start with SIGNATURE_HEADER (not a bot comment)
 *   - [] if it IS a bot comment but contains no classifiable failures (e.g.
 *     the "no failed checks" success path, fallback/error comment, or
 *     malformed/empty table)
 *   - Array of row objects otherwise. Caller is responsible for severity
 *     filtering.
 */
export function parseBotComment(markdown) {
  if (typeof markdown !== "string") return null;
  if (!markdown.startsWith(SIGNATURE_HEADER)) return null;

  // No-failures success path (format.mjs emits "U+2705 No failed checks classified.")
  if (markdown.includes(`${SUCCESS_TICK} No failed checks classified.`)) return [];

  // Fallback (bot crash) path
  if (markdown.includes("**release-gate bot failed")) return [];

  const lines = markdown.split(/\r?\n/);

  // Locate the failure table header. The header row produced by formatComment
  // is `| Check | Severity | Owner | Override path | Notes |`.
  const headerIdx = lines.findIndex((l) =>
    /^\|\s*Check\s*\|\s*Severity\s*\|\s*Owner\s*\|/.test(l)
  );
  if (headerIdx === -1) return [];

  // Skip the alignment separator row (e.g. `|---|---|---|---|---|`).
  const dataStart = headerIdx + 2;
  const rows = [];
  for (let i = dataStart; i < lines.length; i++) {
    const line = lines[i];
    if (!line.startsWith("|")) break;
    if (/^\|\s*-+\s*\|/.test(line)) continue;
    // Split on un-escaped pipes. Use a split-and-unescape strategy: replace
    // escaped pipes with a sentinel, split on `|`, restore the sentinel.
    const SENTINEL = " PIPE ";
    const masked = line.replace(/\\\|/g, SENTINEL);
    const cells = masked
      .split("|")
      .slice(1, -1)
      .map((c) => c.replace(new RegExp(SENTINEL, "g"), "\\|"));
    if (cells.length < 5) continue;

    const { check_name, is_unknown } = parseCheckNameCell(cells[0]);
    const severity = normalizeSeverity(cells[1]);
    const owner = unescapeCell(cells[2]);
    const override_path = unescapeCell(cells[3]);
    const notes = unescapeCell(cells[4]);

    if (!check_name) continue;
    rows.push({ check_name, severity, owner, override_path, notes, is_unknown });
  }

  return rows;
}

/**
 * From a list of GitHub issue comments, pick the latest one that is a
 * release-gate bot comment (body starts with SIGNATURE_HEADER).
 *
 * @param {Array<{id, body, updated_at}>} comments
 * @returns {{id, body, updated_at} | null}
 */
export function pickLatestBotComment(comments) {
  if (!Array.isArray(comments)) return null;
  const botComments = comments.filter(
    (c) => typeof c?.body === "string" && c.body.startsWith(SIGNATURE_HEADER)
  );
  if (botComments.length === 0) return null;
  // Sort descending by updated_at; tie-break on id (numerically higher = newer).
  botComments.sort((a, b) => {
    const ta = new Date(a.updated_at ?? 0).getTime();
    const tb = new Date(b.updated_at ?? 0).getTime();
    if (tb !== ta) return tb - ta;
    return (b.id ?? 0) - (a.id ?? 0);
  });
  return botComments[0];
}
