import { type Metadata } from 'next';

import { AdminGameWizard } from '@/components/admin/games/wizard/AdminGameWizard';

export const metadata: Metadata = {
  title: 'Add Game',
  description: 'Import a game from BoardGameGeek into the shared catalog',
};

export default function AddGamePage() {
  return <AdminGameWizard />;
}
