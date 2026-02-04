import type { Metadata } from 'next';

import { PdfTierLimitsClient } from './client';

export const metadata: Metadata = {
  title: 'PDF Tier Upload Limits | Admin Configuration',
  description: 'Configure daily and weekly PDF upload limits per subscription tier',
};

export default function PdfTierLimitsPage() {
  return <PdfTierLimitsClient />;
}
