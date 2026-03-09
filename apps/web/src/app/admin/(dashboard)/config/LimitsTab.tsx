'use client';

import { PdfLimitsConfig } from '@/components/admin/PdfLimitsConfig';

export function LimitsTab() {
  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold text-foreground">
          Upload &amp; Library Limits
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Configure PDF upload limits, game library limits, and tier-based restrictions.
        </p>
      </div>
      <PdfLimitsConfig />
    </div>
  );
}
