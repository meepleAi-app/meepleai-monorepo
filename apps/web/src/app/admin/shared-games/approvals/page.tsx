'use client';

import { ArrowLeft } from 'lucide-react';
import Link from 'next/link';

import { SharedGamesSection } from '@/components/admin/dashboard/shared-games-section';

export default function ApprovalsPage() {
  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 via-amber-50/30 to-orange-50/20 p-8">
      <div className="max-w-[1600px] mx-auto">
        {/* Header */}
        <div className="mb-8">
          <Link
            href="/admin"
            className="inline-flex items-center gap-1.5 text-sm text-slate-500 hover:text-amber-700 transition-colors mb-4"
          >
            <ArrowLeft className="w-4 h-4" />
            Back to Dashboard
          </Link>
          <h1 className="font-quicksand font-bold text-4xl text-slate-900 mb-2">
            Approval Queue
          </h1>
          <p className="font-nunito text-lg text-slate-600">
            Review and manage all pending shared game submissions
          </p>
        </div>

        {/* Full Approval Queue Table */}
        <SharedGamesSection />
      </div>
    </div>
  );
}
