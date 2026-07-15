/**
 * /knowledge-base/global — Route shell
 * Issue #1482 Task 6
 *
 * Thin server component wrapping the client orchestrator in a Suspense
 * boundary. Required by Next.js App Router: the orchestrator uses
 * `useSearchParams()` which opts the subtree out of SSR static prerendering;
 * without Suspense the build emits a CSR bailout warning.
 *
 * @see KbGlobaleView — client orchestrator (URL SSOT, branch logic, hooks)
 */

import { type JSX, Suspense } from 'react';

import { KbGlobaleView } from './_components/KbGlobaleView';

export default function KnowledgeBaseGlobalPage(): JSX.Element {
  return (
    <Suspense fallback={null}>
      <KbGlobaleView />
    </Suspense>
  );
}
