#!/bin/bash
# Script per migrare i componenti dalla root di components/ ai moduli feature-based
# Parte del refactoring descritto in docs/code-review-frontend-detailed.md

set -e

BASE_DIR="apps/web/src/components"

echo "🚀 Starting components/ root migration..."
echo "Creating feature-based module directories..."

# Fase 2.1: Creare moduli feature-based
mkdir -p "$BASE_DIR/layout"
mkdir -p "$BASE_DIR/modals"
mkdir -p "$BASE_DIR/comments"
mkdir -p "$BASE_DIR/upload"
mkdir -p "$BASE_DIR/prompt"
mkdir -p "$BASE_DIR/search"
mkdir -p "$BASE_DIR/versioning"
mkdir -p "$BASE_DIR/progress"
mkdir -p "$BASE_DIR/errors"

echo "✅ Directories created"
echo ""
echo "📦 Migrating files to feature modules..."

# Layout components
echo "  → Migrating layout components..."
mv "$BASE_DIR/ThemeSwitcher.tsx" "$BASE_DIR/layout/" 2>/dev/null || true
mv "$BASE_DIR/Toast.tsx" "$BASE_DIR/layout/" 2>/dev/null || true
mv "$BASE_DIR/KeyboardShortcutsHelp.tsx" "$BASE_DIR/layout/" 2>/dev/null || true
mv "$BASE_DIR/CommandPalette.tsx" "$BASE_DIR/layout/" 2>/dev/null || true

# Modals
echo "  → Migrating modal components..."
mv "$BASE_DIR/BggSearchModal.tsx" "$BASE_DIR/modals/" 2>/dev/null || true
mv "$BASE_DIR/ErrorModal.tsx" "$BASE_DIR/modals/" 2>/dev/null || true
mv "$BASE_DIR/ErrorModal.stories.tsx" "$BASE_DIR/modals/" 2>/dev/null || true
mv "$BASE_DIR/ExportChatModal.tsx" "$BASE_DIR/modals/" 2>/dev/null || true
mv "$BASE_DIR/ExportChatModal.stories.tsx" "$BASE_DIR/modals/" 2>/dev/null || true
mv "$BASE_DIR/SessionSetupModal.tsx" "$BASE_DIR/modals/" 2>/dev/null || true
mv "$BASE_DIR/SessionWarningModal.tsx" "$BASE_DIR/modals/" 2>/dev/null || true

# Comments
echo "  → Migrating comment components..."
mv "$BASE_DIR/CommentForm.tsx" "$BASE_DIR/comments/" 2>/dev/null || true
mv "$BASE_DIR/CommentItem.tsx" "$BASE_DIR/comments/" 2>/dev/null || true
mv "$BASE_DIR/CommentThread.tsx" "$BASE_DIR/comments/" 2>/dev/null || true
mv "$BASE_DIR/InlineCommentIndicator.tsx" "$BASE_DIR/comments/" 2>/dev/null || true

# Upload
echo "  → Migrating upload components..."
mv "$BASE_DIR/MultiFileUpload.tsx" "$BASE_DIR/upload/" 2>/dev/null || true
mv "$BASE_DIR/UploadQueue.tsx" "$BASE_DIR/upload/" 2>/dev/null || true
mv "$BASE_DIR/UploadQueueItem.tsx" "$BASE_DIR/upload/" 2>/dev/null || true
mv "$BASE_DIR/UploadSummary.tsx" "$BASE_DIR/upload/" 2>/dev/null || true

# Prompt
echo "  → Migrating prompt components..."
mv "$BASE_DIR/PromptEditor.tsx" "$BASE_DIR/prompt/" 2>/dev/null || true
mv "$BASE_DIR/PromptVersionCard.tsx" "$BASE_DIR/prompt/" 2>/dev/null || true

# Search
echo "  → Migrating search components..."
mv "$BASE_DIR/SearchFilters.tsx" "$BASE_DIR/search/" 2>/dev/null || true
mv "$BASE_DIR/SearchModeToggle.tsx" "$BASE_DIR/search/" 2>/dev/null || true

# Versioning
echo "  → Migrating versioning components..."
mv "$BASE_DIR/VersionTimeline.tsx" "$BASE_DIR/versioning/" 2>/dev/null || true
mv "$BASE_DIR/VersionTimelineFilters.tsx" "$BASE_DIR/versioning/" 2>/dev/null || true
mv "$BASE_DIR/ChangeItem.tsx" "$BASE_DIR/versioning/" 2>/dev/null || true

# Progress
echo "  → Migrating progress components..."
mv "$BASE_DIR/ProcessingProgress.tsx" "$BASE_DIR/progress/" 2>/dev/null || true

# Errors
echo "  → Migrating error components..."
mv "$BASE_DIR/ErrorBoundary.tsx" "$BASE_DIR/errors/" 2>/dev/null || true
mv "$BASE_DIR/ErrorDisplay.tsx" "$BASE_DIR/errors/" 2>/dev/null || true
mv "$BASE_DIR/ErrorDisplay.stories.tsx" "$BASE_DIR/errors/" 2>/dev/null || true
mv "$BASE_DIR/SimpleErrorMessage.tsx" "$BASE_DIR/errors/" 2>/dev/null || true
mv "$BASE_DIR/SimpleErrorMessage.stories.tsx" "$BASE_DIR/errors/" 2>/dev/null || true
mv "$BASE_DIR/RouteErrorBoundary.tsx" "$BASE_DIR/errors/" 2>/dev/null || true

