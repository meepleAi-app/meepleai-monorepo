import { type Metadata } from 'next';

import { KBSettings } from '@/components/admin/knowledge-base/kb-settings';

export const metadata: Metadata = {
  title: 'Settings',
  description: 'View knowledge base and RAG pipeline configuration',
};

export default function KBSettingsPage() {
  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Knowledge Base Settings
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          View knowledge base and RAG pipeline configuration
        </p>
      </div>

      {/* Settings */}
      <KBSettings />
    </div>
  );
}
