import { redirect } from 'next/navigation';

export default function ChatHistoryPage() {
  redirect('/admin/agents/usage?tab=chat-log');
}
