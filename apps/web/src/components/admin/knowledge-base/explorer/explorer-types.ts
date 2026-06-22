/**
 * Shared types for the KB explorer master-detail panel.
 * Kept in a standalone file so KbTree, KbExplorer and KbDocDetailPanel can
 * all import without creating circular deps.
 */

/**
 * Metadata the tree carries for a selected document.
 * Passed down to KbDocDetailPanel so it can display title/gameId even when
 * the document is locked (HTTP 423, no body) — e.g. for action-bar rendering.
 */
export interface SelectedDocMeta {
  id: string;
  title: string;
  gameId: string;
}
