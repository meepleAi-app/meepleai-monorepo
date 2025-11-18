#!/bin/bash
# Script per riorganizzare components/ui/ con struttura categorizzata
# Parte del refactoring descritto in docs/code-review-frontend-detailed.md - Fase 3

set -e

UI_DIR="apps/web/src/components/ui"

echo "🚀 Starting components/ui/ reorganization..."
echo "Current structure: Flat (46 files)"
echo "Target structure: 7 categories with colocation"
echo ""

# Create category directories
echo "📁 Creating category directories..."
mkdir -p "$UI_DIR/forms"
mkdir -p "$UI_DIR/buttons"
mkdir -p "$UI_DIR/overlays"
mkdir -p "$UI_DIR/feedback"
mkdir -p "$UI_DIR/navigation"
mkdir -p "$UI_DIR/data-display"
mkdir -p "$UI_DIR/layout"

echo "✅ Categories created"
echo ""

# Migrate FORMS category
echo "📦 Migrating FORMS components..."
for comp in input textarea select checkbox switch label form; do
  compCap="$(echo "$comp" | sed 's/.*/\u&/')"
  mkdir -p "$UI_DIR/forms/$comp"
  [ -f "$UI_DIR/$comp.tsx" ] && mv "$UI_DIR/$comp.tsx" "$UI_DIR/forms/$comp/$compCap.tsx"
  [ -f "$UI_DIR/$comp.stories.tsx" ] && mv "$UI_DIR/$comp.stories.tsx" "$UI_DIR/forms/$comp/$compCap.stories.tsx"
  [ -f "$UI_DIR/__tests__/$comp.test.tsx" ] && mv "$UI_DIR/__tests__/$comp.test.tsx" "$UI_DIR/forms/$comp/$compCap.test.tsx"
  echo "export { $compCap } from './$compCap';" > "$UI_DIR/forms/$comp/index.ts"
done

# Migrate BUTTONS category
echo "📦 Migrating BUTTONS components..."
for comp in button toggle toggle-group; do
  compCap="$(echo "$comp" | sed -r 's/(^|-)([a-z])/\U\2/g')"
  mkdir -p "$UI_DIR/buttons/$comp"
  [ -f "$UI_DIR/$comp.tsx" ] && mv "$UI_DIR/$comp.tsx" "$UI_DIR/buttons/$comp/$compCap.tsx"
  [ -f "$UI_DIR/$comp.stories.tsx" ] && mv "$UI_DIR/$comp.stories.tsx" "$UI_DIR/buttons/$comp/$compCap.stories.tsx"
  echo "export { $compCap } from './$compCap';" > "$UI_DIR/buttons/$comp/index.ts"
done

# Migrate OVERLAYS category
echo "📦 Migrating OVERLAYS components..."
for comp in dialog sheet dropdown-menu; do
  compCap="$(echo "$comp" | sed -r 's/(^|-)([a-z])/\U\2/g')"
  mkdir -p "$UI_DIR/overlays/$comp"
  [ -f "$UI_DIR/$comp.tsx" ] && mv "$UI_DIR/$comp.tsx" "$UI_DIR/overlays/$comp/$compCap.tsx"
  [ -f "$UI_DIR/$comp.stories.tsx" ] && mv "$UI_DIR/$comp.stories.tsx" "$UI_DIR/overlays/$comp/$compCap.stories.tsx"
  echo "export { $compCap } from './$compCap';" > "$UI_DIR/overlays/$comp/index.ts"
done

# Migrate FEEDBACK category
echo "📦 Migrating FEEDBACK components..."
for comp in alert progress sonner skeleton; do
  compCap="$(echo "$comp" | sed 's/.*/\u&/')"
  mkdir -p "$UI_DIR/feedback/$comp"
  [ -f "$UI_DIR/$comp.tsx" ] && mv "$UI_DIR/$comp.tsx" "$UI_DIR/feedback/$comp/$compCap.tsx"
  [ -f "$UI_DIR/$comp.stories.tsx" ] && mv "$UI_DIR/$comp.stories.tsx" "$UI_DIR/feedback/$comp/$compCap.stories.tsx"
  echo "export { $compCap } from './$compCap';" > "$UI_DIR/feedback/$comp/index.ts"
done

# Migrate NAVIGATION category
echo "📦 Migrating NAVIGATION components..."
comp="tabs"
compCap="Tabs"
mkdir -p "$UI_DIR/navigation/$comp"
[ -f "$UI_DIR/$comp.tsx" ] && mv "$UI_DIR/$comp.tsx" "$UI_DIR/navigation/$comp/$compCap.tsx"
[ -f "$UI_DIR/$comp.stories.tsx" ] && mv "$UI_DIR/$comp.stories.tsx" "$UI_DIR/navigation/$comp/$compCap.stories.tsx"
echo "export { $compCap } from './$compCap';" > "$UI_DIR/navigation/$comp/index.ts"

