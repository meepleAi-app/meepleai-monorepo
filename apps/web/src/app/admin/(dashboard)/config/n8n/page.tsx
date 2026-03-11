import { Suspense } from 'react';

import { N8nConfigContent } from './_content';
import { N8nConfigSkeleton } from './_skeleton';

export const metadata = {
  title: 'n8n Workflows | Admin',
};

export default function N8nConfigPage() {
  return (
    <Suspense fallback={<N8nConfigSkeleton />}>
      <N8nConfigContent />
    </Suspense>
  );
}
