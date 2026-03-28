import { redirect } from 'next/navigation';

export default function AgentModelsPage() {
  redirect('/admin/agents/config?tab=models');
}
