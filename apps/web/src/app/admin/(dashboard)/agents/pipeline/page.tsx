import { redirect } from 'next/navigation';

export default function PipelineExplorerPage() {
  redirect('/admin/agents/inspector?tab=pipeline');
}
