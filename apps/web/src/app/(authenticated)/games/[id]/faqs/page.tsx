import { redirect } from 'next/navigation';

/**
 * Game FAQs sub-route - redirects to game detail page
 * Stub until full FAQs viewer is implemented
 */
export default async function GameFaqsPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  redirect(`/games/${id}`);
}
