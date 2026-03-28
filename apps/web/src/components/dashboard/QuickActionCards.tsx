'use client';

import React from 'react';

import { Dice5, MessageCircle, Search } from 'lucide-react';
import Link from 'next/link';

import { GlassCard } from '@/components/ui/surfaces/GlassCard';

interface QuickAction {
  href: string;
  label: string;
  description: string;
  icon: React.ElementType;
  iconColor: string;
}

const actions: QuickAction[] = [
  {
    href: '/sessions/new',
    label: 'Nuova Serata',
    description: 'Organizza una partita',
    icon: Dice5,
    iconColor: 'text-amber-400',
  },
  {
    href: '/chat',
    label: 'Chat AI',
    description: 'Chiedi alle regole',
    icon: MessageCircle,
    iconColor: 'text-purple-400',
  },
];

export interface QuickActionCardsProps {
  onSearchClick?: () => void;
}

export function QuickActionCards({ onSearchClick }: QuickActionCardsProps) {
  return (
    <section className="grid grid-cols-2 gap-3 px-4">
      {actions.map(action => {
        const Icon = action.icon;
        return (
          <Link key={action.href} href={action.href}>
            <GlassCard className="flex flex-col gap-2 p-4">
              <Icon className={`h-6 w-6 ${action.iconColor}`} />
              <div>
                <p className="text-sm font-semibold text-[var(--gaming-text-primary)]">
                  {action.label}
                </p>
                <p className="text-xs text-[var(--gaming-text-secondary)]">{action.description}</p>
              </div>
            </GlassCard>
          </Link>
        );
      })}
      <button type="button" onClick={onSearchClick} className="col-span-2">
        <GlassCard className="flex items-center gap-3 p-4 text-left">
          <Search className="h-6 w-6 text-blue-400" />
          <div>
            <p className="text-sm font-semibold text-[var(--gaming-text-primary)]">Esplora</p>
            <p className="text-xs text-[var(--gaming-text-secondary)]">Cerca nel catalogo BGG</p>
          </div>
        </GlassCard>
      </button>
    </section>
  );
}
