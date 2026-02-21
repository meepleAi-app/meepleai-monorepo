import type { Metadata } from 'next';

import { ToolkitConfiguratorClient } from './client';

export const metadata: Metadata = {
  title: 'Toolkit Configurator — MeepleAI Admin',
};

interface Props {
  params: Promise<{ gameId: string }>;
}

export default async function ToolkitConfiguratorPage({ params }: Props) {
  const { gameId } = await params;
  return <ToolkitConfiguratorClient gameId={gameId} />;
}
