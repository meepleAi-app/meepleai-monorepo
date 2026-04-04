import { type ReactNode, Suspense } from 'react';

import { EmbeddingFlowBanner } from '@/components/ui/admin/embedding-flow-banner';

export default function QueueLayout({ children }: { children: ReactNode }) {
  return (
    <>
      <Suspense fallback={null}>
        <EmbeddingFlowBanner currentStep="queue" />
      </Suspense>
      {children}
    </>
  );
}
