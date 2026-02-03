/**
 * Admin Game Setup Wizard - Server Component Wrapper
 *
 * Admin-only workflow for:
 * 1. Upload PDF → Public library
 * 2. Create game with icons/images
 * 3. Open chat with RAG agent
 * 4. Q&A about the game rules
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { AdminWizardClient } from './client';

export default function AdminWizardPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <AdminWizardClient />
    </RequireRole>
  );
}
