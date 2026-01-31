/**
 * Slot Management Page
 * Issue #3246 (FRONT-010)
 *
 * Features:
 * - View active agent sessions
 * - Release/transfer slots
 * - Slot usage statistics
 */

'use client';

import { ArrowLeft } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/primitives/button';
import { SlotCards } from '@/components/agent/config/SlotCards';

export default function SlotManagementPage() {
  const router = useRouter();

  return (
    <div className="min-h-screen bg-slate-950 p-6">
      {/* Header */}
      <div className="mb-6 flex items-center gap-4">
        <Button variant="ghost" size="icon" onClick={() => router.back()}>
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <div>
          <h1 className="text-2xl font-bold text-white agent-heading">Slot Management</h1>
          <p className="text-sm text-slate-400">Manage your active agent sessions</p>
        </div>
      </div>

      {/* Slot Cards */}
      <div className="max-w-4xl">
        <SlotCards />
      </div>

      {/* Usage Stats - Placeholder */}
      <div className="mt-8 max-w-4xl">
        <div className="rounded-lg border border-slate-800 bg-slate-900/50 p-6">
          <h3 className="mb-4 font-semibold text-slate-200">Usage Statistics</h3>
          <p className="text-sm text-slate-400">
            Slot usage analytics coming soon
          </p>
        </div>
      </div>
    </div>
  );
}
