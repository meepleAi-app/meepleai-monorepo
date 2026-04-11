'use client';

import { usePathname, useRouter } from 'next/navigation';

import { entityHsl } from '@/components/ui/data-display/meeple-card';
import { useRecentsStore } from '@/stores/use-recents';

/**
 * RecentsBar — shows recent entity pills in the MiniNav right area.
 * Max 3 pills visible (excludes current page).
 * Hidden on mobile (< md).
 */
export function RecentsBar() {
  const items = useRecentsStore(s => s.items);
  const pathname = usePathname();
  const router = useRouter();

  const visible = items.filter(i => i.href !== pathname).slice(0, 3);

  if (visible.length === 0) return null;

  return (
    <div className="hidden md:flex items-center gap-1.5" data-testid="recents-bar">
      {visible.map(item => {
        // entityHsl returns a full hsl(...) or hsla(...) string
        const color = entityHsl(item.entity);
        const colorWithAlpha = entityHsl(item.entity, 0.1);
        const colorHover = entityHsl(item.entity, 0.4);
        return (
          <button
            key={item.id}
            type="button"
            data-testid={`recent-pill-${item.id}`}
            title={item.title}
            onClick={() => router.push(item.href)}
            aria-label={item.title}
            className="flex h-7 w-7 items-center justify-center rounded-full text-[11px] font-bold transition-all duration-200 hover:scale-110 hover:shadow-md focus-visible:ring-2 focus-visible:ring-current focus-visible:outline-none"
            style={{
              backgroundColor: colorWithAlpha,
              color,
              outline: '1.5px solid transparent',
            }}
            onMouseEnter={e => {
              (e.currentTarget as HTMLElement).style.outlineColor = colorHover;
            }}
            onMouseLeave={e => {
              (e.currentTarget as HTMLElement).style.outlineColor = 'transparent';
            }}
          >
            {item.title.charAt(0).toUpperCase()}
          </button>
        );
      })}
    </div>
  );
}
