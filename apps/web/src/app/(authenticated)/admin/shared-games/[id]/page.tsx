/**
 * Game Detail Page - Issue #3536
 *
 * Route: /admin/shared-games/[id]
 * Displays game details with tabbed interface for metadata, documents, and review history.
 */

import { GameDetailClient } from './client';

interface GameDetailPageProps {
  params: Promise<{ id: string }>;
}

export default function GameDetailPage({ params }: GameDetailPageProps) {
  return <GameDetailClient params={params} />;
}

