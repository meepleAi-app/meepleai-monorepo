/**
 * @mockup admin-mockups/design_files/sp4-kb-detail.html
 *
 * #2311 DEC-D1: the mockup header declares `Route: /kb/[id]`, but the canonical
 * implementation lives at `/knowledge-base/[id]` (annotation @mockup wired,
 * MOCKUPS_INDEX entry, breadcrumb + useRecentsStore, PR #2310 KB row
 * navigation map). This thin redirect lets the mockup-shorthand path still
 * land on the canonical surface without forking the route.
 */

import { redirect } from 'next/navigation';

export const dynamic = 'force-static';

export default async function KbShorthandRedirectPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  redirect(`/knowledge-base/${encodeURIComponent(id)}`);
}
