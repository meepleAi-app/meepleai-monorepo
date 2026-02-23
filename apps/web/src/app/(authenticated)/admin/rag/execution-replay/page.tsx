/**
 * RAG Execution Replay Page - Server Component Wrapper
 * Issue #4459
 *
 * Admin page for replaying past RAG executions and comparing results.
 */

import { Suspense } from 'react';

import { RequireRole } from '@/components/auth/RequireRole';

import { ExecutionReplayClient } from './client';

export default function ExecutionReplayPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <Suspense>
        <ExecutionReplayClient />
      </Suspense>
    </RequireRole>
  );
}
