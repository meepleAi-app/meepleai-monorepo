/**
 * drop-v2-prefix.ts — Stage 2 codemod for design-system de-versioning (#1025).
 *
 * Reads the Stage 1 audit JSON, computes source→target rename map + delete set,
 * detects collisions, and (in --apply mode) executes filesystem moves +
 * ts-morph import-statement rewrites across apps/web/src/.
 *
 * Modes:
 *   (default)    dry-run — no filesystem mutation, emits dry-run report only.
 *   --apply      execute renames, deletes, and import rewrites.
 *   --reverse    invert the rename map (rollback). Combine with --apply to apply.
 *
 * Outputs (always written, even in dry-run):
 *   docs/for-developers/audits/2026-05-11-codemod-dry-run.md
 *   docs/for-developers/audits/2026-05-11-codemod-dry-run.json
 *
 * Run from repo root or scripts/codemod/:
 *   npx tsx scripts/codemod/drop-v2-prefix.ts
 *   pnpm --filter @meepleai/codemod codemod
 */

import { promises as fs } from "node:fs";
import * as path from "node:path";
import { fileURLToPath } from "node:url";
import { Project, SyntaxKind } from "ts-morph";
import pc from "picocolors";

// ---------------------------------------------------------------------------
// Repo layout constants
// ---------------------------------------------------------------------------

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
// scripts/codemod/ → repo root is two levels up
const REPO_ROOT = path.resolve(__dirname, "..", "..");

const AUDIT_JSON = path.join(
  REPO_ROOT,
  "docs",
  "for-developers",
  "audits",
  "2026-05-11-mockup-conformity.json"
);
const REPORT_MD = path.join(
  REPO_ROOT,
  "docs",
  "for-developers",
  "audits",
  "2026-05-11-codemod-dry-run.md"
);
const REPORT_JSON = path.join(
  REPO_ROOT,
  "docs",
  "for-developers",
  "audits",
  "2026-05-11-codemod-dry-run.json"
);

const WEB_SRC = path.join(REPO_ROOT, "apps", "web", "src");
const WEB_TSCONFIG = path.join(REPO_ROOT, "apps", "web", "tsconfig.json");

// Known name-collision watchlist from the human MD audit (Stage 2 blockers).
// These are NOT filesystem collisions — they are duplicate filenames across
// different feature clusters. Recorded for human review, not script blocking.
const NAME_COLLISION_WATCHLIST = [
  "btn",
  "drawer",
  "input-field",
  "toggle-switch",
  "entity-card",
  "ConfidenceBadge",
  "TypingIndicator",
  "StepIndicator",
  "step-progress",
  "SessionDiaryTimeline",
  "ConnectionBar",
];

// ---------------------------------------------------------------------------
// Audit JSON types
// ---------------------------------------------------------------------------

interface AuditEntry {
  path: string;
  mockup_ref: string;
  cluster: string;
  outcome: string;
  action: string;
  diff_summary: string;
  target_path: string | null;
  notes: string | null;
}

interface AuditFile {
  audit_date: string;
  stage: number;
  components: AuditEntry[];
}

// ---------------------------------------------------------------------------
// Plan types
// ---------------------------------------------------------------------------

interface RenamePlan {
  source: string; // repo-relative POSIX path
  target: string;
  cluster: string;
  isDirectory: boolean;
}

interface DeletePlan {
  source: string;
  cluster: string;
  reason: string;
}

interface Collision {
  kind:
    | "target-exists" // file/dir already exists at target outside source
    | "duplicate-target" // two source entries mapped to same target
    | "name-watchlist" // matches the audit's known-collision name list
    | "source-missing"; // audit refers to a path that does not exist
  source?: string;
  target?: string;
  detail: string;
  severity: "blocker" | "warning" | "info";
  recommendation: string;
}

interface ImportRewritePreview {
  file: string;
  oldSpecifier: string;
  newSpecifier: string;
}

// ---------------------------------------------------------------------------
// CLI parsing
// ---------------------------------------------------------------------------

const args = new Set(process.argv.slice(2));
const APPLY = args.has("--apply");
const REVERSE = args.has("--reverse");
const VERBOSE = args.has("--verbose") || args.has("-v");

// ---------------------------------------------------------------------------
// Path helpers — always emit POSIX-style relative paths in reports
// ---------------------------------------------------------------------------

