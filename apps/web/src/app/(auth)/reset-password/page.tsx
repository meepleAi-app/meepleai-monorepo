/**
 * Password Reset Page (AUTH-04) — v2 migration (Task 13).
 *
 * #2773: async Server Component reads searchParams (async in Next 16) and passes
 * `token` down as a prop, so the client _content renders WITHOUT
 * `useSearchParams` — SSR'd, no CSR bailout. Same props pattern as
 * `login/page.tsx` (#2650 / #2771).
 */

import { Metadata } from 'next';

import { ResetPasswordPageContent } from './_content';

export const metadata: Metadata = {
  title: 'Reimposta password | MeepleAI',
  description: 'Reimposta la password del tuo account MeepleAI in modo sicuro.',
  robots: { index: false, follow: false },
};

export default async function ResetPasswordPage({
  searchParams,
}: {
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const params = await searchParams;
  const token = typeof params.token === 'string' ? params.token : undefined;

  return <ResetPasswordPageContent token={token} />;
}
