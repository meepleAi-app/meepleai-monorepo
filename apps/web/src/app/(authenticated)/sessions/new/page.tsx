/**
 * New Session Page
 *
 * Issue #5041 — Sessions Redesign Phase 1
 *
 * 4-step creation wizard for starting a new game session.
 */

import { SessionCreationWizard } from '@/components/session/SessionCreationWizard';

export default function NewSessionPage() {
  return (
    <div className="container mx-auto px-4 py-6">
      <SessionCreationWizard />
    </div>
  );
}
