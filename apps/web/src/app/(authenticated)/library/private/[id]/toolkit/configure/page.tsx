import { UserToolkitConfiguratorClient } from './client';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Toolkit Configurator — MeepleAI',
};

interface Props {
  params: Promise<{ id: string }>;
}

export default async function UserToolkitConfiguratorPage({ params }: Props) {
  // URL slug is the canonical `[id]` per the codebase-wide convention (issue #2316).
  // Aliased to `privateGameId` here to preserve the semantic prop name across
  // UserToolkitConfiguratorClient + downstream hooks (Option B / Wiegers).
  const { id: privateGameId } = await params;
  return <UserToolkitConfiguratorClient privateGameId={privateGameId} />;
}
