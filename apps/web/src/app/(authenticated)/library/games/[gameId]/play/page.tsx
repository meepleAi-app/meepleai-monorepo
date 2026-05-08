import { ResumePageContent } from './_content';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Riprendi campagna · Libro game',
};

/**
 * Resume picker page (issue #835).
 * Server shell — la dispatch logic sui 4 stati live nel client component.
 */
export default async function PlayResumePage({
  params,
}: {
  params: Promise<{ gameId: string }>;
}) {
  const { gameId } = await params;
  return <ResumePageContent gameId={gameId} />;
}
