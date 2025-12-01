'use client';

/**
 * Hero Section - Landing Page
 *
 * Marketing hero section with MeepleAvatar, CTA buttons, and responsive layout.
 * Design: Playful Boardroom (wireframes-playful-boardroom.md)
 *
 * Features:
 * - Mobile: Single column, centered MeepleAvatar
 * - Desktop: Two column layout (content + visual)
 * - Primary CTA → /register
 * - Secondary CTA → scroll to features
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md (lines 33-111)
 */

import Link from 'next/link';
import { MeepleAvatar } from '@/components/ui/meeple-avatar';
import { Button } from '@/components/ui/button';
import { ArrowRight, ChevronDown } from 'lucide-react';

export function HeroSection() {
  const scrollToFeatures = () => {
    const featuresSection = document.getElementById('features');
    featuresSection?.scrollIntoView({ behavior: 'smooth' });
  };

  return (
    <section className="relative min-h-[calc(100vh-56px)] flex items-center justify-center px-4 py-12 md:py-20 bg-gradient-to-b from-background to-background/95">
      <div className="container mx-auto max-w-6xl">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-12 items-center">
          {/* Left Column: Content */}
          <div className="text-center md:text-left space-y-6">
            {/* MeepleAvatar - Mobile only */}
            <div className="md:hidden flex justify-center mb-8">
              <MeepleAvatar state="confident" size="lg" className="w-48 h-48" />
            </div>

            {/* Heading */}
            <h1 className="font-heading text-4xl md:text-5xl lg:text-6xl font-bold text-foreground leading-tight">
              Il tuo assistente AI
              <br />
              <span className="text-primary">per giochi da tavolo</span>
            </h1>

            {/* Subheading */}
            <p className="text-lg md:text-xl text-muted-foreground max-w-lg mx-auto md:mx-0">
              Risposte immediate alle regole, in italiano. Sempre con te.
            </p>

            {/* CTA Buttons */}
            <div className="flex flex-col sm:flex-row gap-4 justify-center md:justify-start pt-4">
              <Button
                asChild
                size="lg"
                className="bg-primary hover:bg-primary/90 text-white font-semibold shadow-lg hover:shadow-xl transition-all"
              >
                <Link href="/register">
                  Inizia Gratis
                  <ArrowRight className="ml-2 h-5 w-5" />
                </Link>
              </Button>

              <Button
                variant="ghost"
                size="lg"
                onClick={scrollToFeatures}
                className="group font-semibold"
                aria-label="Scorri alla sezione caratteristiche"
              >
                Scopri di più
                <ChevronDown className="ml-2 h-5 w-5 group-hover:translate-y-1 transition-transform" />
              </Button>
            </div>
          </div>

          {/* Right Column: MeepleAvatar (Desktop) */}
          <div className="hidden md:flex justify-center items-center">
            <div className="relative">
              <MeepleAvatar state="confident" size="lg" className="w-72 h-72 lg:w-80 lg:h-80" />
              {/* Decorative background */}
              <div className="absolute inset-0 bg-primary/5 rounded-full blur-3xl -z-10 scale-110" />
            </div>
          </div>
        </div>
      </div>

      {/* Scroll indicator */}
      <div className="absolute bottom-8 left-1/2 -translate-x-1/2 hidden md:block">
        <button
          onClick={scrollToFeatures}
          className="animate-bounce text-muted-foreground hover:text-primary transition-colors"
          aria-label="Scorri alle caratteristiche"
        >
          <ChevronDown className="h-8 w-8" />
        </button>
      </div>
    </section>
  );
}
