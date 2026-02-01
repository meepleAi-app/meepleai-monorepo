/**
 * User AI Usage Page - Issue #3080
 *
 * Personal AI usage statistics page for authenticated users.
 * Displays usage summary with optional detailed breakdown.
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { PersonalUsagePageClient } from './client';

export const metadata = {
  title: 'AI Usage | Settings | MeepleAI',
  description: 'View your personal AI usage statistics and cost breakdown',
};

export default function SettingsUsagePage() {
  return (
    <RequireRole allowedRoles={['Admin', 'Editor', 'User']}>
      <PersonalUsagePageClient />
    </RequireRole>
  );
}
