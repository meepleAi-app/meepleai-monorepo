/**
 * Pricing Page
 *
 * 3-tier pricing (Free / Pro / Team) built with v2 primitives.
 * Source of truth: admin-mockups/design_files/public.jsx PricingPage.
 */

'use client';

import { useRouter } from 'next/navigation';

import { Button } from '@/components/ui/primitives/button';
import { HeroGradient } from '@/components/ui/v2/hero-gradient';
import { PricingCard, type PricingCardProps } from '@/components/ui/v2/pricing-card/pricing-card';

type Tier = Omit<PricingCardProps, 'className'>;

const TIERS: readonly Tier[] = [
  {
    tier: 'Free',
    price: '€0',
    description: 'Perfetto per esplorare MeepleAI con i tuoi giochi preferiti.',
    features: [
      { label: '2 giochi salvati', included: true },
      { label: '10 chat al mese', included: true },
      { label: 'Session mode base', included: true },
      { label: 'Accesso community catalog', included: true },
      { label: 'Support community', included: true },
    ],
    cta: { label: 'Inizia gratis', href: '/register' },
  },
  {
    tier: 'Pro',
    price: '€9/mese',
    description: 'Per il boardgamer serio che gioca spesso e vuole il massimo.',
    highlighted: true,
    features: [
      { label: 'Giochi illimitati', included: true },
      { label: 'Chat illimitate', included: true },
      { label: 'RAG hybrid search', included: true },
      { label: 'Multi-agent completo', included: true },
      { label: 'Game nights & diary', included: true },
      { label: 'Priority support', included: true },
    ],
    cta: { label: 'Scegli Pro', href: '/register?plan=pro' },
  },
  {
    tier: 'Team',
    price: '€29/mese',
    description: 'Per boardgame café, club e gruppi organizzati.',
    features: [
      { label: '5 account inclusi', included: true },
      { label: 'KB condivisa del team', included: true },
      { label: 'Admin panel', included: true },
      { label: 'Game nights multi-account', included: true },
      { label: 'Dedicated support', included: true },
      { label: 'Early access funzionalità', included: true },
    ],
    cta: { label: 'Contattaci', href: '/contact' },
  },
];

export default function PricingPage() {
  const router = useRouter();

  return (
    <div className="min-h-dvh bg-background">
      <HeroGradient
        title={
          <>
            Semplice, trasparente,
            <br />
            <span style={{ color: 'hsl(var(--e-game))' }}>board-game friendly</span>
          </>
        }
        subtitle="Nessun contratto. Cancella quando vuoi. Il piano Free rimane gratuito per sempre."
        className="py-12 md:py-16"
      />
      <div className="max-w-6xl mx-auto py-12 px-4">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 md:gap-8 items-stretch">
          {TIERS.map(tier => (
            <PricingCard key={tier.tier} {...tier} />
          ))}
        </div>

        <p className="mt-12 text-center text-sm text-muted-foreground max-w-2xl mx-auto">
          Tutti i piani includono aggiornamenti gratuiti. Cambi idea? Puoi passare da un piano
          all&apos;altro in qualsiasi momento.
        </p>

        <div className="mt-12 flex flex-col sm:flex-row justify-between items-center gap-4 pt-8 border-t border-border">
          <Button variant="ghost" onClick={() => router.push('/')}>
            ← Torna alla Home
          </Button>
          <Button variant="outline" onClick={() => router.push('/faq')}>
            Domande frequenti →
          </Button>
        </div>
      </div>
    </div>
  );
}
