import { type Metadata } from 'next';

import { ShareRequestDetail } from '@/components/admin/share-requests/ShareRequestDetail';

interface ShareRequestPageProps {
  params: Promise<{ id: string }>;
}

export const metadata: Metadata = {
  title: 'Share Request',
  description: 'Review a share request',
};

export default async function ShareRequestPage({ params }: ShareRequestPageProps) {
  const { id } = await params;
  return <ShareRequestDetail id={id} />;
}
