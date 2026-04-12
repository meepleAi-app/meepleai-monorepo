'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { useNavBreadcrumb } from '@/lib/hooks/useNavBreadcrumb';
import { cn } from '@/lib/utils';
import { resolveCTA } from '@/lib/utils/nav-cta';

export function ActionPill({ className }: { className?: string }) {
  const pathname = usePathname();
  const segments = useNavBreadcrumb();
  const cta = resolveCTA(pathname);

  if (segments.length === 0 && !cta) return null;

  return (
    <div
      data-testid="action-pill"
      className={cn(
        'fixed bottom-6 left-1/2 -translate-x-1/2 z-50',
        'hidden lg:flex items-center gap-3',
        'px-4 py-2 rounded-full',
        'bg-[rgba(15,20,40,0.82)] backdrop-blur-[12px]',
        'shadow-[0_8px_32px_rgba(0,0,0,0.4)] border border-white/8',
        className
      )}
    >
      {segments.length > 0 && (
        <nav aria-label="Breadcrumb">
          <ol className="flex items-center gap-1 m-0 p-0 list-none">
            {segments.map((seg, i) => (
              <li key={seg.href} className="flex items-center gap-1">
                {i > 0 && (
                  <span className="text-white/22 text-[8.5px]" aria-hidden="true">
                    ›
                  </span>
                )}
                <Link
                  href={seg.href}
                  className="text-[10.5px] font-semibold transition-colors hover:opacity-90"
                  style={{
                    color:
                      i === segments.length - 1 && seg.color ? seg.color : 'rgba(255,255,255,0.55)',
                  }}
                  aria-current={i === segments.length - 1 ? 'page' : undefined}
                >
                  {seg.label}
                </Link>
              </li>
            ))}
          </ol>
        </nav>
      )}

      {cta && (
        <Link
          href={cta.href}
          data-testid="action-pill-cta"
          className={cn(
            'px-3 py-[5px] rounded-full text-[10.5px] font-bold text-white',
            'bg-[hsl(25,95%,45%)] hover:bg-[hsl(25,95%,40%)]',
            'shadow-[0_2px_8px_hsla(25,95%,45%,0.4)] transition-colors'
          )}
        >
          {cta.label}
        </Link>
      )}
    </div>
  );
}
