import { Content } from './_content';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Traduci pagina libro game',
};

export default async function TranslatePage({
  params,
}: {
  params: Promise<{ gameId: string; campaignId: string }>;
}) {
  const { gameId, campaignId } = await params;
  return <Content gameId={gameId} campaignId={campaignId} />;
}
