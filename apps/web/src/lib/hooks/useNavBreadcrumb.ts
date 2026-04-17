'use client';

import { usePathname } from 'next/navigation';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';
import { entityHsl } from '@/components/ui/data-display/meeple-card/tokens';
export interface BreadcrumbSegment {
  label: string;
  href: string;
  entityType?: MeepleEntityType;
  color?: string;
}

/** @deprecated card-hand-store removed — this hook is dead code, slated for removal */
interface CardHandEntry {
  href: string;
  label: string;
  entityType: MeepleEntityType;
}

const ROUTE_LABELS: Record<string, string> = {
  '/dashboard': 'Dashboard',
  '/library': 'Libreria',
  '/sessions': 'Sessioni',
  '/agents': 'Agenti',
  '/toolkit': 'Toolkit',
  '/chat': 'Chat',
};

export function useNavBreadcrumb(): BreadcrumbSegment[] {
  const pathname = usePathname();
  const cards: CardHandEntry[] = [];

  const segments: BreadcrumbSegment[] = [];
  const parts = pathname.split('/').filter(Boolean);
  let accumulated = '';

  for (const part of parts) {
    accumulated = `${accumulated}/${part}`;
    const card = cards.find(c => c.href === accumulated || pathname.startsWith(c.href + '/'));
    if (card && !segments.find(s => s.href === accumulated)) {
      segments.push({
        label: card.label,
        href: accumulated,
        entityType: card.entityType,
        color: entityHsl(card.entityType),
      });
    } else if (ROUTE_LABELS[accumulated] && !segments.find(s => s.href === accumulated)) {
      segments.push({ label: ROUTE_LABELS[accumulated], href: accumulated });
    }
  }

  if (segments.length === 0 && ROUTE_LABELS[pathname]) {
    segments.push({ label: ROUTE_LABELS[pathname], href: pathname });
  }

  return segments;
}
