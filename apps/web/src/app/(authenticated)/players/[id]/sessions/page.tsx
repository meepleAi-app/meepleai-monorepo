import { redirect } from 'next/navigation';

/**
 * Player Sessions sub-route - redirects to player detail page
 * Stub until full player sessions listing is implemented
 */
export default async function PlayerSessionsPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  redirect(`/players/${id}`);
}
