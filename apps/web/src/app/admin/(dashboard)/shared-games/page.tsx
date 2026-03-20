import { redirect } from 'next/navigation';

export default function SharedGamesIndexPage() {
  redirect('/admin/shared-games/all');
}
