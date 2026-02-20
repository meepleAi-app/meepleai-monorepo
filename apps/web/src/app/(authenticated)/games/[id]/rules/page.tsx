import { redirect } from 'next/navigation';

/**
 * Game Rules sub-route - redirects to game detail page
 * Stub until full rules viewer is implemented
 */
export default async function GameRulesPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  redirect(`/games/${id}`);
}
