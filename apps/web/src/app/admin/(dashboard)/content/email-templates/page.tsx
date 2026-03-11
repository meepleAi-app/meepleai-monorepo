/**
 * Admin Email Templates Page (Issue #52-#56)
 *
 * Server component wrapper with Suspense boundary.
 */

import { Suspense } from 'react';

import { EmailTemplatesContent } from './_content';
import { EmailTemplatesSkeleton } from './_skeleton';

export default function EmailTemplatesPage() {
  return (
    <Suspense fallback={<EmailTemplatesSkeleton />}>
      <EmailTemplatesContent />
    </Suspense>
  );
}
