/**
 * Verify Email Page (Issue #3076)
 *
 * Handles email verification token validation.
 * User lands here after clicking the verification link in their email.
 */

import { Metadata } from 'next';

import { VerifyEmailContent } from './_content';

export const metadata: Metadata = {
  title: 'Verifica email | MeepleAI',
  description: 'Verifica il tuo indirizzo email per attivare il tuo account MeepleAI.',
  robots: { index: false, follow: false },
};

/**
 * #2773: async Server Component reads searchParams (async in Next 16) and passes
 * `token`/`email` down as props, so the client content renders WITHOUT
 * `useSearchParams` — no CSR bailout. Same props pattern as `login/page.tsx`.
 */
export default async function VerifyEmailPage({
  searchParams,
}: {
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const params = await searchParams;
  const token = typeof params.token === 'string' ? params.token : undefined;
  const email = typeof params.email === 'string' ? params.email : undefined;

  return <VerifyEmailContent token={token} emailParam={email} />;
}
