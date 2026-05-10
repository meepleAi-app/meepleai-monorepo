import { notFound } from 'next/navigation';

import { KNOWN_PROVIDERS, type ProviderName } from '@/lib/api/schemas/providers';

import { ProviderDetail } from './ProviderDetail';

export function generateStaticParams() {
  return KNOWN_PROVIDERS.map(name => ({ name }));
}

export const metadata = { title: 'Provider — Admin' };

export default async function ProviderDetailPage({
  params,
}: {
  params: Promise<{ name: string }>;
}) {
  const { name } = await params;
  if (!(KNOWN_PROVIDERS as readonly string[]).includes(name)) {
    notFound();
  }
  return <ProviderDetail name={name as ProviderName} />;
}
