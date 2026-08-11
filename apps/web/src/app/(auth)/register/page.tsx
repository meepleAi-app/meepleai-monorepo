/**
 * Register Page - App Router
 *
 * Standalone registration page with AuthModal.
 * Uses AuthLayout wrapper for consistent auth page UX (Issue #2231).
 *
 * #2773: this Server Component reads searchParams (async in Next 16) and passes
 * `oauth_disabled` down as a prop, so the client RegisterPageContent renders
 * WITHOUT `useSearchParams` — the form is server-rendered (no CSR bailout, no
 * <Suspense> fallback). Same props pattern as `login/page.tsx` (#2650 / #2771).
 */

import { RegisterPageContent } from './_content';

export default async function RegisterPage({
  searchParams,
}: {
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const params = await searchParams;
  const oauthDisabled = params.oauth_disabled === 'true';

  return <RegisterPageContent oauthDisabled={oauthDisabled} />;
}
