// apps/web/src/app/admin/(dashboard)/knowledge-base/page.tsx
import { type Metadata } from 'next';

import { KbExplorer } from '@/components/admin/knowledge-base/explorer/KbExplorer';

export const metadata: Metadata = {
  title: 'Knowledge Base',
  description: 'Esploratore master-detail della Knowledge Base admin',
};

export default function KnowledgeBasePage() {
  return <KbExplorer />;
}
