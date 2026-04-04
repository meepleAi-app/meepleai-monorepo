'use client';

import { QueuePreviewWidget } from './queue-preview-widget';
import { RecentPdfsWidget } from './recent-pdfs-widget';

export function KbHubClient() {
  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
      <QueuePreviewWidget />
      <RecentPdfsWidget />
    </div>
  );
}
