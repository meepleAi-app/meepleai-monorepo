/**
 * How It Works Section - Landing Page
 *
 * Three-step process explaining how to use MeepleAI.
 * Design: Playful Boardroom (wireframes-playful-boardroom.md)
 *
 * Features:
 * - Numbered steps (1-2-3)
 * - Visual flow with connecting lines (desktop)
 * - Clear action-oriented copy
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md (lines 81-87)
 */

import { ArrowRight } from 'lucide-react';

const steps = [
  {
    number: '1',
    emoji: '🎲',
    title: 'Scegli il gioco',
    description: 'Cerca tra migliaia di giochi o carica il tuo manuale',
  },
  {
    number: '2',
    emoji: '💬',
    title: 'Fai una domanda',
    description: 'Chiedi in linguaggio naturale, come parleresti a un amico',
  },
  {
    number: '3',
    emoji: '✨',
    title: 'Ottieni risposta citata',
    description: 'Ricevi risposte accurate con riferimenti al manuale ufficiale',
  },
];

export function HowItWorksSection() {
  return (
    <section className="py-16 md:py-24 px-4 bg-background">
      <div className="container mx-auto max-w-6xl">
        {/* Section Header */}
        <div className="text-center mb-12">
          <h2 className="font-heading text-3xl md:text-4xl font-bold text-foreground mb-4">
            Come funziona
          </h2>
          <div className="w-20 h-1 bg-primary mx-auto rounded-full" />
        </div>

        {/* Steps */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-8 md:gap-12 relative">
          {steps.map((step, index) => (
            <div key={index} className="relative">
              {/* Connecting Arrow (Desktop only) */}
              {index < steps.length - 1 && (
                <div className="hidden md:block absolute top-16 left-full w-full -translate-x-1/2 z-0">
                  <ArrowRight className="h-8 w-8 text-primary/30 mx-auto" />
                </div>
              )}

              {/* Step Card */}
              <div className="relative z-10 text-center space-y-4">
                {/* Number Badge */}
                <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-primary text-white font-bold text-2xl shadow-lg">
                  {step.number}
                </div>

                {/* Emoji */}
                <div className="text-5xl" role="img" aria-label={step.title}>
                  {step.emoji}
                </div>

                {/* Title */}
                <h3 className="font-heading text-xl md:text-2xl font-semibold text-foreground">
                  {step.title}
                </h3>

                {/* Description */}
                <p className="text-muted-foreground leading-relaxed max-w-xs mx-auto">
                  {step.description}
                </p>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