function toPosix(p: string): string {
  return p.split(path.sep).join("/");
}

function repoRel(absPath: string): string {
  return toPosix(path.relative(REPO_ROOT, absPath));
}

function absFromRepoRel(rel: string): string {
  return path.join(REPO_ROOT, rel);
}

// ---------------------------------------------------------------------------
// Audit ingestion
// ---------------------------------------------------------------------------

async function loadAudit(): Promise<AuditFile> {
  const raw = await fs.readFile(AUDIT_JSON, "utf8");
  return JSON.parse(raw) as AuditFile;
}

// ---------------------------------------------------------------------------
// Filesystem sweep — capture files in v2/ tree NOT enumerated in audit
// ---------------------------------------------------------------------------
//
// Stage 1 audit enumerated mockup-faithful components only. The v2/ tree also
// contains co-located tests, barrel index.ts files, helper modules (e.g.
// entity-tokens.ts), and stubs missed by the audit. Without sweep, Phase B
// leaves ~192 files orphaned. Sweep walks the v2/ tree, derives canonical
// targets from path structure, and merges into the rename plan.

const V2_ROOTS = [
  path.join(WEB_SRC, "components", "v2"),
  path.join(WEB_SRC, "components", "ui", "v2"),
];

async function walkDir(dir: string): Promise<string[]> {
  const out: string[] = [];
  let entries: import("node:fs").Dirent[];
  try {
    entries = (await fs.readdir(dir, { withFileTypes: true })) as unknown as import("node:fs").Dirent[];
  } catch {
    return out;
  }
  for (const e of entries) {
    const full = path.join(dir, e.name);
    if (e.isDirectory()) {
      out.push(...(await walkDir(full)));
    } else if (e.isFile()) {
      out.push(full);
    }
  }
  return out;
}

function deriveSweptTarget(absPath: string): string | null {
  // Map components/v2/<rest> → components/features/<rest>
  // Map components/ui/v2/<rest> → components/ui/<rest>
  const rel = toPosix(path.relative(WEB_SRC, absPath));
  if (rel.startsWith("components/ui/v2/")) {
    const after = rel.slice("components/ui/v2/".length);
    return path.join(WEB_SRC, "components", "ui", ...after.split("/"));
  }
  if (rel.startsWith("components/v2/")) {
    const after = rel.slice("components/v2/".length);
    return path.join(WEB_SRC, "components", "features", ...after.split("/"));
  }
  return null;
}

async function sweepFilesystem(
  audit: AuditFile,
  existingRenames: RenamePlan[]
): Promise<RenamePlan[]> {
  // Index of file paths already handled by audit (rename source OR delete source)
  const handled = new Set<string>();
  // Index of DIRECTORY prefixes covered by audit dir-renames (path ending in /).
  // Files inside these dirs must be skipped — the dir-rename moves them atomically.
  const dirPrefixes: string[] = [];
  for (const e of audit.components) {
    if (e.action === "rename-stage-2" || e.action === "delete-stage-2") {
      const p = toPosix(e.path);
      if (p.endsWith("/")) {
        dirPrefixes.push(p);
      } else {
        handled.add(p);
      }
    }
  }
  for (const r of existingRenames) {
    const s = toPosix(r.source);
    if (r.isDirectory || s.endsWith("/")) {
      dirPrefixes.push(s.endsWith("/") ? s : s + "/");
    } else {
      handled.add(s);
    }
  }

  const swept: RenamePlan[] = [];
  for (const root of V2_ROOTS) {
    const files = await walkDir(root);
    for (const abs of files) {
      const sourceRel = toPosix(path.relative(REPO_ROOT, abs));
      if (handled.has(sourceRel)) continue;
      // Skip files inside an audit dir-rename (moved atomically with parent).
      if (dirPrefixes.some((p) => sourceRel.startsWith(p))) continue;

      const targetAbs = deriveSweptTarget(abs);
      if (!targetAbs) continue;
      const targetRel = toPosix(path.relative(REPO_ROOT, targetAbs));

      // Derive cluster from path: features/<cluster>/... or ui/<primitive>/...
      const segs = targetRel.split("/");
      const cluster = segs[3] ?? "unknown";

      swept.push({
        source: sourceRel,
        target: targetRel,
        cluster: `sweep:${cluster}`,
        isDirectory: false,
      });
    }
  }
  return swept;
}

