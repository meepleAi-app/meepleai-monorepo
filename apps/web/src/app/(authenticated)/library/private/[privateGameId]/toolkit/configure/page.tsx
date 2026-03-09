import { UserToolkitConfiguratorClient } from './client';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Toolkit Configurator — MeepleAI',
};

interface Props {
  params: Promise<{ privateGameId: string }>;
}

export default async function UserToolkitConfiguratorPage({ params }: Props) {
  const { privateGameId } = await params;
  return <UserToolkitConfiguratorClient privateGameId={privateGameId} />;
}
