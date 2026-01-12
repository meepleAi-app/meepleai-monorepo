#!/usr/bin/env bash
# Cleanup script for tmpclaude temporary files
# Usage: ./scripts/cleanup-tmpclaude.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

echo "🧹 Searching for tmpclaude temporary files..."

# Find and count files
FILE_COUNT=$(find "${ROOT_DIR}" -name "tmpclaude*" -type f | wc -l)

if [ "$FILE_COUNT" -eq 0 ]; then
    echo "✅ No tmpclaude files found. Repository is clean!"
    exit 0
fi

echo "📁 Found $FILE_COUNT tmpclaude file(s):"
find "${ROOT_DIR}" -name "tmpclaude*" -type f

echo ""
read -p "❓ Do you want to delete these files? (y/N): " -n 1 -r
echo

if [[ $REPLY =~ ^[Yy]$ ]]; then
    find "${ROOT_DIR}" -name "tmpclaude*" -type f -delete
    echo "✅ Cleanup completed successfully!"
else
    echo "ℹ️  Cleanup cancelled."
fi
