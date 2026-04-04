import { redirect } from 'next/navigation';

export default function AbTestNewPage() {
  redirect('/admin/agents/playground?tab=compare');
}
