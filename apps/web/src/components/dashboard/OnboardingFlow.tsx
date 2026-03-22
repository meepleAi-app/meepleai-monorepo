'use client';

import { useState, useEffect, useCallback } from 'react';

import { Dice5, Search, UserPlus, X } from 'lucide-react';
import Link from 'next/link';

import { IS_ALPHA_MODE } from '@/lib/alpha-mode';
import { cn } from '@/lib/utils';

const STORAGE_KEY = 'meepleai-onboarding-complete';

interface OnboardingStep {
  id: string;
  icon: React.ReactNode;
  title: string;
  description: string;
  href: string;
  ctaLabel: string;
}

const STEPS: OnboardingStep[] = [
  {
    id: 'add-game',
    icon: <Search className="w-6 h-6 text-primary" />,
    title: 'Aggiungi il tuo primo gioco',
    description: 'Cerca nel catalogo e aggiungilo alla tua libreria',
    href: '/games',
    ctaLabel: 'Cerca catalogo',
  },
  {
    id: 'invite-friend',
    icon: <UserPlus className="w-6 h-6 text-primary" />,
    title: 'Invita un amico',
    description: 'Condividi la tua libreria e gioca insieme',
    href: '/players',
    ctaLabel: 'Invita',
  },
  {
    id: 'play',
    icon: <Dice5 className="w-6 h-6 text-primary" />,
    title: 'Gioca!',
    description: 'Crea la tua prima partita e traccia i punteggi',
    href: '/sessions/new',
    ctaLabel: 'Nuova Partita',
  },
];

export function OnboardingFlow() {
  const [showOnboarding, setShowOnboarding] = useState(false);

  useEffect(() => {
    // SSR safety: only read localStorage on client
    const completed = localStorage.getItem(STORAGE_KEY);
    if (!completed) setShowOnboarding(true);
  }, []);

  const handleDismiss = useCallback(() => {
    localStorage.setItem(STORAGE_KEY, 'true');
    setShowOnboarding(false);
  }, []);

  if (!showOnboarding) return null;

  return (
    <div
      data-testid="onboarding-flow"
      className="relative rounded-2xl border border-primary/20 bg-primary/5 p-6 space-y-4"
    >
      <button
        type="button"
        onClick={handleDismiss}
        className="absolute top-3 right-3 p-1.5 rounded-full hover:bg-muted transition-colors"
        aria-label="Chiudi onboarding"
      >
        <X className="w-4 h-4 text-muted-foreground" />
      </button>

      <div className="text-center space-y-1">
        <h2 className="text-xl font-bold font-quicksand">Benvenuto su MeepleAI!</h2>
        <p className="text-sm text-muted-foreground">
          Inizia a costruire la tua esperienza di gioco
        </p>
      </div>

      <div className="grid gap-3 sm:grid-cols-3">
        {(IS_ALPHA_MODE ? STEPS.filter(s => s.id === 'add-game') : STEPS).map(step => (
          <Link
            key={step.id}
            href={step.href}
            className={cn(
              'flex flex-col items-center gap-2 rounded-xl border border-border/50 bg-card p-4 text-center',
              'hover:border-primary/30 hover:shadow-sm transition-all'
            )}
          >
            <div className="flex items-center justify-center w-12 h-12 rounded-full bg-primary/10">
              {step.icon}
            </div>
            <span className="text-sm font-medium text-foreground">{step.title}</span>
            <span className="text-xs text-muted-foreground">{step.description}</span>
            <span className="mt-auto text-xs font-medium text-primary">{step.ctaLabel} →</span>
          </Link>
        ))}
      </div>

      <button
        type="button"
        onClick={handleDismiss}
        className="block mx-auto text-xs text-muted-foreground hover:text-foreground transition-colors"
      >
        Salta, esplorero da solo
      </button>
    </div>
  );
}
