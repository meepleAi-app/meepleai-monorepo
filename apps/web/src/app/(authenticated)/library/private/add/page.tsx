/**
 * User Wizard Page - Add Game to Private Library
 * Issue #4: User Game Creation Wizard (3-Step)
 *
 * Authenticated route - requires User, Editor, or Admin role.
 * Wizard flow: Game → Optional PDF → Optional Agent
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { UserWizardClient } from './client';

export default function UserWizardPage() {
  return (
    <RequireRole allowedRoles={['User', 'Editor', 'Admin']}>
      <UserWizardClient />
    </RequireRole>
  );
}
