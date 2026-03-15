'use client';

import Link from 'next/link';
import { memo } from 'react';

interface DetailLinkBlockProps {
  title: string;
  entityColor: string;
  data: {
    type: 'detailLink';
    href: string;
    label: string;
  };
}

export const DetailLinkBlock = memo(function DetailLinkBlock({
  title,
  entityColor,
  data,
}: DetailLinkBlockProps) {
  const { href, label } = data;

  return (
    <div className="flex flex-col items-center gap-2">
      {title && (
        <h4
          className="text-xs font-semibold uppercase tracking-wide"
          style={{ color: `hsl(${entityColor})` }}
        >
          {title}
        </h4>
      )}

      <Link
        href={href}
        className="inline-flex items-center gap-1 rounded-md border px-3 py-1.5 text-xs font-medium transition-colors hover:opacity-80"
        style={{
          borderColor: `hsl(${entityColor} / 0.5)`,
          color: `hsl(${entityColor})`,
          backgroundColor: `hsl(${entityColor} / 0.06)`,
        }}
      >
        {label}
        <span aria-hidden="true">→</span>
      </Link>
    </div>
  );
});

DetailLinkBlock.displayName = 'DetailLinkBlock';
