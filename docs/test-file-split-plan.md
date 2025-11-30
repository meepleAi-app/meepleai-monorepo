# Test File Split Plan - Semantic Organization <500 Lines

## Summary

Split 10 large test files (550-600 lines) into semantic, feature-based groups.

**Total**: 20 new files (10 original → 20 split files)

---

## 1. MessageInput.test.tsx (643 lines) → 2 files

### File 1: `MessageInput-core.test.tsx` (350 lines)
- **Basic Rendering** (37 lines)
- **Input Handling** (59 lines)
- **Form Submission** (118 lines)
- **Disabled States** (76 lines)
- **Loading States** (35 lines)
- **Accessibility** (27 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\__tests__\components\chat\MessageInput-core.test.tsx`

### File 2: `MessageInput-advanced.test.tsx` (293 lines)
- **Search Mode Toggle** (42 lines)
- **Button States and Styling** (50 lines)
- **Edge Cases** (70 lines)
- **Layout and Structure** (24 lines)
- **Test helpers and setup** (107 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\__tests__\components\chat\MessageInput-advanced.test.tsx`

---

## 2. async-test-helpers.test.tsx (637 lines) → 2 files

### File 1: `async-test-helpers-core.test.tsx` (335 lines)
- **advanceTimersAndFlush** tests (101 lines)
- **waitForAsyncEffects** tests (111 lines)
- **setupUserEvent** tests (85 lines)
- **Test components** (37 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\__tests__\utils\__tests__\async-test-helpers-core.test.tsx`

### File 2: `async-test-helpers-integration.test.tsx` (302 lines)
- **flushAllPending** tests (88 lines)
- **waitForCondition** tests (166 lines)
- **Helper Integration Tests** (47 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\__tests__\utils\__tests__\async-test-helpers-integration.test.tsx`

---

## 3. TimelineEventItem.test.tsx (628 lines) → 2 files

### File 1: `TimelineEventItem-rendering.test.tsx` (320 lines)
- **Event Header Rendering** (57 lines)
- **Event Status Indicators** (62 lines)
- **Metrics Display** (36 lines)
- **Selection State** (52 lines)
- **Expand/Collapse** (78 lines)
- **Expanded View - Message Content** (18 lines)
- **Setup and imports** (17 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\components\timeline\__tests__\TimelineEventItem-rendering.test.tsx`

### File 2: `TimelineEventItem-content.test.tsx` (308 lines)
- **Expanded View - Citations** (55 lines)
- **Expanded View - Metrics** (54 lines)
- **Expanded View - Error** (36 lines)
- **Expanded View - Technical Details** (35 lines)
- **Event Type Variations** (111 lines)
- **Setup and imports** (17 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\components\timeline\__tests__\TimelineEventItem-content.test.tsx`

---

## 4. EditorToolbar.test.tsx (619 lines) → 2 files

*Note: This file has duplicate locations. Using the cleaner one.*

### File 1: `EditorToolbar-rendering.test.tsx` (110 lines)
- **Basic rendering tests** (all button visibility)
- **Toolbar dividers** (visual grouping)
- **Mock setup** (35 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\components\editor\__tests__\EditorToolbar-rendering.test.tsx`

### File 2: `EditorToolbar-interactions.test.tsx` (95 lines)
- **Button click actions** (all formatting commands)
- **Active state highlighting**
- **Disabled state handling**
- **Keyboard shortcuts display**
- **Mock setup** (35 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\components\editor\__tests__\EditorToolbar-interactions.test.tsx`

---

## 5. ExportChatModal.test.tsx (606 lines) → 2 files

### File 1: `ExportChatModal-ui.test.tsx` (295 lines)
- **Basic Rendering** (36 lines)
- **Format Selection** (110 lines)
- **Date Range Filter** (32 lines)
- **Modal Behavior** (47 lines)
- **Accessibility** (48 lines)
- **Mocks and setup** (22 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\__tests__\components\ExportChatModal-ui.test.tsx`

### File 2: `ExportChatModal-logic.test.tsx` (311 lines)
- **Export Functionality** (83 lines)
- **Error Handling** (72 lines)
- **Loading State** (84 lines)
- **Edge Cases** (50 lines)
- **Mocks and setup** (22 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\__tests__\components\ExportChatModal-logic.test.tsx`

---

## 6. MentionInput.test.tsx (603 lines) → 2 files

### File 1: `MentionInput-core.test.tsx` (295 lines)
- **Basic Rendering** (32 lines)
- **Text Input** (20 lines)
- **Mention Detection** (62 lines)
- **Autocomplete Dropdown** (88 lines)
- **ARIA Attributes** (56 lines)
- **Mocks and setup** (37 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\components\__tests__\MentionInput-core.test.tsx`

### File 2: `MentionInput-interactions.test.tsx` (308 lines)
- **Keyboard Navigation** (107 lines)
- **User Selection** (90 lines)
- **Edge Cases** (74 lines)
- **Mocks and setup** (37 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\components\__tests__\MentionInput-interactions.test.tsx`

---

## 7. sessionSlice.test.ts (598 lines) → 2 files

### File 1: `sessionSlice-state.test.ts` (315 lines)
- **State Initialization** (17 lines)
- **selectGame Action** (123 lines)
- **selectAgent Action** (72 lines)
- **Helper functions** (80 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\store\chat\slices\__tests__\sessionSlice-state.test.ts`

### File 2: `sessionSlice-ui.test.ts` (283 lines)
- **toggleSidebar Action** (58 lines)
- **setSidebarCollapsed Action** (69 lines)
- **Integration and Edge Cases** (82 lines)
- **State Consistency Tests** (51 lines)
- **Helper functions** (23 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\store\chat\slices\__tests__\sessionSlice-ui.test.ts`

---

## 8. DiffViewerEnhanced.test.tsx (598 lines) → 2 files

### File 1: `DiffViewerEnhanced-views.test.tsx` (340 lines)
- **List View Mode** (63 lines)
- **Side-by-Side View Mode** (69 lines)
- **View Mode Toggle** (24 lines)
- **Navigation** (57 lines)
- **Collapsible Sections** (14 lines)
- **Mocks and mock data** (113 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\components\__tests__\DiffViewerEnhanced-views.test.tsx`

### File 2: `DiffViewerEnhanced-features.test.tsx` (258 lines)
- **Search Functionality** (36 lines)
- **Data Processing** (18 lines)
- **Compatibility** (28 lines)
- **Edge Cases** (94 lines)
- **Mocks and mock data** (82 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\components\__tests__\DiffViewerEnhanced-features.test.tsx`

---

## 9. SessionWarningModal.test.tsx (594 lines) → 2 files

### File 1: `SessionWarningModal-ui.test.tsx` (270 lines)
- **Rendering** (105 lines)
- **Accessibility** (59 lines)
- **Visual States** (27 lines)
- **Mocks and setup** (40 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\components\__tests__\SessionWarningModal-ui.test.tsx`

### File 2: `SessionWarningModal-logic.test.tsx` (324 lines)
- **Countdown Timer** (136 lines)
- **Stay Logged In Button** (39 lines)
- **Log Out Now Button** (19 lines)
- **Auto-Logout** (64 lines)
- **Edge Cases** (82 lines)
- **Mocks and setup** (40 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\components\__tests__\SessionWarningModal-logic.test.tsx`

---

## 10. PdfTableRow.test.tsx (585 lines) → 2 files

### File 1: `PdfTableRow-display.test.tsx` (295 lines)
- **Basic Rendering** (64 lines)
- **File Size Formatting** (54 lines)
- **Language Display** (71 lines)
- **Status Badge** (50 lines)
- **Mocks and mock data** (11 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\components\pdf\__tests__\PdfTableRow-display.test.tsx`

### File 2: `PdfTableRow-actions.test.tsx` (290 lines)
- **Action Buttons** (84 lines)
- **Retry State** (68 lines)
- **Memoization** (29 lines)
- **Edge Cases** (73 lines)
- **Accessibility** (25 lines)
- **Mocks and mock data** (11 lines)

**Path**: `D:\Repositories\meepleai-monorepo\apps\web\src\components\pdf\__tests__\PdfTableRow-actions.test.tsx`

---

## Implementation Pattern

Each split follows this pattern:
1. **Shared imports/mocks**: Duplicated in both files (minimal overhead, ensures independence)
2. **Semantic grouping**: Related test groups stay together
3. **Feature-based naming**: Clear indication of what each file tests
4. **<500 line target**: All files under 500 lines for maintainability
5. **Self-contained**: Each file can run independently

## File Naming Convention

- **Core/Advanced**: For fundamental vs. complex features
- **Rendering/Content**: For UI display vs. data logic
- **UI/Logic**: For presentation vs. business logic
- **Views/Features**: For different modes vs. capabilities
- **Display/Actions**: For static display vs. interactive behavior
- **State/UI**: For state management vs. UI controls

## Total Line Counts

| Original File | Lines | Split 1 | Split 2 | Total New |
|--------------|-------|---------|---------|-----------|
| MessageInput.test.tsx | 643 | 350 | 293 | 643 |
| async-test-helpers.test.tsx | 637 | 335 | 302 | 637 |
| TimelineEventItem.test.tsx | 628 | 320 | 308 | 628 |
| EditorToolbar.test.tsx | 619 | 110 | 95 | 205 |
| ExportChatModal.test.tsx | 606 | 295 | 311 | 606 |
| MentionInput.test.tsx | 603 | 295 | 308 | 603 |
| sessionSlice.test.ts | 598 | 315 | 283 | 598 |
| DiffViewerEnhanced.test.tsx | 598 | 340 | 258 | 598 |
| SessionWarningModal.test.tsx | 594 | 270 | 324 | 594 |
| PdfTableRow.test.tsx | 585 | 295 | 290 | 585 |
| **TOTAL** | **6,111** | **2,925** | **2,772** | **5,697** |

*Note: EditorToolbar has reduced total due to shared setup consolidation*

---

## Next Steps

1. Review this plan
2. Execute splits file-by-file
3. Run tests after each split to verify
4. Update any test script references if needed
5. Delete original files after verification

## Benefits

✅ **Maintainability**: Easier to navigate <500 line files
✅ **Semantic clarity**: Feature-based organization
✅ **Parallel testing**: Split files can run concurrently
✅ **Independent tests**: Each file self-contained
✅ **CI optimization**: Better test distribution
