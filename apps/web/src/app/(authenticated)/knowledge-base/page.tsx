import { redirect } from 'next/navigation';

/**
 * Knowledge Base index — no standalone listing page exists.
 * Knowledge bases are per-game, accessible from the library.
 */
export default function KnowledgeBasePage() {
  redirect('/library');
}
