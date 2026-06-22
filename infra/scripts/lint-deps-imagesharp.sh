#!/usr/bin/env bash
# Issue #2055 Phase G AC-G2 — prevent regression to ImageSharp 3.x split license.
# ADR DEC-3d-1 (LOCKED 2026-06-20, docs/for-claude/architecture/adr/adr-2026-06-09-wikidata-enrichment-architecture.md):
# SixLabors.ImageSharp 3.x adopted the Six Labors Split License (commercial use
# requires a paid licence). MeepleAI is proprietary → incompatible. Replaced by
# Magick.NET-Q8-AnyCPU 14.x (Apache 2.0). This guard fails CI if any reference
# is re-introduced.
#
# Scope: scan ONLY .cs source + .csproj declarations. The guard:
#   - INCLUDES: src/.cs files (using directives, type references), .csproj
#               (active PackageReference lines).
#   - EXCLUDES: bin/, obj/ (build artifacts mirror the prior build's deps),
#               source comments (`//` or XML doc) that reference the historical
#               migration in narrative form, and .csproj comment lines (XML
#               <!-- comments --> are stripped before matching).
set -euo pipefail

# 1) Search source files (.cs) ignoring comment lines.
#    `^\s*//` excludes single-line C# comments. `^\s*///` excludes XML doc
#    comments. `^\s*\*` excludes multi-line block comment continuations.
#    Use -P perl regex so we get negative-lookahead-free filtering via grep -v.
src_violations=$(
  grep -rE "SixLabors\.ImageSharp" \
    apps/api/src apps/api/tests \
    --include="*.cs" \
    --exclude-dir=bin --exclude-dir=obj \
    2>/dev/null \
  | grep -vE ":\s*(//|///|\*)" \
  || true
)

# 2) Search .csproj files but skip XML comment lines (`<!-- ... -->`).
csproj_violations=$(
  grep -rE "SixLabors\.ImageSharp" \
    apps/api/src apps/api/tests \
    --include="*.csproj" \
    --exclude-dir=bin --exclude-dir=obj \
    2>/dev/null \
  | grep -vE ":\s*<!--" \
  || true
)

violations="${src_violations}${csproj_violations}"

if [ -n "${violations}" ]; then
  echo "ERROR: ImageSharp reference re-introduced (DEC-3d-1 violation, ADR Wikidata 2026-06-09)."
  echo "Use Magick.NET-Q8-AnyCPU instead."
  echo ""
  echo "Offending matches:"
  echo "${violations}"
  exit 1
fi
echo "OK: no ImageSharp references found."
