'use client';

import { ArrowLeft } from 'lucide-react';
import Link from 'next/link';

import { UserManagementSection } from '@/components/admin/dashboard/user-management-section';

export default function UserManagementPage() {
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
            User Management
          </h1>
          <p className="font-nunito text-lg text-slate-600">
            Comprehensive user administration and account management
          </p>
        </div>

        {/* Full User Management Table */}
        <UserManagementSection />
      </div>
    </div>
  );
}
