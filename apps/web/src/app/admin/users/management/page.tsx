import { redirect } from 'next/navigation';

import { type Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Redirecting... | MeepleAI Admin',
};

/**
 * Legacy user management route.
 * Redirects to the new unified admin dashboard Users section.
 */
export default function LegacyUserManagementRedirect() {
  redirect('/admin/users');
}
