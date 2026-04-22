import { HeroGradient } from '@/components/ui/v2/hero-gradient';

export function WelcomeHero() {
  return (
    <HeroGradient
      title={
        <>
          <span className="block text-sm uppercase tracking-widest text-muted-foreground mb-4 font-normal">
            Il tuo compagno di gioco AI
          </span>
          <span className="block">Ogni serata giochi merita un arbitro</span>
        </>
      }
      subtitle="Setup, regole, punteggi, dispute — un agente AI che conosce il tuo gioco e vi aiuta al tavolo."
      primaryCta={{ label: 'Inizia gratis', href: '/register' }}
      secondaryCta={{ label: 'Scopri come funziona ↓', href: '#come-funziona' }}
      className="min-h-[80vh] flex flex-col items-center justify-center"
    />
  );
}
