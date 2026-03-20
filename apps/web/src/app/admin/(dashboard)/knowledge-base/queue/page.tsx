import { type Metadata } from 'next';

import { QueueDashboardClient } from './components/queue-dashboard-client';

export const metadata: Metadata = {
  title: 'Processing Queue',
  description: 'Monitor and manage PDF processing jobs',
};

export default async function ProcessingQueuePage({
  searchParams,
}: {
  searchParams: Promise<{ gameId?: string; jobId?: string; documentId?: string }>;
}) {
  const { gameId, jobId, documentId } = await searchParams;
  return <QueueDashboardClient gameId={gameId} highlightJobId={jobId} documentId={documentId} />;
}
