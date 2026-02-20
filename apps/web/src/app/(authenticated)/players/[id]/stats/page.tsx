import { redirect } from 'next/navigation';

/**
 * Player Stats sub-route - redirects to player detail page
 * Stub until full player statistics page is implemented
 */
export default async function PlayerStatsPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  redirect(`/players/${id}`);
}
