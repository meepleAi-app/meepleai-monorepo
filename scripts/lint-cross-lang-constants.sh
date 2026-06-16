#!/usr/bin/env bash
# lint-cross-lang-constants.sh — ADR-075 short-term drift detection
#
# Auto-discovers paired cross-language constant files and verifies they encode
# the same keys. Targets the drift risk for constants that must be kept in
# sync between BE (C#) and FE (TypeScript) without a codegen pipeline.
#
# Discovery convention (Option A from #2383 brainstorm 2026-06-16):
#   BE: apps/api/src/Api/**/Constants/<Name>Routes.cs
#   FE: apps/web/src/lib/constants/<kebab-name>-routes.ts
#
# Example pair (when it lands):
#   apps/api/src/Api/BoundedContexts/UserNotifications/Application/Constants/NotificationRoutes.cs
#   apps/web/src/lib/constants/notification-routes.ts
#
# Matching strategy:
#   "FooBarRoutes.cs" <-> "foo-bar-routes.ts" (PascalCase -> kebab-case)
#
# Behaviour:
#   - Zero BE files OR zero FE files matching the glob -> exit 0 (no-op).
#   - File pair found -> extract the constant string keys from each side,
#     normalise (sort + lowercase), hash with sha256, compare.
#   - Mismatch -> exit 1 with a diff hint.
#
# Long-term: replaced by a JSON single-source-of-truth + quicktype.io codegen
# step. Tracked separately in the ADR-075 long-term tracker issue.
#
# Refs: docs/for-claude/architecture/adr/adr-075-deep-link-versioning.md
#       umbrella #2383, brainstorm session 2026-06-16

set -euo pipefail

BE_GLOB="apps/api/src/Api/**/Constants/*Routes.cs"
FE_DIR="apps/web/src/lib/constants"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# Enable globstar so ** matches across directories. Required for the BE glob.
shopt -s globstar nullglob

# ─── Helpers ─────────────────────────────────────────────────────────────────

pascal_to_kebab() {
  # PascalCase -> kebab-case. e.g. "NotificationRoutes" -> "notification-routes"
  local input="$1"
  echo "$input" \
    | sed -E 's/([a-z0-9])([A-Z])/\1-\2/g' \
    | sed -E 's/([A-Z])([A-Z][a-z])/\1-\2/g' \
    | tr '[:upper:]' '[:lower:]'
}

# Extract string-literal keys from a C# constants file. Looks for any
#   public const string Foo = "bar";
# or
#   public static readonly string Foo = "bar";
# pattern and emits the right-hand-side string value.
extract_cs_keys() {
  local file="$1"
  grep -oE 'public\s+(const|static\s+readonly)\s+string\s+[A-Za-z0-9_]+\s*=\s*"[^"]*"' "$file" \
    | sed -E 's/.*=\s*"([^"]*)"/\1/' \
    | sort -u
}

# Extract string-literal values from a TypeScript constants file. Looks for any
#   export const Foo = 'bar';
# or
#   export const Foo = "bar";
# or
#   Foo: 'bar',  // inside an object literal
# pattern and emits the right-hand-side string value.
extract_ts_keys() {
  local file="$1"
  # Two passes: top-level `export const X = '...';` AND object-property `X: '...',`
  {
    grep -oE "export\s+const\s+[A-Za-z0-9_]+\s*=\s*['\"][^'\"]+['\"]" "$file" \
      | sed -E "s/.*=\s*['\"]([^'\"]+)['\"]/\1/"
    grep -oE "[A-Za-z0-9_]+\s*:\s*['\"][^'\"]+['\"]" "$file" \
      | sed -E "s/.*:\s*['\"]([^'\"]+)['\"]/\1/"
  } | sort -u
}

hash_keys() {
  # Normalise: trim, lowercase, sort, hash. sha256sum prints "<hash>  -" so
  # cut the first field.
  tr '[:upper:]' '[:lower:]' | sort -u | sha256sum | cut -d ' ' -f 1
}

# ─── Main ────────────────────────────────────────────────────────────────────

mapfile -t be_files < <(compgen -G "$BE_GLOB" 2>/dev/null || true)

if [[ ${#be_files[@]} -eq 0 ]]; then
  echo "[lint-cross-lang-constants] No BE constants files matching '$BE_GLOB' — skipping (no-op)."
  exit 0
fi

if [[ ! -d "$FE_DIR" ]]; then
  echo "[lint-cross-lang-constants] FE directory '$FE_DIR' missing — skipping (no-op)."
  exit 0
fi

declare -i pairs_checked=0
declare -i mismatches=0

for be_file in "${be_files[@]}"; do
  be_basename="$(basename "$be_file" .cs)"            # e.g. NotificationRoutes
  fe_basename="$(pascal_to_kebab "$be_basename")"      # e.g. notification-routes
  fe_file="${FE_DIR}/${fe_basename}.ts"

  if [[ ! -f "$fe_file" ]]; then
    echo "[lint-cross-lang-constants] WARN: BE '$be_file' has no matching FE pair at '$fe_file' — skipping."
    continue
  fi

  pairs_checked+=1

  be_hash="$(extract_cs_keys "$be_file" | hash_keys)"
  fe_hash="$(extract_ts_keys "$fe_file" | hash_keys)"

  if [[ "$be_hash" != "$fe_hash" ]]; then
    mismatches+=1
    echo "[lint-cross-lang-constants] FAIL: drift detected for $be_basename"
    echo "  BE keys ($be_file):"
    extract_cs_keys "$be_file" | sed 's/^/    /'
    echo "  FE keys ($fe_file):"
    extract_ts_keys "$fe_file" | sed 's/^/    /'
    echo "  Diff (BE only / FE only):"
    diff <(extract_cs_keys "$be_file") <(extract_ts_keys "$fe_file") | sed 's/^/    /' || true
  else
    echo "[lint-cross-lang-constants] OK: $be_basename matches ($be_file <-> $fe_file)"
  fi
done

if [[ $pairs_checked -eq 0 ]]; then
  echo "[lint-cross-lang-constants] No BE/FE pairs to check — exiting clean (no-op)."
  exit 0
fi

if [[ $mismatches -gt 0 ]]; then
  echo "[lint-cross-lang-constants] FAILED: $mismatches of $pairs_checked pair(s) drift."
  exit 1
fi

echo "[lint-cross-lang-constants] All $pairs_checked pair(s) consistent."
