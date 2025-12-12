/**
 * Admin FAQ Management Page - Server Component Wrapper
 * Issue #2028: Backend FAQ system for game-specific FAQs
 *
 * Security:
 * - RequireRole: Admin/Editor access
 * - All operations use CQRS commands/queries
 * - Session validated at middleware level
 */

import { Metadata } from 'next';
import { RequireRole } from '@/components/auth/RequireRole';
import { FAQManagementClient } from './client';

export const metadata: Metadata = {
  title: 'FAQ Management | MeepleAI Admin',
  description: 'Manage game-specific FAQs with CRUD operations and upvote tracking',
};

export default function FAQManagementPage() {
  return (
    <RequireRole allowedRoles={['Admin', 'Editor']}>
      <FAQManagementClient />
    </RequireRole>
  );
}
