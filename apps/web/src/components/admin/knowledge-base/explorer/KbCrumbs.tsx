'use client';

import { usePathname } from 'next/navigation';

const KB_BASE = '/admin/knowledge-base';

const LABELS: ReadonlyArray<readonly [string, string]> = [
  [`${KB_BASE}/vectors`, 'Vector Collections'],
  [`${KB_BASE}/queue`, 'Processing Queue'],
  [`${KB_BASE}/pipeline`, 'RAG Pipeline'],
  [`${KB_BASE}/embedding`, 'Embedding'],
  [`${KB_BASE}/feedback`, 'Feedback'],
  [`${KB_BASE}/settings`, 'Settings'],
  [`${KB_BASE}/snapshots`, 'Snapshots'],
  [`${KB_BASE}/upload`, 'Upload'],
];

function labelFor(pathname: string): string {
  if (pathname === KB_BASE) return 'Explorer';
  const hit = LABELS.find(([href]) => pathname === href || pathname.startsWith(`${href}/`));
  return hit ? hit[1] : 'Explorer';
}

export function KbCrumbs() {
  const pathname = usePathname();
  return (
    <div className="font-mono text-[11px] text-muted-foreground mt-0.5">
      Admin · KB · {labelFor(pathname)}
    </div>
  );
}
