import { redirect } from 'next/navigation';

export default function SandboxPage() {
  redirect('/admin/agents/playground');
}
