import { redirect } from 'next/navigation';

/**
 * Profile landing page - redirects to achievements
 * Stub until full profile page with settings/achievements tabs is implemented
 */
export default function ProfilePage() {
  redirect('/profile/achievements');
}
