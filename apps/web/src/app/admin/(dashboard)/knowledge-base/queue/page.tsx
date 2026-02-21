import { type Metadata } from 'next';

import { QueueDashboardClient } from './components/queue-dashboard-client';

export const metadata: Metadata = {
  title: 'Processing Queue',
  description: 'Monitor and manage PDF processing jobs',
};

export default async function ProcessingQueuePage({
  searchParams,
}: {
  searchParams: Promise<{ gameId?: string }>;
}) {
  const { gameId } = await searchParams;
  return <QueueDashboardClient gameId={gameId} />;
}
