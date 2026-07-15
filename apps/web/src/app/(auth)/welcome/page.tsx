/**
 * Welcome Page - Post-Registration Landing
 *
 * Displays a welcome message after successful registration,
 * then automatically redirects to dashboard after 2 seconds.
 *
 * #2773: async Server Component reads searchParams (async in Next 16) and passes
 * `redirectTo` down as a prop, so the client WelcomeContent renders WITHOUT
 * `useSearchParams` — no CSR bailout. Same props pattern as `login/page.tsx`.
 */

import { WelcomeContent } from './_content';

export default async function WelcomePage({
  searchParams,
}: {
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const params = await searchParams;
  const redirectTo = typeof params.redirectTo === 'string' ? params.redirectTo : undefined;

  return <WelcomeContent redirectToParam={redirectTo} />;
}
