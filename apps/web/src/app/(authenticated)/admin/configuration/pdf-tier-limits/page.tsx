import { PdfTierLimitsClient } from './client';

import type { Metadata } from 'next';


export const metadata: Metadata = {
  title: 'PDF Tier Upload Limits | Admin Configuration',
  description: 'Configure daily and weekly PDF upload limits per subscription tier',
};

export default function PdfTierLimitsPage() {
  return <PdfTierLimitsClient />;
}
