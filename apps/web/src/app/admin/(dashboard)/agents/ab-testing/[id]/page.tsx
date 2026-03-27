import { redirect } from 'next/navigation';

export default function AbTestDetailPage() {
  redirect('/admin/agents/playground?tab=compare');
}
