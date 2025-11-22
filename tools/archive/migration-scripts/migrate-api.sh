#!/bin/bash
# API Migration Script - Migrate from legacy API structure to modular SDK
# FE-IMP-005: Modular API SDK Migration

set -e

REPO_ROOT="D:/Repositories/meepleai-monorepo"
WEB_SRC="$REPO_ROOT/apps/web/src"

echo "🚀 Starting API Migration (FE-IMP-005)"
echo "📂 Working directory: $WEB_SRC"
echo ""

# Create backup
echo "📦 Creating backup..."
BACKUP_DIR="$REPO_ROOT/backup-api-migration-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"
cp -r "$WEB_SRC" "$BACKUP_DIR/"
echo "✅ Backup created at: $BACKUP_DIR"
echo ""

# Count files to migrate
FILE_COUNT=$(find "$WEB_SRC" -type f \( -name "*.ts" -o -name "*.tsx" \) -exec grep -l "from.*lib/api\|from '@/lib/api'" {} \; | wc -l)
echo "📊 Files to migrate: $FILE_COUNT"
echo ""

# Migration patterns
echo "🔄 Applying migration transformations..."

# Find all TypeScript files with API imports
find "$WEB_SRC" -type f \( -name "*.ts" -o -name "*.tsx" \) -print0 | while IFS= read -r -d '' file; do
  if grep -q "from.*lib/api\|from '@/lib/api'" "$file"; then
    echo "  🔧 Migrating: ${file#$WEB_SRC/}"

    # Create temp file
    temp_file="$file.tmp"

    # Apply transformations (chatThreads → chat)
    sed -e 's/api\.chatThreads\.getByGame(/api.chat.getThreadsByGame(/g' \
        -e 's/api\.chatThreads\.getById(/api.chat.getThreadById(/g' \
        -e 's/api\.chatThreads\.create(/api.chat.createThread(/g' \
        -e 's/api\.chatThreads\.addMessage(/api.chat.addMessage(/g' \
        -e 's/api\.chatThreads\.close(/api.chat.closeThread(/g' \
        -e 's/api\.chatThreads\.reopen(/api.chat.reopenThread(/g' \
        -e 's/api\.ruleSpecComments\.getComments(/api.chat.getRuleSpecComments(/g' \
        -e 's/api\.ruleSpecComments\.createComment(/api.chat.createRuleSpecComment(/g' \
        -e 's/api\.ruleSpecComments\.updateComment(/api.chat.updateRuleSpecComment(/g' \
        -e 's/api\.ruleSpecComments\.deleteComment(/api.chat.deleteRuleSpecComment(/g' \
        -e 's/api\.ruleSpecComments\.createReply(/api.chat.createCommentReply(/g' \
        -e 's/api\.ruleSpecComments\.resolveComment(/api.chat.resolveComment(/g' \
        -e 's/api\.ruleSpecComments\.unresolveComment(/api.chat.unresolveComment(/g' \
        -e 's/api\.cache\.getStats(/api.chat.getCacheStats(/g' \
        -e 's/api\.cache\.invalidateGame(/api.chat.invalidateGameCache(/g' \
        -e 's/api\.cache\.invalidateTag(/api.chat.invalidateCacheByTag(/g' \
        "$file" > "$temp_file"

    # Replace original file if changes were made
    if ! cmp -s "$file" "$temp_file"; then
      mv "$temp_file" "$file"
    else
      rm "$temp_file"
    fi
  fi
done

echo ""
echo "✅ Migration transformations completed!"
echo ""

# Verify no legacy API calls remain
echo "🔍 Verifying migration..."
LEGACY_COUNT=$(find "$WEB_SRC" -type f \( -name "*.ts" -o -name "*.tsx" \) -exec grep -c "api\.chatThreads\|api\.ruleSpecComments\|api\.cache\." {} \; 2>/dev/null | awk '{s+=$1} END {print s}')

if [ "$LEGACY_COUNT" -eq 0 ]; then
  echo "✅ All legacy API calls migrated successfully!"
else
  echo "⚠️  Warning: $LEGACY_COUNT legacy API calls still found"
  echo "   Run: grep -r 'api\.chatThreads\|api\.ruleSpecComments' apps/web/src"
fi

echo ""
echo "🎉 Migration completed!"
echo "📊 Summary:"
echo "   - Files processed: $FILE_COUNT"
echo "   - Backup location: $BACKUP_DIR"
echo ""
echo "Next steps:"
echo "   1. Review changes: git diff"
echo "   2. Run TypeScript: pnpm typecheck"
echo "   3. Run tests: pnpm test"
echo "   4. Commit changes: git add . && git commit -m 'feat(FE-IMP-005): Migrate to modular API SDK'"
