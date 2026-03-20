import { type Metadata } from 'next';

import { QueueDashboardClient } from './components/queue-dashboard-client';

export const metadata: Metadata = {
  title: 'Processing Queue',
  description: 'Monitor and manage PDF processing jobs',
};

export default async function ProcessingQueuePage({
  searchParams,
}: {
  searchParams: Promise<{ gameId?: string; jobId?: string }>;
}) {
  const { gameId, jobId } = await searchParams;
  return <QueueDashboardClient gameId={gameId} highlightJobId={jobId} />;
}
