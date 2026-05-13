import { Metadata } from 'next';

import { NotFoundContent } from './_not-found-content';

export const metadata: Metadata = {
  title: 'Pagina non trovata | MeepleAI',
  description: 'La pagina che stai cercando non esiste o è stata spostata.',
  robots: { index: false, follow: false },
};

export default function NotFound() {
  return <NotFoundContent />;
}
