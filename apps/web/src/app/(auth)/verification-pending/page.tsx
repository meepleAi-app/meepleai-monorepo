/**
 * Verification Pending Page (Issue #3076)
 *
 * Displayed after registration to inform user to check their email.
 * Shows the email address (masked) and provides resend functionality.
 *
 * Task 12 (auth-flow-v2 migration): uses v2 AuthCard primitive via _content.tsx.
 */

import { VerificationPendingContent } from './_content';

/**
 * #2773: async Server Component reads searchParams (async in Next 16) and passes
 * `email` down as a prop, so the client content renders WITHOUT `useSearchParams`
 * — no CSR bailout. Same props pattern as `login/page.tsx` (#2650 / #2771).
 */
export default async function VerificationPendingPage({
  searchParams,
}: {
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const params = await searchParams;
  const email = typeof params.email === 'string' ? params.email : undefined;

  return <VerificationPendingContent emailParam={email} />;
}