function buildPlans(audit: AuditFile): {
  renames: RenamePlan[];
  deletes: DeletePlan[];
} {
  const renames: RenamePlan[] = [];
  const deletes: DeletePlan[] = [];

  for (const entry of audit.components) {
    if (entry.action === "rename-stage-2") {
      if (!entry.target_path) {
        // Defensive: rename without target is a data error.
        continue;
      }
      const isDirectory =
        entry.path.endsWith("/") || entry.target_path.endsWith("/");
      renames.push({
        source: toPosix(entry.path),
        target: toPosix(entry.target_path),
        cluster: entry.cluster,
        isDirectory,
      });
    } else if (entry.action === "delete-stage-2") {
      deletes.push({
        source: toPosix(entry.path),
        cluster: entry.cluster,
        reason: entry.diff_summary ?? "orphan stub",
      });
    }
  }

  return { renames, deletes };
}

// ---------------------------------------------------------------------------
// Collision detection
// ---------------------------------------------------------------------------

async function pathExists(p: string): Promise<boolean> {
  try {
    await fs.access(p);
    return true;
  } catch {
    return false;
  }
}

async function detectCollisions(
  renames: RenamePlan[],
  deletes: DeletePlan[]
): Promise<Collision[]> {
  const collisions: Collision[] = [];

  // 1. Source-missing — defensive sanity check against the audit
  for (const r of renames) {
    if (!(await pathExists(absFromRepoRel(r.source)))) {
      collisions.push({
        kind: "source-missing",
        source: r.source,
        target: r.target,
        detail: `Audit lists source path '${r.source}' but it does not exist on disk.`,
        severity: "blocker",
        recommendation:
          "Re-run Stage 1 audit OR remove this entry from the audit JSON.",
      });
    }
  }
  for (const d of deletes) {
    if (!(await pathExists(absFromRepoRel(d.source)))) {
      collisions.push({
        kind: "source-missing",
        source: d.source,
        detail: `Audit lists delete source '${d.source}' but it does not exist on disk.`,
        severity: "info",
        recommendation:
          "Already cleaned up; remove from audit JSON or treat as no-op.",
      });
    }
  }

  // 2. Target-exists — a file/dir at the target path that is NOT the source itself
  for (const r of renames) {
    const absSrc = absFromRepoRel(r.source);
    const absTgt = absFromRepoRel(r.target);
    if (absSrc === absTgt) continue;
    if (await pathExists(absTgt)) {
      collisions.push({
        kind: "target-exists",
        source: r.source,
        target: r.target,
        detail: `Target '${r.target}' already exists. Rename would overwrite or fail.`,
        severity: "blocker",
        recommendation:
          "Inspect the existing target. Decide: (a) merge content, (b) rename target out of the way, (c) drop the rename.",
      });
    }
  }

  // 3. Duplicate-target — two source entries mapping to the same target
  const byTarget = new Map<string, RenamePlan[]>();
  for (const r of renames) {
    const arr = byTarget.get(r.target) ?? [];
    arr.push(r);
    byTarget.set(r.target, arr);
  }
  for (const [target, group] of byTarget) {
    if (group.length > 1) {
      collisions.push({
        kind: "duplicate-target",
        target,
        detail: `Multiple sources map to '${target}': ${group
          .map((g) => g.source)
          .join(", ")}`,
        severity: "blocker",
        recommendation:
          "Adjust audit target_path so each rename is unique, or consolidate the duplicate components before Phase B.",
      });
    }
  }

  // 4. Name-watchlist — informational only; same basename across clusters
  const watch = new Set(
    NAME_COLLISION_WATCHLIST.map((n) => n.toLowerCase())
  );
  for (const r of renames) {
    const base = path.basename(r.target, path.extname(r.target));
    const dirLeaf = path.basename(path.dirname(r.target));
    const candidates = [base.toLowerCase(), dirLeaf.toLowerCase()];
    if (candidates.some((c) => watch.has(c))) {
      collisions.push({
        kind: "name-watchlist",
        source: r.source,
        target: r.target,
        detail: `Basename '${base}' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters.`,
        severity: "warning",
        recommendation:
          "After rename, audit consumer imports for ambiguous re-exports or aliasing.",
      });
    }
  }

  return collisions;
}

