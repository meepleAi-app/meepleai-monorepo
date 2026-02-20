import { redirect } from 'next/navigation';

/**
 * Game Reviews sub-route - redirects to game detail page
 * Stub until full reviews viewer is implemented
 */
export default async function GameReviewsPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  redirect(`/games/${id}`);
}