# Migrate DATA-DISPLAY category
echo "📦 Migrating DATA-DISPLAY components..."
for comp in table card badge avatar; do
  compCap="$(echo "$comp" | sed 's/.*/\u&/')"
  mkdir -p "$UI_DIR/data-display/$comp"
  [ -f "$UI_DIR/$comp.tsx" ] && mv "$UI_DIR/$comp.tsx" "$UI_DIR/data-display/$comp/$compCap.tsx"
  [ -f "$UI_DIR/$comp.stories.tsx" ] && mv "$UI_DIR/$comp.stories.tsx" "$UI_DIR/data-display/$comp/$compCap.stories.tsx"
  echo "export { $compCap } from './$compCap';" > "$UI_DIR/data-display/$comp/index.ts"
done

# Migrate LAYOUT category
echo "📦 Migrating LAYOUT components..."
comp="separator"
compCap="Separator"
mkdir -p "$UI_DIR/layout/$comp"
[ -f "$UI_DIR/$comp.tsx" ] && mv "$UI_DIR/$comp.tsx" "$UI_DIR/layout/$comp/$compCap.tsx"
[ -f "$UI_DIR/$comp.stories.tsx" ] && mv "$UI_DIR/$comp.stories.tsx" "$UI_DIR/layout/$comp/$compCap.stories.tsx"
echo "export { $compCap } from './$compCap';" > "$UI_DIR/layout/$comp/index.ts"

echo "✅ Component migration completed"
echo ""

# Create barrel exports for each category
echo "📝 Creating category barrel exports..."

# Forms barrel
cat > "$UI_DIR/forms/index.ts" << 'EOF'
// Barrel exports for forms category
export * from './input';
export * from './textarea';
export * from './select';
export * from './checkbox';
export * from './switch';
export * from './label';
export * from './form';
EOF

# Buttons barrel
cat > "$UI_DIR/buttons/index.ts" << 'EOF'
// Barrel exports for buttons category
export * from './button';
export * from './toggle';
export * from './toggle-group';
EOF

# Overlays barrel
cat > "$UI_DIR/overlays/index.ts" << 'EOF'
// Barrel exports for overlays category
export * from './dialog';
export * from './sheet';
export * from './dropdown-menu';
EOF

# Feedback barrel
cat > "$UI_DIR/feedback/index.ts" << 'EOF'
// Barrel exports for feedback category
export * from './alert';
export * from './progress';
export * from './sonner';
export * from './skeleton';
EOF

# Navigation barrel
cat > "$UI_DIR/navigation/index.ts" << 'EOF'
// Barrel exports for navigation category
export * from './tabs';
EOF

# Data Display barrel
cat > "$UI_DIR/data-display/index.ts" << 'EOF'
// Barrel exports for data-display category
export * from './table';
export * from './card';
export * from './badge';
export * from './avatar';
EOF

# Layout barrel
cat > "$UI_DIR/layout/index.ts" << 'EOF'
// Barrel exports for layout category
export * from './separator';
EOF

echo "✅ Category barrel exports created"
echo ""

# Update main UI barrel export
echo "📝 Updating main UI barrel export..."
cat > "$UI_DIR/index.ts" << 'EOF'
// Main barrel exports for UI components
// Import from categorized structure for better organization

// Forms
export * from './forms';

// Buttons
export * from './buttons';

// Overlays
export * from './overlays';

// Feedback
export * from './feedback';

// Navigation
export * from './navigation';

// Data Display
export * from './data-display';

// Layout
export * from './layout';
EOF

echo "✅ Main UI barrel export updated"
echo ""

# Clean up old __tests__ directory if empty
if [ -d "$UI_DIR/__tests__" ] && [ -z "$(ls -A $UI_DIR/__tests__)" ]; then
  rmdir "$UI_DIR/__tests__"
  echo "✅ Removed empty __tests__ directory"
fi

echo "🎉 Phase 3 migration complete!"
echo ""
echo "Summary:"
echo "  - Created 7 category directories"
echo "  - Migrated ~23 components with colocation"
echo "  - Created category barrel exports"
echo "  - Updated main UI barrel export"
echo ""
echo "Next steps:"
echo "  1. Run: pnpm typecheck"
echo "  2. Fix any import errors"
echo "  3. Run: pnpm test"
echo "  4. Run: pnpm build"
