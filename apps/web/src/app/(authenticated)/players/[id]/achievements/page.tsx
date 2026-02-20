import { redirect } from 'next/navigation';

/**
 * Player Achievements sub-route - redirects to player detail page
 * Stub until full player achievements page is implemented
 */
export default async function PlayerAchievementsPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  redirect(`/players/${id}`);
}
