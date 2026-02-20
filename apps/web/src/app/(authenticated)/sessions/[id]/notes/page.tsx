import { redirect } from 'next/navigation';

/**
 * Session Notes sub-route - redirects to session detail page
 * Stub until full session notes page is implemented
 */
export default async function SessionNotesPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  redirect(`/sessions/${id}`);
}