# PDF (spostare in modulo pdf esistente se c'è)
if [ -d "$BASE_DIR/pdf" ]; then
  echo "  → Migrating PDF component to existing pdf/ module..."
  mv "$BASE_DIR/PdfPreview.tsx" "$BASE_DIR/pdf/" 2>/dev/null || true
fi

# Diff (spostare in modulo diff esistente)
if [ -d "$BASE_DIR/diff" ]; then
  echo "  → Migrating diff components to existing diff/ module..."
  mv "$BASE_DIR/DiffViewerEnhanced.tsx" "$BASE_DIR/diff/" 2>/dev/null || true
  mv "$BASE_DIR/DiffSummary.tsx" "$BASE_DIR/diff/" 2>/dev/null || true
fi

# Chat (spostare FollowUpQuestions nel modulo chat se esiste)
if [ -d "$BASE_DIR/chat" ]; then
  echo "  → Migrating chat components to existing chat/ module..."
  mv "$BASE_DIR/FollowUpQuestions.tsx" "$BASE_DIR/chat/" 2>/dev/null || true
  mv "$BASE_DIR/FollowUpQuestions.stories.tsx" "$BASE_DIR/chat/" 2>/dev/null || true
  mv "$BASE_DIR/MentionInput.tsx" "$BASE_DIR/chat/" 2>/dev/null || true
fi

# Admin (AdminCharts)
if [ -d "$BASE_DIR/admin" ]; then
  echo "  → Migrating admin components to existing admin/ module..."
  mv "$BASE_DIR/AdminCharts.tsx" "$BASE_DIR/admin/" 2>/dev/null || true
fi

echo "✅ File migration completed"
echo ""
echo "📝 Creating barrel exports (index.ts) for each module..."

# Layout barrel export
cat > "$BASE_DIR/layout/index.ts" << 'EOF'
// Barrel exports for layout module
export { ThemeSwitcher } from './ThemeSwitcher';
export { Toast } from './Toast';
export { KeyboardShortcutsHelp } from './KeyboardShortcutsHelp';
export { CommandPalette } from './CommandPalette';
EOF

# Modals barrel export
cat > "$BASE_DIR/modals/index.ts" << 'EOF'
// Barrel exports for modals module
export { BggSearchModal } from './BggSearchModal';
export { ErrorModal } from './ErrorModal';
export { ExportChatModal } from './ExportChatModal';
export { SessionSetupModal } from './SessionSetupModal';
export { SessionWarningModal } from './SessionWarningModal';
EOF

# Comments barrel export
cat > "$BASE_DIR/comments/index.ts" << 'EOF'
// Barrel exports for comments module
export { CommentForm } from './CommentForm';
export { CommentItem } from './CommentItem';
export { CommentThread } from './CommentThread';
export { InlineCommentIndicator } from './InlineCommentIndicator';
EOF

# Upload barrel export
cat > "$BASE_DIR/upload/index.ts" << 'EOF'
// Barrel exports for upload module
export { MultiFileUpload } from './MultiFileUpload';
export { UploadQueue } from './UploadQueue';
export { UploadQueueItem } from './UploadQueueItem';
export { UploadSummary } from './UploadSummary';
EOF

# Prompt barrel export
cat > "$BASE_DIR/prompt/index.ts" << 'EOF'
// Barrel exports for prompt module
export { PromptEditor } from './PromptEditor';
export { PromptVersionCard } from './PromptVersionCard';
EOF

# Search barrel export
cat > "$BASE_DIR/search/index.ts" << 'EOF'
// Barrel exports for search module
export { SearchFilters } from './SearchFilters';
export { SearchModeToggle } from './SearchModeToggle';
EOF

# Versioning barrel export
cat > "$BASE_DIR/versioning/index.ts" << 'EOF'
// Barrel exports for versioning module
export { VersionTimeline } from './VersionTimeline';
export { VersionTimelineFilters } from './VersionTimelineFilters';
export { ChangeItem } from './ChangeItem';
EOF

# Progress barrel export
cat > "$BASE_DIR/progress/index.ts" << 'EOF'
// Barrel exports for progress module
export { ProcessingProgress } from './ProcessingProgress';
EOF

# Errors barrel export
cat > "$BASE_DIR/errors/index.ts" << 'EOF'
// Barrel exports for errors module
export { ErrorBoundary } from './ErrorBoundary';
export { ErrorDisplay } from './ErrorDisplay';
export { SimpleErrorMessage } from './SimpleErrorMessage';
export { RouteErrorBoundary } from './RouteErrorBoundary';
EOF

echo "✅ Barrel exports created"
echo ""
echo "🎉 Migration complete!"
echo ""
echo "Next steps:"
echo "1. Run: pnpm typecheck (to find all broken imports)"
echo "2. Update imports in consumers (use find/replace)"
echo "3. Run: pnpm test"
echo "4. Run: pnpm build"
echo ""
echo "Files remaining in components/ root:"
find "$BASE_DIR" -maxdepth 1 -type f -name "*.tsx" | wc -l
