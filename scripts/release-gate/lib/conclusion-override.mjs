// Phase 2a (#1444) — pure verdict synthesizer.
// Computes a single GH Checks API conclusion from classified failures so
// reviewers can gate on one row instead of N per-tier rows.
//
// Pure: no I/O, no octokit. Tested by __tests__/conclusion-override.test.mjs.

export const VERDICT_CHECK_NAME = "Release-Gate Verdict";
export const VERDICT_EXTERNAL_ID = "release-gate-verdict-v1";

// AC-9: GitHub Checks API output.text hard limit is 65_535 chars; we cap at
// 60_000 to leave 5_535 chars of headroom for the truncation footer + safety.
export const OUTPUT_TEXT_MAX = 60_000;

const TRUNC_FOOTER = "\n\n_... (table truncated — see release-gate bot comment for the full classification)_";

function isPhase2aEnabled(gates) {
  // Default-true semantics so the bot ships behaviorally on by adding the
  // sub-key; existing release-gates.yml files without phase2a still get the
  // verdict published. Operators opt OUT explicitly with `enabled: false`.
  if (!gates || typeof gates !== "object") return true;
  const p2a = gates.bot?.phase2a;
  if (!p2a || typeof p2a !== "object") return true;
  return p2a.enabled !== false;
}

function countByTier(failures) {
  // AC-11 (edge case matrix row 11): unknown / unrecognized severity rolls into
  // `warning` (least-surprise default — neutral conclusion, not silently passing).
  const counts = { blocker: 0, warning: 0, informational: 0 };
  for (const f of failures) {
    if (f?.is_unknown) {
      counts.warning += 1;
      continue;
    }
    const sev = f?.severity;
    if (sev === "blocker" || sev === "warning" || sev === "informational") {
      counts[sev] += 1;
    } else {
      counts.warning += 1;
    }
  }
  return counts;
}

function pickConclusion(counts) {
  // AC-5: blocker dominates → failure; warning-only → neutral; otherwise success.
  // Informational-only counts as success (auto-bypassed) per Scenario 1.
  if (counts.blocker > 0) return "failure";
  if (counts.warning > 0) return "neutral";
  return "success";
}

function buildTitle(counts) {
  const suffix = counts.informational > 0 ? " (auto-bypassed)" : "";
  return `${counts.blocker} blocker · ${counts.warning} warning · ${counts.informational} informational${suffix}`;
}

function buildSummary({ conclusion, title, commentUrl }) {
  const lines = [];
  lines.push(`**Verdict conclusion**: \`${conclusion}\``);
  lines.push("");
  lines.push(title);
  if (commentUrl) {
    lines.push("");
    lines.push(`See [release-gate bot comment](${commentUrl}) for the per-check classification table.`);
  }
  return lines.join("\n");
}

function escapeCell(value) {
  return String(value ?? "")
    .replace(/\|/g, "\\|")
    .replace(/\r?\n/g, " ");
}

function buildText(failures) {
  if (!failures || failures.length === 0) {
    return "_No failed checks classified — verdict synthesized from a clean run._";
  }

  const header = [
    "| Check | Severity | Owner | Override path | Notes |",
    "|---|---|---|---|---|",
  ];
  const rows = failures.map((f) => {
    const sevLabel = f.is_unknown ? "warning (was unknown)" : escapeCell(f.severity);
    const name = escapeCell(f.name);
    const owner = escapeCell(f.owner);
    const override = escapeCell(f.override_path);
    const notes = escapeCell(f.notes ?? "");
    return `| \`${name}\` | ${sevLabel} | ${owner} | ${override} | ${notes} |`;
  });

  let body = [...header, ...rows].join("\n");

  if (body.length > OUTPUT_TEXT_MAX) {
    // AC-9: truncate, preserve footer.
    body = body.slice(0, OUTPUT_TEXT_MAX - TRUNC_FOOTER.length) + TRUNC_FOOTER;
  }

  return body;
}

export function computeVerdict({ failures = [], gates = {}, commentUrl = null } = {}) {
  // AC-3: kill switch — when disabled, surface a shape callers can branch on
  // without calling the octokit Checks API.
  if (!isPhase2aEnabled(gates)) {
    return { enabled: false };
  }

  const counts = countByTier(failures);
  const conclusion = pickConclusion(counts);
  const title = buildTitle(counts);
  const summary = buildSummary({ conclusion, title, commentUrl });
  const text = buildText(failures);

  return {
    enabled: true,
    conclusion,
    title,
    summary,
    text,
    counts,
  };
}
