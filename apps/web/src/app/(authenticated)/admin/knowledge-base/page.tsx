/**
 * Admin Knowledge Base Overview - Server Component Wrapper
 * Issue #4892
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { KnowledgeBaseOverviewClient } from './client';

export default function AdminKnowledgeBasePage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <KnowledgeBaseOverviewClient />
    </RequireRole>
  );
}
