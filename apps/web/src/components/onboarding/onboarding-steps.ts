import { Library, Play, Compass, UserCircle, type LucideIcon } from 'lucide-react';

export interface OnboardingStep {
  id: 'hasGames' | 'hasSessions' | 'hasVisitedDiscover' | 'hasCompletedProfile';
  title: string;
  description: string;
  href: string;
  icon: LucideIcon;
  source: 'backend' | 'localStorage';
}

export const ONBOARDING_STEPS: OnboardingStep[] = [
  {
    id: 'hasGames',
    title: 'Aggiungi il primo gioco',
    description: 'Cerca nel catalogo o aggiungine uno manualmente',
    href: '/library?action=add',
    icon: Library,
    source: 'backend',
  },
  {
    id: 'hasSessions',
    title: 'Crea una sessione',
    description: 'Registra la tua prima partita',
    href: '/sessions/new',
    icon: Play,
    source: 'backend',
  },
  {
    id: 'hasVisitedDiscover',
    title: 'Esplora il catalogo',
    description: 'Scopri giochi dalla community',
    href: '/discover',
    icon: Compass,
    source: 'localStorage',
  },
  {
    id: 'hasCompletedProfile',
    title: 'Completa il profilo',
    description: 'Aggiungi un avatar e una bio',
    href: '/profile',
    icon: UserCircle,
    source: 'backend',
  },
];

export const TOTAL_STEPS = ONBOARDING_STEPS.length;
