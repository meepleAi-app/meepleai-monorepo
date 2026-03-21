import { type Metadata } from 'next';

import { SeedingPageClient } from './client';

export const metadata: Metadata = {
  title: 'Game Seeding',
  description: 'Manage BGG game enrichment and track seeding progress',
};

export default function SeedingPage() {
  return <SeedingPageClient />;
}
