import { type Metadata } from 'next';
import { redirect } from 'next/navigation';

export const metadata: Metadata = {
  title: 'Redirecting... | MeepleAI Admin',
};

/**
 * Legacy enterprise dashboard route.
 * Redirects to the new unified admin overview.
 */
export default function LegacyDashboardRedirect() {
  redirect('/admin/overview');
}
