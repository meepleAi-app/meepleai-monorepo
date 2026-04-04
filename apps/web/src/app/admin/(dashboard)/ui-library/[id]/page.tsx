'use client';

import { use } from 'react';

import { notFound } from 'next/navigation';

import { ComponentDetail } from '@/components/admin/ui-library/ComponentDetail';
import { getRegistryEntry } from '@/config/component-registry';

interface UILibraryDetailPageProps {
  params: Promise<{ id: string }>;
}

export default function UILibraryDetailPage({ params }: UILibraryDetailPageProps) {
  const { id } = use(params);
  const entry = getRegistryEntry(id);

  if (!entry) {
    notFound();
  }

  return (
    <div className="p-6">
      <ComponentDetail entry={entry} />
    </div>
  );
}
