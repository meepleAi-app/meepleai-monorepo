'use client';

/**
 * PDF Analytics Tab - Admin Dashboard
 * Issue #3715.7: System-wide PDF monitoring
 */

import { Card } from '@/components/ui/data-display/card';

export function PdfAnalyticsTab() {
  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        {/* Stat Cards */}
        <Card className="p-6 bg-white/70 backdrop-blur-md border-amber-200">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-nunito text-slate-600">Total PDFs</p>
              <p className="text-3xl font-quicksand font-bold text-slate-900">1,234</p>
            </div>
            <span className="text-4xl">📄</span>
          </div>
          <p className="text-xs text-green-600 mt-2">+12% vs last month</p>
        </Card>

        <Card className="p-6 bg-green-50/70 backdrop-blur-md border-green-200">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-nunito text-slate-600">Success Rate</p>
              <p className="text-3xl font-quicksand font-bold text-green-900">89.1%</p>
            </div>
            <span className="text-4xl">✅</span>
          </div>
          <p className="text-xs text-slate-600 mt-2">Target: &gt;95%</p>
        </Card>

        <Card className="p-6 bg-blue-50/70 backdrop-blur-md border-blue-200">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-nunito text-slate-600">Avg Processing</p>
              <p className="text-3xl font-quicksand font-bold text-blue-900">2m 45s</p>
            </div>
            <span className="text-4xl">⏱️</span>
          </div>
          <p className="text-xs text-green-600 mt-2">-15% faster</p>
        </Card>

        <Card className="p-6 bg-amber-50/70 backdrop-blur-md border-amber-200">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-nunito text-slate-600">Storage</p>
              <p className="text-3xl font-quicksand font-bold text-amber-900">14.6 GB</p>
            </div>
            <span className="text-4xl">💾</span>
          </div>
          <p className="text-xs text-slate-600 mt-2">/ 100 GB (14.6%)</p>
        </Card>
      </div>

      {/* Placeholder for charts */}
      <Card className="p-6 bg-white/70 backdrop-blur-md">
        <h3 className="font-quicksand font-semibold text-lg mb-4">Uploads Trend</h3>
        <div className="h-64 flex items-center justify-center text-slate-400">
          Charts coming in Task #18
        </div>
      </Card>

      {/* Placeholder for failed PDFs */}
      <Card className="p-6 bg-white/70 backdrop-blur-md">
        <h3 className="font-quicksand font-semibold text-lg mb-4">Failed PDFs</h3>
        <div className="h-48 flex items-center justify-center text-slate-400">
          Failed PDF Viewer coming in Task #19
        </div>
      </Card>
    </div>
  );
}
