/**
 * Setup Account Page — Invitation Activation Flow (Issue #124)
 *
 * Public page for invited users to set their password and activate their account.
 * Uses the POST /auth/validate-invitation and POST /auth/activate-account endpoints.
 *
 * Flow:
 * 1. Read `token` from URL search params
 * 2. Validate token via POST /auth/validate-invitation (token in body, never in URL — I1)
 * 3. If valid: show password form with pre-filled email/displayName
 * 4. On submit: activate account and redirect to onboarding or dashboard
 */

import { SetupAccountContent } from './_content';

/**
 * #2773: async Server Component reads searchParams (async in Next 16) and passes
 * `token` down as a prop, so the client content renders WITHOUT `useSearchParams`
 * — no CSR bailout. Same props pattern as `login/page.tsx` (#2650 / #2771).
 */
export default async function SetupAccountPage({
  searchParams,
}: {
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const params = await searchParams;
  const token = typeof params.token === 'string' ? params.token : undefined;

  return <SetupAccountContent token={token} />;
}
