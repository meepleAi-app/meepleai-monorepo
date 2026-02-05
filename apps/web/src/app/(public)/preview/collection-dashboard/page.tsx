import { Metadata } from 'next';

import { CollectionDashboardPreviewClient } from './client';

export const metadata: Metadata = {
  title: 'Collection Dashboard - Preview | MeepleAI',
  description: 'Preview della Collection Dashboard con Hero Stats, Filtri e Griglia',
};

export default function CollectionDashboardPreviewPage() {
  return <CollectionDashboardPreviewClient />;
}
