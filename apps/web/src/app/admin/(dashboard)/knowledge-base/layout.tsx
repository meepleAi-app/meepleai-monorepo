import { type ReactNode } from 'react';

import { KbSubNav } from '@/components/admin/knowledge-base/explorer/KbSubNav';
import { KbTopBand } from '@/components/admin/knowledge-base/explorer/KbTopBand';

export default function KnowledgeBaseLayout({ children }: { children: ReactNode }) {
  return (
    <div className="px-6">
      <KbTopBand />
      <div className="space-y-6 pt-6">
        <KbSubNav />
        {children}
      </div>
    </div>
  );
}
