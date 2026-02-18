import { redirect } from 'next/navigation';

/**
 * Users base route - redirects to roles & permissions
 * Prevents React #130 error when navigating to /admin/users
 */
export default function UsersBasePage() {
  redirect('/admin/users/roles');
}
