import { PrivateGameHub } from '@/components/library/private-game-detail/PrivateGameHub';

interface Props {
  params: Promise<{ privateGameId: string }>;
}

export default async function PrivateGameDetailPage({ params }: Props) {
  const { privateGameId } = await params;
  return <PrivateGameHub privateGameId={privateGameId} />;
}
