import { redirect } from 'next/navigation';

import { type Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Redirecting... | MeepleAI Admin',
};

/**
 * Legacy approvals route.
 * Redirects to the new unified admin dashboard Shared Games section.
 */
export default function LegacyApprovalsRedirect() {
  redirect('/admin/shared-games');
}
