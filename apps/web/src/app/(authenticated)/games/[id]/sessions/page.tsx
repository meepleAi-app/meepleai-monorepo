import { redirect } from 'next/navigation';

/**
 * Game Sessions sub-route - redirects to game detail page
 * Stub until full game sessions listing is implemented
 */
export default async function GameSessionsPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  redirect(`/games/${id}`);
}
