// Pure aggregation logic — builds the weekly Slack digest payload + the next
// state JSON from a list of release PRs (with classified failures attached)
// and the previous state.
// Tested by __tests__/digest-builder.test.mjs.

export const STATE_SCHEMA_VERSION = 1;

export const EMPTY_STATE = Object.freeze({
  schemaVersion: STATE_SCHEMA_VERSION,
  lastWeekISO: null,
  warningsByCheck: {},
});

// ISO 8601 week — "YYYY-Www" (e.g. "2026-W22").
// Implementation reference: https://en.wikipedia.org/wiki/ISO_week_date#Algorithms
export function isoWeek(date) {
  const d = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()));
  // Thursday of the current ISO week determines the year.
  const dayNum = d.getUTCDay() === 0 ? 7 : d.getUTCDay();
  d.setUTCDate(d.getUTCDate() + 4 - dayNum);
  const yearStart = new Date(Date.UTC(d.getUTCFullYear(), 0, 1));
  const weekNo = Math.ceil(((d - yearStart) / 86400000 + 1) / 7);
  const ww = String(weekNo).padStart(2, "0");
  return `${d.getUTCFullYear()}-W${ww}`;
}

function normalizePrevState(prevState) {
  if (
    !prevState ||
    typeof prevState !== "object" ||
    prevState.schemaVersion !== STATE_SCHEMA_VERSION ||
    typeof prevState.warningsByCheck !== "object"
  ) {
    return { ...EMPTY_STATE, warningsByCheck: {} };
  }
  return prevState;
}

function isPhase2cEnabled(gates) {
  if (gates?.bot?.phase2c?.enabled === false) return false;
  return true;
}

function escalationThreshold(gates) {
  const v = gates?.bot?.phase2c?.escalation_threshold_weeks;
  if (typeof v === "number" && Number.isInteger(v) && v > 0) return v;
  return 4;
}

// Format a Slack message body. Uses mrkdwn (not Block Kit) for simplicity — the
// CLI builds Block Kit JSON when posting.
function formatSlackMessage({ aggregates, escalations, deltas, weekISO }) {
  if (aggregates.length === 0) {
    return `:white_check_mark: No release-gate warnings this week (${weekISO}).`;
  }

  const lines = [];
  lines.push(`*Release-Gate Digest — ${weekISO}*`);
  lines.push(
    `${deltas.new_this_week} new · ${deltas.persisting} persisting · ${deltas.resolved_this_week} resolved`
  );
  lines.push("");

  const top = aggregates.slice(0, 3);
  for (const entry of top) {
    lines.push(
      `• \`${entry.check_name}\` × ${entry.frequency} — owner @${entry.owner}`
    );
  }

  if (escalations.length > 0) {
    lines.push("");
    for (const esc of escalations) {
      lines.push(
        `:warning: \`${esc.check_name}\` — persisting ${esc.weeks_seen} weeks, suggest reclassify to blocker`
      );
    }
  }

  return lines.join("\n");
}

/**
 * Build the digest from this week's release PRs and prior state.
 *
 * @param {Object} args
 * @param {Array<{number, title, mergedAt, baseRef, botFailures}>} args.prs
 *   Release PRs in the 7-day window. `botFailures` is the array returned by
 *   parse-bot-comment for the latest bot comment on each PR.
 * @param {Object} args.prevState - state JSON loaded from state/release-gate-digest.json
 * @param {Date} args.now - current time (frozen in tests)
 * @param {Object} args.gates - parsed .github/release-gates.yml object
 *
 * @returns {Object} - { action: 'post'|'skip', reason?, slackMessage?,
 *   aggregates, deltas, escalations, newState, openStatePr, statePrBranchName }
 */
export function buildDigest({ prs, prevState, now, gates }) {
  const weekISO = isoWeek(now);

  // 1. Kill switch (AC-6)
  if (!isPhase2cEnabled(gates)) {
    return {
      action: "skip",
      reason: "bot.phase2c.enabled=false",
      weekISO,
    };
  }

  // 2. Normalize prevState (graceful schema drift)
  const prev = normalizePrevState(prevState);

  // 3. Idempotency (AC-8 — same-week rerun)
  if (prev.lastWeekISO === weekISO) {
    return {
      action: "skip",
      reason: "already-processed-this-week",
      weekISO,
    };
  }

  // 4. Aggregate warning-tier failures across PRs
  const warningsByCheck = new Map();
  for (const pr of prs ?? []) {
    for (const failure of pr.botFailures ?? []) {
      if (failure.severity !== "warning") continue;
      const key = failure.check_name;
      if (!warningsByCheck.has(key)) {
        warningsByCheck.set(key, {
          check_name: failure.check_name,
          owner: failure.owner,
          frequency: 0,
        });
      }
      warningsByCheck.get(key).frequency += 1;
    }
  }

  const aggregates = Array.from(warningsByCheck.values()).sort(
    (a, b) => b.frequency - a.frequency
  );

  // 5. Delta tracking + new state construction
  const newWarnings = {};
  let newCount = 0;
  let persistingCount = 0;

  for (const entry of aggregates) {
    const existing = prev.warningsByCheck[entry.check_name];
    if (existing) {
      newWarnings[entry.check_name] = {
        owner: entry.owner,
        firstSeenWeek: existing.firstSeenWeek,
        weeksSeen: (existing.weeksSeen ?? 1) + 1,
        lastSeenWeek: weekISO,
      };
      persistingCount += 1;
    } else {
      newWarnings[entry.check_name] = {
        owner: entry.owner,
        firstSeenWeek: weekISO,
        weeksSeen: 1,
        lastSeenWeek: weekISO,
      };
      newCount += 1;
    }
  }

  // Resolved = present in prev state, absent in this week
  let resolvedCount = 0;
  for (const prevName of Object.keys(prev.warningsByCheck ?? {})) {
    if (!warningsByCheck.has(prevName)) {
      resolvedCount += 1;
    }
  }

  // 6. Escalations (AC-5)
  const threshold = escalationThreshold(gates);
  const escalations = [];
  for (const [name, entry] of Object.entries(newWarnings)) {
    if (entry.weeksSeen >= threshold) {
      escalations.push({
        check_name: name,
        owner: entry.owner,
        weeks_seen: entry.weeksSeen,
      });
    }
  }

  // 7. Build Slack message
  const deltas = {
    new_this_week: newCount,
    persisting: persistingCount,
    resolved_this_week: resolvedCount,
  };
  const slackMessage = formatSlackMessage({
    aggregates,
    escalations,
    deltas,
    weekISO,
  });

  // 8. New state + state PR decision
  const newState = {
    schemaVersion: STATE_SCHEMA_VERSION,
    lastWeekISO: weekISO,
    warningsByCheck: newWarnings,
  };
  const openStatePr = aggregates.length > 0;
  const statePrBranchName = `chore/release-gate-digest-state-${weekISO}`;

  return {
    action: "post",
    weekISO,
    slackMessage,
    aggregates,
    deltas,
    escalations,
    newState,
    openStatePr,
    statePrBranchName,
  };
}
