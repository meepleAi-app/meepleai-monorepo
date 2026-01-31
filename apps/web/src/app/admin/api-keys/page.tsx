import { Metadata } from 'next';

import { ApiKeysPageClient } from './client';

export const metadata: Metadata = {
  title: 'API Keys Management | MeepleAI Admin',
  description: 'Manage API keys, view usage statistics, and control access',
};

export default function ApiKeysPage() {
  return <ApiKeysPageClient />;
}
