// Pure markdown formatters — bot comment + GH Actions summary.
// Tested by __tests__/format.test.mjs.

export const SIGNATURE_HEADER = "<!-- release-gate-bot:v1 -->";

function pickVerdict(failures) {
  if (failures.some((f) => f.severity === "blocker")) return "BLOCKER";
  if (failures.some((f) => f.severity === "warning")) return "WARNING";
  if (failures.some((f) => f.severity === "informational")) return "GREEN";
  return "GREEN";
}

function emojiFor(severity, gates) {
  const ve = gates?.bot?.verdict_emoji ?? {};
  return ve[severity] ?? "•";
}

function countBy(failures) {
  // unknown is mutually exclusive from severity buckets so the summary table
  // sums to total = blocker + warning + informational + unknown without double-counting.
  const counts = { blocker: 0, warning: 0, informational: 0, unknown: 0 };
  for (const f of failures) {
    if (f.is_unknown) {
      counts.unknown += 1;
    } else if (counts[f.severity] !== undefined) {
      counts[f.severity] += 1;
    }
  }
  return counts;
}

export function formatComment({ failures, meta, gates, error = null }) {
  const lines = [];
  lines.push(SIGNATURE_HEADER);
  lines.push("");
  lines.push("## Release-gate Classification");
  lines.push("");
  lines.push(`Commit \`${meta.commit_short ?? meta.commit_sha ?? "unknown"}\` · Generated ${meta.generated_at}`);
  lines.push("");

  if (error) {
    lines.push("⚠️ **release-gate bot failed — manual triage required.**");
    lines.push("");
    lines.push(`- Phase: \`${error.phase ?? "unknown"}\``);
    lines.push(`- Error: \`${error.message ?? "no message"}\``);
    lines.push("- Action: review `.github/release-gates.yml` syntax and bot logs.");
    lines.push("");
    return lines.join("\n");
  }

  if (failures.length === 0) {
    lines.push("✅ No failed checks classified.");
    return lines.join("\n");
  }

  const counts = countBy(failures);
  const verdict = pickVerdict(failures);

  lines.push(`**Verdict**: ${emojiFor(verdict.toLowerCase() === "green" ? "informational" : verdict.toLowerCase(), gates)} ${verdict}`);
  lines.push("");
  lines.push(`${counts.blocker} blocker · ${counts.warning} warning · ${counts.informational} informational`);
  lines.push("");

  lines.push("| Check | Severity | Owner | Override path | Notes |");
  lines.push("|---|---|---|---|---|");
  for (const f of failures) {
    const sev = `${emojiFor(f.severity, gates)} ${f.severity}`;
    const note = (f.notes ?? "").replace(/\|/g, "\\|");
    const tag = f.is_unknown ? " 🆕" : "";
    lines.push(`| \`${f.name}\`${tag} | ${sev} | ${f.owner} | ${f.override_path} | ${note} |`);
  }
  lines.push("");

  const unknownCount = counts.unknown;
  if (unknownCount > 0) {
    lines.push(`🆕 ${unknownCount} new check${unknownCount === 1 ? "" : "s"} detected — please update \`.github/release-gates.yml\`.`);
    lines.push("");
  }

  lines.push(`<sub>Classification duration: ${meta.duration_ms ?? "?"} ms · PR #${meta.pr_number ?? "?"}</sub>`);

  return lines.join("\n");
}

export function formatActionsSummary({ failures, total_checks, meta }) {
  const counts = countBy(failures);
  const verdict = pickVerdict(failures);

  const lines = [];
  lines.push("## Release-gate Classification Summary");
  lines.push("");
  lines.push("| Metric | Value |");
  lines.push("|---|---:|");
  lines.push(`| Total checks evaluated | ${total_checks} |`);
  lines.push(`| Blocker failures | ${counts.blocker} |`);
  lines.push(`| Warning failures | ${counts.warning} |`);
  lines.push(`| Informational failures | ${counts.informational} |`);
  lines.push(`| Unknown (new) checks | ${counts.unknown} |`);
  lines.push(`| Classification duration (ms) | ${meta.duration_ms ?? "?"} |`);
  lines.push("");
  lines.push(`**Verdict**: ${verdict}`);

  return lines.join("\n");
}
