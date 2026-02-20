import { redirect } from 'next/navigation';

/**
 * Shared Games base route - redirects to All Games listing
 * Prevents empty page when navigating to /admin/shared-games
 */
export default function SharedGamesBasePage() {
  redirect('/admin/shared-games/all');
}
