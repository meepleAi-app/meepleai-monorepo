import { type ReactNode } from 'react';

import { AiTopBand } from '@/components/admin/ai/AiTopBand';

export default function AdminAiLayout({ children }: { children: ReactNode }) {
  return (
    <div className="px-6">
      <AiTopBand />
      <div className="space-y-6 pt-6">{children}</div>
    </div>
  );
}
