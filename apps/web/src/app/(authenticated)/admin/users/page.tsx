import { redirect } from 'next/navigation';

/**
 * Users base route - redirects to dashboard users section
 */
export default function UsersBasePage() {
  redirect('/admin/users/roles');
}
