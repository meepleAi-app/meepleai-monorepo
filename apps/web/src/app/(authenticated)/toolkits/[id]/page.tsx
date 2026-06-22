/**
 * /toolkits/[id] — Stage 3 cluster page (Issue #1145).
 *
 * Server boundary: extracts `id` from params, wraps the client orchestrator
 * in a Suspense boundary (required for `useSearchParams()` SSR opt-out per
 * Next.js App Router pattern; same lesson learned in #1147 discover FE).
 *
 * Pattern reference: /players/[id]/page.tsx (#1138).
 */

import { Suspense } from 'react';

import { ToolkitDetailView } from './_components/ToolkitDetailView';

interface PageProps {
  readonly params: Promise<{ id: string }>;
}

export default async function ToolkitDetailPage({ params }: PageProps) {
  const { id } = await params;
  return (
    <Suspense fallback={null}>
      <ToolkitDetailView toolkitId={id} />
    </Suspense>
  );
}
