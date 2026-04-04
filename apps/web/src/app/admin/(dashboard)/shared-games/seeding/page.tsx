import { type Metadata } from 'next';

import { SeedingPageClient } from './client';

export const metadata: Metadata = {
  title: 'Game Seeding',
  description: 'Manage game enrichment and track seeding progress',
};

export default function SeedingPage() {
  return <SeedingPageClient />;
}
