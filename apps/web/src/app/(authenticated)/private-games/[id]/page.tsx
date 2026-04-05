/**
 * Private Game Detail Page
 *
 * Issue #3664: Private game PDF support — detail page with PDF upload and RAG chat.
 *
 * Server Component: resolves route params and delegates rendering to the
 * PrivateGameDetailClient which fetches game data on the client.
 *
 * Route: /private-games/[id]
 */

import { PrivateGameDetailClient } from '@/components/private-game/PrivateGameDetailClient';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

interface Props {
  params: Promise<{ id: string }>;
}

// ---------------------------------------------------------------------------
// Page Component
// ---------------------------------------------------------------------------

export default async function PrivateGameDetailPage({ params }: Props) {
  const { id } = await params;
  return <PrivateGameDetailClient privateGameId={id} />;
}
