/**
 * GreetingSection Component - Dashboard Personalized Greeting
 *
 * Issue #1836: PAGE-002 - Dashboard Page
 *
 * Displays personalized welcome message with user's display name.
 * Includes time-based greeting (Buongiorno, Buonasera) and contextual subtitle.
 *
 * Features:
 * - Time-based greeting (Italian)
 * - User display name fallback to email
 * - Responsive typography
 * - Quicksand font for heading
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md
 */

import React from 'react';

import type { AuthUser } from '@/types';

export interface GreetingSectionProps {
  /** Authenticated user data */
  user: AuthUser;
}

/**
 * Get time-based greeting in Italian
 */
function getGreeting(): string {
  const hour = new Date().getHours();

  if (hour >= 5 && hour < 12) {
    return 'Buongiorno';
  } else if (hour >= 12 && hour < 18) {
    return 'Buon pomeriggio';
  } else if (hour >= 18 && hour < 22) {
    return 'Buonasera';
  } else {
    return 'Buonanotte';
  }
}

/**
 * GreetingSection Component
 */
export function GreetingSection({ user }: GreetingSectionProps) {
  const greeting = getGreeting();
  const displayName = user.displayName || user.email.split('@')[0];

  return (
    <section className="space-y-2" aria-label="Greeting" data-testid="dashboard-greeting">
      <h1 className="text-3xl font-quicksand font-bold text-foreground">
        {greeting}, {displayName}! 👋
      </h1>
      <p className="text-muted-foreground">Benvenuto nel tuo dashboard. Ecco cosa c'è di nuovo.</p>
    </section>
  );
}
