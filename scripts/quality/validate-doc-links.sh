#!/bin/bash
#
# ⚠️  DEPRECATED: This bash script is deprecated.
# ✅  USE: scripts/quality/validate-doc-links.ps1 (PowerShell)
#
# PowerShell version works on Windows, Linux, and macOS with PowerShell Core.
# See scripts/MIGRATION.md for migration guide.
#
# Documentation Link Validator
# Tests all markdown links in INDEX.md and README.md

set -e

# Dynamic path resolution (works from any directory)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$(dirname "$SCRIPT_DIR")")"
DOCS_DIR="$PROJECT_ROOT/docs"
ERRORS=0

echo "🔍 Validating documentation links..."
echo ""

# Function to validate a link
validate_link() {
    local file=$1
    local link=$2
    local base_dir=$(dirname "$file")

    # Skip external URLs
    if [[ $link =~ ^https?:// ]]; then
        return 0
    fi

    # Skip anchors
    if [[ $link =~ ^# ]]; then
        return 0
    fi

    # Construct full path
    if [[ $link =~ ^\.\. ]]; then
        # Parent directory
        full_path="$base_dir/$link"
    elif [[ $link =~ ^\. ]]; then
        # Current directory
        full_path="$base_dir/$link"
    else
        # Relative to base
        full_path="$base_dir/$link"
    fi

    # Normalize path
    full_path=$(realpath -m "$full_path" 2>/dev/null || echo "$full_path")

    # Check if file/directory exists
    if [ ! -e "$full_path" ]; then
        echo "❌ BROKEN LINK in $file:"
        echo "   Link: $link"
        echo "   Expected: $full_path"
        ((ERRORS++))
        return 1
    fi

    return 0
}

# Validate INDEX.md
echo "📄 Validating INDEX.md..."
if [ -f "$DOCS_DIR/INDEX.md" ]; then
    while IFS= read -r link; do
        validate_link "$DOCS_DIR/INDEX.md" "$link"
    done < <(grep -oP '\[.*?\]\(\K[^)#]+' "$DOCS_DIR/INDEX.md" 2>/dev/null || true)
fi

echo ""

# Validate README.md
echo "📄 Validating README.md..."
if [ -f "$DOCS_DIR/README.md" ]; then
    while IFS= read -r link; do
        validate_link "$DOCS_DIR/README.md" "$link"
    done < <(grep -oP '\[.*?\]\(\K[^)#]+' "$DOCS_DIR/README.md" 2>/dev/null || true)
fi

echo ""

if [ $ERRORS -eq 0 ]; then
    echo "✅ All links validated successfully!"
    exit 0
else
    echo "❌ Found $ERRORS broken links"
    exit 1
fi
