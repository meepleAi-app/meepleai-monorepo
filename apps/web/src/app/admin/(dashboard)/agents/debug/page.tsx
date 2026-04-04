import { redirect } from 'next/navigation';

export default function DebugConsolePage() {
  redirect('/admin/agents/inspector');
}
