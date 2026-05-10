/**
 * DEFERRED — vedi spec G4 v3 §5.2:
 * docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md
 *
 * Questo componente fa parte di un progetto "/kb/[id]" page (originariamente
 * Wave 3 Tier M) che è stato cancellato dopo il pivot legale del 2026-05-10:
 * il flusso "serata di gioco" ora apre il PDF via CitationModal con ownership
 * gate, NON via pagina dedicata.
 *
 * Lo stub resta per:
 *   - Future "browse KB" feature autonoma (post-Alpha)
 *   - Mantenere v2-migration-matrix coerente
 *
 * NON IMPORTARE questo componente. Ritorna null finché un nuovo plan dedicato
 * non lo riprende.
 */
// TODO: implement per admin-mockups/design_files/sp4-kb-detail.jsx
// Mapped from mockup component: ChunkSearchBox
// Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
// Tracking: docs/frontend/v2-migration-matrix.md (Issue #573)

import type { ReactElement } from 'react';

export interface ChunkSearchBoxProps {
  // TODO: extract props contract from mockup analysis
}

export function ChunkSearchBox(_props: ChunkSearchBoxProps): ReactElement | null {
  return null;
}
