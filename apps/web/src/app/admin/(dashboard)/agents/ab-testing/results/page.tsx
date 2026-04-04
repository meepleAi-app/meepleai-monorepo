import { redirect } from 'next/navigation';

export default function AbTestResultsPage() {
  redirect('/admin/agents/playground?tab=compare');
}
