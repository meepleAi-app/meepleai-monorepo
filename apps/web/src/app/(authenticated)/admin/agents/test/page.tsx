/**
 * Agent Testing Console - Server Component Wrapper
 * Issue #3378
 *
 * Interactive testing interface for agent typologies with:
 * - Strategy/Model override selection
 * - Real-time metrics display
 * - Citation visualization
 * - Test result saving
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { AgentTestClient } from './client';

export default function AgentTestPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <AgentTestClient />
    </RequireRole>
  );
}
