import { redirect } from 'next/navigation';

/**
 * Player Games sub-route - redirects to player detail page
 * Stub until full player games listing is implemented
 */
export default async function PlayerGamesPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  redirect(`/players/${id}`);
}
