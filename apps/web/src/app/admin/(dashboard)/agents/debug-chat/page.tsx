import { redirect } from 'next/navigation';

export default function DebugChatPage() {
  redirect('/admin/agents/playground?tab=chat');
}
