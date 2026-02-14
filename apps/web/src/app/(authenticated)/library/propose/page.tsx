/**
 * Editor Proposal Wizard - Propose Game to Shared Catalog
 * Issue #6: Editor Proposal Wizard (5-Step)
 *
 * Editor-only route - requires Editor or Admin role.
 * Creates ShareRequest for admin approval instead of direct publish.
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { EditorProposalClient } from './client';

export default function EditorProposalPage() {
  return (
    <RequireRole allowedRoles={['Editor', 'Admin']}>
      <EditorProposalClient />
    </RequireRole>
  );
}
