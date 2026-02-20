import { redirect } from 'next/navigation';

/**
 * Game Strategies sub-route - redirects to game detail page
 * Stub until full strategies viewer is implemented
 */
export default async function GameStrategiesPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  redirect(`/games/${id}`);
}
