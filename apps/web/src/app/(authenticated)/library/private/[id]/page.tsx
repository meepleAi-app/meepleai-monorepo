import { PrivateGameHub } from '@/components/library/private-game-detail/PrivateGameHub';

interface Props {
  params: Promise<{ id: string }>;
}

export default async function PrivateGameDetailPage({ params }: Props) {
  // URL slug is the canonical `[id]` per the codebase-wide convention (issue #2316).
  // Aliased to `privateGameId` here to preserve the semantic prop name across
  // PrivateGameHub + downstream hooks/clients (Option B / Wiegers).
  const { id: privateGameId } = await params;
  return <PrivateGameHub privateGameId={privateGameId} />;
}
