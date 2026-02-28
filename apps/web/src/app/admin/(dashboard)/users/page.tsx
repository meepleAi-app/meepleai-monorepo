import { redirect } from 'next/navigation';

/**
 * /admin/users index — redirect to the activity log as the default user view.
 */
export default function AdminUsersPage() {
  redirect('/admin/users/activity');
}
