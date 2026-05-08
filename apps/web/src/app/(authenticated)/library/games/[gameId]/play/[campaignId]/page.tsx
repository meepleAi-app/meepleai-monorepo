import { Content } from './_content';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Campagna libro game',
};

export default async function PlayCampaignPage({
  params,
}: {
  params: Promise<{ gameId: string; campaignId: string }>;
}) {
  const { gameId, campaignId } = await params;
  return <Content campaignId={campaignId} gameId={gameId} />;
}
