'use client';

import { useState } from 'react';

import { UserDashboard, type DashboardMode } from '@/components/dashboard';

export function UnifiedDashboardPreviewClient() {
  const [currentMode, setCurrentMode] = useState<DashboardMode>('compact');

  return (
    <div className="min-h-screen">
      {/* Debug Info Banner */}
      <div className="fixed top-0 left-0 z-50 bg-black/80 text-white px-3 py-1.5 text-xs font-mono rounded-br-lg">
        Mode: <span className="text-amber-400 font-bold">{currentMode}</span>
      </div>

      <UserDashboard
        defaultMode="compact"
        userName="Marco"
        onModeChange={setCurrentMode}
      />
    </div>
  );
}