// ---------------------------------------------------------------------------
// Import rewrite planning (ts-morph)
// ---------------------------------------------------------------------------

interface ImportPlan {
  rewrites: ImportRewritePreview[];
  filesTouched: Set<string>;
}

/**
 * Build mapping of repo-relative source dir/file → target dir/file (no extension)
 * suitable for matching import specifiers. We map both:
 *   - `components/v2/<cluster>/<File>`   (relative or alias suffix)
 *   - `components/ui/v2/<primitive>/...` → `components/ui/<primitive>/...`
 *
 * Specifiers may omit `.tsx`/`.ts` AND may point to a directory (index.ts).
 */
function buildSpecifierRewrites(renames: RenamePlan[]): Map<string, string> {
  const map = new Map<string, string>();
  for (const r of renames) {
    // Strip extension if any
    const srcNoExt = r.source.replace(/\.(tsx?|jsx?)$/i, "");
    const tgtNoExt = r.target.replace(/\.(tsx?|jsx?)$/i, "");

    // Strip "apps/web/src/" prefix so we match what `@/...` aliases resolve to,
    // and ALSO what relative imports resolve to after ts-morph normalisation.
    const srcAfterSrc = srcNoExt.replace(/^apps\/web\/src\//, "");
    const tgtAfterSrc = tgtNoExt.replace(/^apps\/web\/src\//, "");

    map.set(srcAfterSrc, tgtAfterSrc);

    // Also map the directory form (used when imports target a folder index.ts)
    if (r.isDirectory) {
      const srcDir = srcAfterSrc.replace(/\/$/, "");
      const tgtDir = tgtAfterSrc.replace(/\/$/, "");
      map.set(srcDir, tgtDir);
    }
  }
  return map;
}

/**
 * Resolve a relative import specifier against a source file location to its
 * canonical apps/web/src/-relative path. Returns null if not resolvable inside
 * apps/web/src.
 */
function resolveImportToSrcRel(
  importerAbsPath: string,
  specifier: string
): string | null {
  let absImport: string;
  if (specifier.startsWith("@/")) {
    absImport = path.join(WEB_SRC, specifier.slice(2));
  } else if (specifier.startsWith(".")) {
    absImport = path.resolve(path.dirname(importerAbsPath), specifier);
  } else {
    return null;
  }
  const rel = path.relative(WEB_SRC, absImport);
  if (rel.startsWith("..")) return null;
  return toPosix(rel);
}

async function planImportRewrites(
  renames: RenamePlan[]
): Promise<ImportPlan> {
  const rewriteMap = buildSpecifierRewrites(renames);
  const project = new Project({
    tsConfigFilePath: WEB_TSCONFIG,
    skipAddingFilesFromTsConfig: false,
  });

  const rewrites: ImportRewritePreview[] = [];
  const filesTouched = new Set<string>();

  // ts-morph normalises file paths with forward slashes regardless of OS.
  const WEB_SRC_POSIX = toPosix(WEB_SRC);
  for (const sf of project.getSourceFiles()) {
    const filePath = sf.getFilePath(); // already POSIX-style
    if (!filePath.startsWith(WEB_SRC_POSIX + "/")) {
      continue;
    }

    const decls = [
      ...sf.getImportDeclarations(),
      ...sf.getExportDeclarations().filter((d) => d.getModuleSpecifier()),
    ];

    for (const decl of decls) {
      const spec = decl.getModuleSpecifierValue();
      if (!spec) continue;
      // Only consider relative or @/ imports
      if (!spec.startsWith(".") && !spec.startsWith("@/")) continue;

      const resolvedSrcRel = resolveImportToSrcRel(filePath, spec);
      if (!resolvedSrcRel) continue;

      // Try direct match first, then progressively trim file segments
      // (importer might have written `.../game-chat` to hit an index.ts).
      let mapped: string | null = null;
      let matchedKey: string | null = null;

      if (rewriteMap.has(resolvedSrcRel)) {
        mapped = rewriteMap.get(resolvedSrcRel)!;
        matchedKey = resolvedSrcRel;
      } else {
        // Walk up parents — handles imports that point at a folder
        let probe = resolvedSrcRel;
        while (probe.includes("/")) {
          probe = probe.substring(0, probe.lastIndexOf("/"));
          if (rewriteMap.has(probe)) {
            mapped = rewriteMap.get(probe)!;
            matchedKey = probe;
            break;
          }
        }
      }

      if (!mapped || !matchedKey) continue;

      // Substitute the matched prefix in the resolved path, then re-encode
      // as @/... (stable across importers) for the new specifier.
      const newSrcRel =
        resolvedSrcRel === matchedKey
          ? mapped
          : mapped + resolvedSrcRel.substring(matchedKey.length);

      const newSpecifier = `@/${newSrcRel}`;

      rewrites.push({
        file: repoRel(filePath),
        oldSpecifier: spec,
        newSpecifier,
      });
      filesTouched.add(repoRel(filePath));

      if (APPLY) {
        decl.setModuleSpecifier(newSpecifier);
      }
    }
  }

  if (APPLY) {
    await project.save();
  }

  return { rewrites, filesTouched };
}

// ---------------------------------------------------------------------------
// Apply mode — filesystem operations
// ---------------------------------------------------------------------------

async function ensureDir(p: string): Promise<void> {
  await fs.mkdir(p, { recursive: true });
}

async function applyMoves(renames: RenamePlan[]): Promise<void> {
  for (const r of renames) {
    const absSrc = absFromRepoRel(r.source);
    const absTgt = absFromRepoRel(r.target);
    if (absSrc === absTgt) continue;
    await ensureDir(path.dirname(absTgt));
    await fs.rename(absSrc, absTgt);
  }
}

async function applyDeletes(deletes: DeletePlan[]): Promise<void> {
  for (const d of deletes) {
    const abs = absFromRepoRel(d.source);
    if (await pathExists(abs)) {
      const stat = await fs.stat(abs);
      if (stat.isDirectory()) {
        await fs.rm(abs, { recursive: true, force: true });
      } else {
        await fs.unlink(abs);
      }
    }
  }
}

// ---------------------------------------------------------------------------
// Reverse mode
// ---------------------------------------------------------------------------

function reversePlans(plans: {
  renames: RenamePlan[];
  deletes: DeletePlan[];
}): { renames: RenamePlan[]; deletes: DeletePlan[] } {
  const reversedRenames: RenamePlan[] = plans.renames.map((r) => ({
    source: r.target,
    target: r.source,
    cluster: r.cluster,
    isDirectory: r.isDirectory,
  }));
  // Cannot undo deletes from git-clean history; skip and warn.
  return { renames: reversedRenames, deletes: [] };
}

// ---------------------------------------------------------------------------
// Report writers
// ---------------------------------------------------------------------------

interface ReportPayload {
  generated_at: string;
  mode: "dry-run" | "apply" | "reverse-dry-run" | "reverse-apply";
  audit_source: string;
  counts: {
    rename_planned: number;
    delete_planned: number;
    collisions_total: number;
    collisions_blocker: number;
    collisions_warning: number;
    collisions_info: number;
    imports_rewritten: number;
    files_touched_by_imports: number;
  };
  verdict: "safe-to-apply" | "pending-resolution";
  renames: { source: string; target: string; cluster: string }[];
  deletes: { source: string; cluster: string; reason: string }[];
  collisions: Collision[];
  imports_sample: ImportRewritePreview[];
}

function chooseMode(): ReportPayload["mode"] {
  if (REVERSE && APPLY) return "reverse-apply";
  if (REVERSE) return "reverse-dry-run";
  if (APPLY) return "apply";
  return "dry-run";
}

async function writeJsonReport(payload: ReportPayload): Promise<void> {
  await fs.writeFile(REPORT_JSON, JSON.stringify(payload, null, 2) + "\n", "utf8");
}

async function writeMdReport(payload: ReportPayload): Promise<void> {
  const lines: string[] = [];
  lines.push(`# Stage 2 codemod dry-run report`);
  lines.push("");
  lines.push(
    `- **Generated**: ${payload.generated_at}`,
    `- **Mode**: \`${payload.mode}\``,
    `- **Audit source**: \`${payload.audit_source}\``,
    `- **Spec ref**: docs/for-developers/specs/2026-05-11-design-system-deversioning.md §3`,
    `- **Issue**: #1025 (umbrella #1023)`
  );
  lines.push("");
  lines.push(`## Counts`);
  lines.push("");
  lines.push(`| Metric | Count |`);
  lines.push(`| --- | --- |`);
  lines.push(`| Renames planned | ${payload.counts.rename_planned} |`);
  lines.push(`| Deletes planned | ${payload.counts.delete_planned} |`);
  lines.push(`| Collisions total | ${payload.counts.collisions_total} |`);
  lines.push(`| Collisions blocker | ${payload.counts.collisions_blocker} |`);
  lines.push(`| Collisions warning | ${payload.counts.collisions_warning} |`);
  lines.push(`| Collisions info | ${payload.counts.collisions_info} |`);
  lines.push(`| Import statements rewritten | ${payload.counts.imports_rewritten} |`);
  lines.push(`| Files touched by import fix | ${payload.counts.files_touched_by_imports} |`);
  lines.push("");

  // Verdict
  lines.push(`## Verdict`);
  lines.push("");
  if (payload.verdict === "safe-to-apply") {
    lines.push(
      `**safe-to-apply** — zero blocker collisions detected. Phase B may proceed.`
    );
  } else {
    lines.push(
      `**pending-resolution** — ${payload.counts.collisions_blocker} blocker collision(s) require manual resolution before \`--apply\`.`
    );
  }
  lines.push("");

  // Collisions
  if (payload.collisions.length > 0) {
    lines.push(`## Collisions`);
    lines.push("");
    const byKind = new Map<string, Collision[]>();
    for (const c of payload.collisions) {
      const arr = byKind.get(c.kind) ?? [];
      arr.push(c);
      byKind.set(c.kind, arr);
    }
    for (const [kind, group] of byKind) {
      lines.push(`### ${kind} (${group.length})`);
      lines.push("");
      lines.push(`| Severity | Source | Target | Detail | Recommendation |`);
      lines.push(`| --- | --- | --- | --- | --- |`);
      for (const c of group) {
        lines.push(
          `| ${c.severity} | \`${c.source ?? "—"}\` | \`${c.target ?? "—"}\` | ${escapePipes(
            c.detail
          )} | ${escapePipes(c.recommendation)} |`
        );
      }
      lines.push("");
    }
  } else {
    lines.push(`## Collisions`);
    lines.push("");
    lines.push(`No collisions detected.`);
    lines.push("");
  }

  // Deletes
  lines.push(`## Deletes planned (${payload.deletes.length})`);
  lines.push("");
  if (payload.deletes.length > 0) {
    lines.push(`| Cluster | Source | Reason |`);
    lines.push(`| --- | --- | --- |`);
    for (const d of payload.deletes) {
      lines.push(`| ${d.cluster} | \`${d.source}\` | ${escapePipes(d.reason)} |`);
    }
  } else {
    lines.push("_None._");
  }
  lines.push("");

  // Renames (truncated sample to keep MD readable)
  lines.push(`## Renames planned (${payload.renames.length})`);
  lines.push("");
  lines.push(`Full list is in the JSON sibling. Sample (first 20):`);
  lines.push("");
  lines.push(`| Cluster | Source | Target |`);
  lines.push(`| --- | --- | --- |`);
  for (const r of payload.renames.slice(0, 20)) {
    lines.push(`| ${r.cluster} | \`${r.source}\` | \`${r.target}\` |`);
  }
  if (payload.renames.length > 20) {
    lines.push(`| … | _${payload.renames.length - 20} more in JSON_ | … |`);
  }
  lines.push("");

  // Import rewrite sample
  lines.push(`## Import rewrites — sample`);
  lines.push("");
  if (payload.imports_sample.length > 0) {
    lines.push(`First 15 of ${payload.counts.imports_rewritten}:`);
    lines.push("");
    lines.push(`| File | Old specifier | New specifier |`);
    lines.push(`| --- | --- | --- |`);
    for (const ir of payload.imports_sample.slice(0, 15)) {
      lines.push(`| \`${ir.file}\` | \`${ir.oldSpecifier}\` | \`${ir.newSpecifier}\` |`);
    }
  } else {
    lines.push("_No import rewrites planned. Either no consumers reference v2 paths, or planner found no matches — please double-check before applying._");
  }
  lines.push("");

  // Footer
  lines.push(`---`);
  lines.push("");
  lines.push(
    `Generated by \`scripts/codemod/drop-v2-prefix.ts\`. Re-run with \`--verbose\` for debug output. Phase B applies with \`--apply\`.`
  );
  lines.push("");

  await fs.writeFile(REPORT_MD, lines.join("\n"), "utf8");
}

function escapePipes(s: string): string {
  return s.replace(/\|/g, "\\|").replace(/\n/g, " ");
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

async function main(): Promise<number> {
  console.log(pc.bold(`drop-v2-prefix.ts — Stage 2 codemod`));
  console.log(`Repo root: ${REPO_ROOT}`);
  console.log(`Mode:      ${chooseMode()}`);
  console.log("");

  const audit = await loadAudit();
  let plans = buildPlans(audit);

  // Filesystem sweep — capture co-located tests, barrels, helpers not in audit.
  //
  // ⚠️ --reverse limitation: sweep is skipped in reverse mode. This means
  // `--reverse --apply` only inverts the audit-derived renames (130), NOT
  // the ~192 swept files. For a full Stage 2 rollback prefer:
  //   git checkout <commit-before-stage-2> -- apps/web/
  //   git clean -fd apps/web/
  // The codemod's --reverse is intended for partial debugging only, not
  // production rollback of an applied Stage 2 PR.
  if (!REVERSE) {
    const swept = await sweepFilesystem(audit, plans.renames);
    plans.renames.push(...swept);
    console.log(
      `Audit: ${plans.renames.length - swept.length} renames + ${
        plans.deletes.length
      } deletes (audit) + ${swept.length} swept files`
    );
  } else {
    plans = reversePlans(plans);
    console.log(
      `Audit (reversed): ${plans.renames.length} renames + ${plans.deletes.length} deletes`
    );
  }

  const collisions = await detectCollisions(plans.renames, plans.deletes);
  const blocker = collisions.filter((c) => c.severity === "blocker").length;
  const warning = collisions.filter((c) => c.severity === "warning").length;
  const info = collisions.filter((c) => c.severity === "info").length;

  console.log(
    `Collisions: ${pc.red(`${blocker} blocker`)}, ${pc.yellow(
      `${warning} warning`
    )}, ${pc.dim(`${info} info`)}`
  );

  // Import rewrite planning runs regardless of mode so the report is complete.
  const importPlan = await planImportRewrites(plans.renames);
  console.log(
    `Import rewrites: ${importPlan.rewrites.length} specifiers across ${importPlan.filesTouched.size} files`
  );

  const verdict: ReportPayload["verdict"] =
    blocker === 0 ? "safe-to-apply" : "pending-resolution";

  const payload: ReportPayload = {
    generated_at: new Date().toISOString(),
    mode: chooseMode(),
    audit_source: repoRel(AUDIT_JSON),
    counts: {
      rename_planned: plans.renames.length,
      delete_planned: plans.deletes.length,
      collisions_total: collisions.length,
      collisions_blocker: blocker,
      collisions_warning: warning,
      collisions_info: info,
      imports_rewritten: importPlan.rewrites.length,
      files_touched_by_imports: importPlan.filesTouched.size,
    },
    verdict,
    renames: plans.renames.map(({ source, target, cluster }) => ({
      source,
      target,
      cluster,
    })),
    deletes: plans.deletes,
    collisions,
    imports_sample: importPlan.rewrites.slice(0, 50),
  };

  await writeJsonReport(payload);
  await writeMdReport(payload);
  console.log("");
  console.log(`Report MD:   ${repoRel(REPORT_MD)}`);
  console.log(`Report JSON: ${repoRel(REPORT_JSON)}`);
  console.log("");

  if (!APPLY) {
    console.log(pc.cyan(`[dry-run] no files modified. Re-run with --apply to execute.`));
    if (verdict === "pending-resolution") {
      console.log(
        pc.red(
          `[dry-run] verdict pending-resolution — refuse to --apply until ${blocker} blocker(s) resolved.`
        )
      );
    }
    return 0;
  }

  if (verdict === "pending-resolution") {
    console.error(
      pc.red(
        `[apply] aborting: ${blocker} blocker collision(s) detected. Resolve them before retrying --apply.`
      )
    );
    return 2;
  }

  console.log(pc.green(`[apply] executing renames + deletes...`));
  await applyMoves(plans.renames);
  await applyDeletes(plans.deletes);
  console.log(pc.green(`[apply] done. Import rewrites were saved during planning.`));
  return 0;
}

main().then(
  (code) => process.exit(code),
  (err) => {
    console.error(err);
    process.exit(1);
  }
);
