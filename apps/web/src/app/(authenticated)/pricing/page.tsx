/**
 * Pricing Page (Game Night Improvvisata - E2-5)
 *
 * Two-column layout comparing Free and Premium tiers.
 * Premium CTA is disabled ("Coming Soon") until Stripe integration.
 */

'use client';

import React from 'react';

import { PricingCard, type PricingFeature } from '@/components/tier/PricingCard';

// ============================================================================
// Tier Definitions (hardcoded from seed data)
// ============================================================================

const FREE_FEATURES: PricingFeature[] = [
  { label: '3 giochi privati', included: true },
  { label: '2 PDF al mese', included: true },
  { label: '1 agente AI', included: true },
  { label: '20 query AI al giorno', included: true },
  { label: '5 query per sessione', included: true },
  { label: '3 foto per sessione', included: true },
  { label: '2 proposte catalogo a settimana', included: true },
  { label: 'Salvataggio sessione', included: false },
  { label: 'Accesso completo agenti', included: false },
  { label: 'Supporto prioritario', included: false },
];

const PREMIUM_FEATURES: PricingFeature[] = [
  { label: '50 giochi privati', included: true },
  { label: '20 PDF al mese', included: true },
  { label: '10 agenti AI', included: true },
  { label: '200 query AI al giorno', included: true },
  { label: '50 query per sessione', included: true },
  { label: '20 foto per sessione', included: true },
  { label: '10 proposte catalogo a settimana', included: true },
  { label: 'Salvataggio sessione', included: true },
  { label: 'Accesso completo agenti', included: true },
  { label: 'Supporto prioritario', included: true },
];

// ============================================================================
// Page
// ============================================================================

export default function PricingPage() {
  return (
    <div className="container max-w-4xl mx-auto py-12 px-4">
      {/* Header */}
      <div className="text-center mb-10">
        <h1 className="text-3xl font-bold font-quicksand text-foreground">Scegli il tuo Piano</h1>
        <p className="mt-3 text-muted-foreground font-nunito max-w-xl mx-auto">
          Inizia gratis e passa a Premium quando hai bisogno di piu spazio e funzionalita avanzate.
        </p>
      </div>

      {/* Pricing Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-8" data-testid="pricing-grid">
        <PricingCard
          name="Free"
          price="Gratis"
          features={FREE_FEATURES}
          ctaLabel="Inizia Gratis"
          ctaDisabled={false}
        />

        <PricingCard
          name="Premium"
          price="4,99/mese"
          features={PREMIUM_FEATURES}
          isPopular
          ctaLabel="Coming Soon"
          ctaDisabled
        />
      </div>

      {/* Coming Soon Note */}
      <div className="mt-10 text-center">
        <p className="text-sm text-muted-foreground font-nunito">
          I pagamenti saranno disponibili a breve. Il piano Premium e attualmente in fase di
          sviluppo.
        </p>
      </div>
    </div>
  );
}
