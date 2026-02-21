/**
 * Redirect: /toolkit/new-demo → /toolkit-demo
 *
 * The demo was moved out of /toolkit/ to avoid matching the [sessionId]
 * dynamic route, which triggered real API/SSE connections.
 */
import { redirect } from 'next/navigation';

export default function ToolkitNewDemoRedirect() {
  redirect('/toolkit-demo');
}
