// apps/web/src/app/admin/(dashboard)/knowledge-base/page.tsx
import { type Metadata } from 'next';

import { KbExplorer } from '@/components/admin/knowledge-base/explorer/KbExplorer';

export const metadata: Metadata = {
  title: 'Knowledge Base',
  description: 'Esploratore master-detail della Knowledge Base admin',
};

export default function KnowledgeBasePage() {
  return (
    <div className="space-y-4">
      <header>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Knowledge Base
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Esplora i documenti indicizzati per gioco.
        </p>
      </header>

      <KbExplorer />
    </div>
  );
}
