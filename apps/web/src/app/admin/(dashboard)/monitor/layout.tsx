import type { ReactNode } from 'react';

import { MonitorTopBand } from '@/components/admin/monitor/MonitorTopBand';

export default function MonitorLayout({ children }: { children: ReactNode }) {
  return (
    <div className="space-y-6 px-6">
      <MonitorTopBand />
      {children}
    </div>
  );
}
