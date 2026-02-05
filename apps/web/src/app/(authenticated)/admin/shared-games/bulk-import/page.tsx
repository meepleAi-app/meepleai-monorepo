import { BulkImportClient } from './client';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Bulk Import from BGG - Admin',
  description: 'Import multiple games from BoardGameGeek',
};

export default function BulkImportPage() {
  return <BulkImportClient />;
}
