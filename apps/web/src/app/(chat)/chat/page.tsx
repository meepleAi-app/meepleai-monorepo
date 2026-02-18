import { redirect } from 'next/navigation';

/**
 * Chat base route - redirects to new chat
 */
export default function ChatBasePage() {
  redirect('/chat/new');
}
