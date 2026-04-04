import { redirect } from 'next/navigation';

export default function ChatLimitsPage() {
  redirect('/admin/agents/config?tab=limits');
}
