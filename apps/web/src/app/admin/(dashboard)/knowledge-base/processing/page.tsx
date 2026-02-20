import { redirect } from 'next/navigation';

/**
 * Processing Queue - redirects to the existing queue page
 * Alias route: /admin/knowledge-base/processing → /admin/knowledge-base/queue
 */
export default function ProcessingQueuePage() {
  redirect('/admin/knowledge-base/queue');
}
