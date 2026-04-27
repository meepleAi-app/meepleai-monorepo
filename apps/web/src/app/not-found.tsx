import { Metadata } from 'next';

import { NotFoundContent } from './_not-found-content';

// NOTE: `export const dynamic = 'force-dynamic'` is retained per spec risk #1
// (known Next.js 16 DOMMatrix bug with client imports in not-found.tsx). If
// `pnpm build` succeeds without it, remove in a follow-up — not in this task.
export const dynamic = 'force-dynamic';

export const metadata: Metadata = {
  title: 'Pagina non trovata | MeepleAI',
  description: 'La pagina che stai cercando non esiste o è stata spostata.',
  robots: { index: false, follow: false },
};

export default function NotFound() {
  return <NotFoundContent />;
}
