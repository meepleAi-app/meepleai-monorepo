import { type Metadata } from 'next';

import { KBSettings } from '@/components/admin/knowledge-base/kb-settings';

export const metadata: Metadata = {
  title: 'Settings',
  description: 'View knowledge base and RAG pipeline configuration',
};

export default function KBSettingsPage() {
  return (
    <div className="space-y-6">
      <KBSettings />
    </div>
  );
}
