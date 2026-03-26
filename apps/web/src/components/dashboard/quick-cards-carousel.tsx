'use client';

import { useRouter } from 'next/navigation';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';
import { useCardHand } from '@/stores/use-card-hand';

interface QuickCardItem {
  id: string;
  title: string;
  subtitle?: string;
  imageUrl?: string;
  entity: MeepleEntityType;
  href?: string;
}

interface QuickCardsCarouselProps {
  items: QuickCardItem[];
}

export function QuickCardsCarousel({ items }: QuickCardsCarouselProps) {
  const { drawCard } = useCardHand();
  const router = useRouter();

  function handleTap(item: QuickCardItem) {
    const href = item.href ?? getDefaultHref(item);
    drawCard({
      id: item.id,
      entity: item.entity,
      title: item.title,
      href,
      imageUrl: item.imageUrl,
    });
    router.push(href);
  }

  return (
    <div
      data-testid="quick-cards-carousel"
      className="flex gap-3 overflow-x-auto pb-2 scrollbar-none snap-x snap-mandatory"
    >
      {items.map(item => (
        <button
          key={item.id}
          type="button"
          onClick={() => handleTap(item)}
          className="flex-shrink-0 snap-start w-[140px] rounded-2xl border border-border/30 bg-card/60 backdrop-blur-sm p-3 text-left transition-all hover:shadow-md hover:-translate-y-0.5 active:scale-[0.97]"
        >
          {item.imageUrl && (
            <img
              src={item.imageUrl}
              alt=""
              className="w-full h-[80px] rounded-xl object-cover mb-2"
            />
          )}
          <p className="font-quicksand text-sm font-bold text-foreground truncate">{item.title}</p>
          {item.subtitle && (
            <p className="font-nunito text-[10px] text-muted-foreground truncate">
              {item.subtitle}
            </p>
          )}
        </button>
      ))}
    </div>
  );
}

function getDefaultHref(item: QuickCardItem): string {
  switch (item.entity) {
    case 'game':
      return `/library/games/${item.id}`;
    case 'agent':
      return `/agents/${item.id}`;
    case 'session':
      return `/sessions/${item.id}`;
    case 'chatSession':
      return `/chat`;
    default:
      return `/library`;
  }
}
