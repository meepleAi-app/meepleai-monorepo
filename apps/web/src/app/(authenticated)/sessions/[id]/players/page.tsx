import { redirect } from 'next/navigation';

/**
 * Session Players sub-route - redirects to session detail page
 * Stub until full session players listing is implemented
 */
export default async function SessionPlayersPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  redirect(`/sessions/${id}`);
}
