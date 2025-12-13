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
import { useTranslation } from '@/hooks/useTranslation';

export function HeroSection() {
  const { t } = useTranslation();

  const scrollToFeatures = () => {
    const featuresSection = document.getElementById('features');
    featuresSection?.scrollIntoView({ behavior: 'smooth' });
  };

  return (
    <section className="relative min-h-[calc(100vh-56px)] flex items-center justify-center px-4 py-12 md:py-20 overflow-hidden bg-gradient-to-b">
      {/* Animated gradient background */}
      <div className="absolute inset-0 bg-gradient-to-br from-primary/5 via-background to-secondary/5 animate-pulse-slow" />
      <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_right,_var(--tw-gradient-stops))] from-accent/10 via-transparent to-transparent" />

      {/* Decorative blobs */}
      <div className="absolute top-20 -left-20 w-72 h-72 bg-primary/10 rounded-full blur-3xl animate-pulse-slow" />
      <div className="absolute bottom-20 -right-20 w-96 h-96 bg-secondary/10 rounded-full blur-3xl animate-pulse-slow delay-1000" />

      <div className="container mx-auto max-w-6xl relative z-10">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-12 items-center">
          {/* Left Column: Content */}
          <div className="text-center md:text-left space-y-6 animate-fade-in">
            {/* MeepleAvatar - Mobile only */}
            <div className="md:hidden flex justify-center mb-8 animate-scale-in">
              <div className="relative">
                <MeepleAvatar state="confident" size="lg" className="w-48 h-48 drop-shadow-2xl" />
                <div className="absolute inset-0 bg-primary/20 rounded-full blur-2xl -z-10 animate-pulse-slow" />
              </div>
            </div>

            {/* Heading */}
            <h1 className="font-heading text-4xl md:text-5xl lg:text-6xl font-bold leading-tight animate-slide-up bg-gradient-to-r from-primary via-primary/90 to-secondary bg-clip-text text-transparent">
              {t('home.hero.title')}
            </h1>

            {/* Subheading */}
            <p className="text-lg md:text-xl text-muted-foreground max-w-lg mx-auto md:mx-0 animate-slide-up delay-100">
              {t('home.hero.subtitle')}
            </p>

            {/* Trust indicators */}
            <div className="flex items-center justify-center md:justify-start gap-6 text-sm text-muted-foreground pt-2 animate-slide-up delay-200">
              <div className="flex items-center gap-2">
                <span className="text-2xl">✨</span>
                <span>{t('home.hero.trustIndicators.users')}</span>
              </div>
              <div className="flex items-center gap-2">
                <span className="text-2xl">🎯</span>
                <span>{t('home.hero.trustIndicators.accuracy')}</span>
              </div>
            </div>

            {/* CTA Buttons */}
            <div className="flex flex-col sm:flex-row gap-4 justify-center md:justify-start pt-4 animate-slide-up delay-300">
              <Button
                asChild
                size="lg"
                className="bg-gradient-to-r from-primary to-primary/90 hover:from-primary/90 hover:to-primary text-white font-semibold shadow-xl hover:shadow-2xl hover:scale-105 transition-all duration-300"
              >
                <Link href="/register">
                  {t('home.hero.cta.getStarted')}
                  <ArrowRight className="ml-2 h-5 w-5 group-hover:translate-x-1 transition-transform" />
                </Link>
              </Button>

              <Button
                variant="outline"
                size="lg"
                onClick={scrollToFeatures}
                className="group font-semibold border-2 hover:bg-primary/5 hover:border-primary/50 transition-all duration-300"
                aria-label={t('home.hero.scrollToFeatures')}
              >
                {t('home.hero.cta.learnMore')}
                <ChevronDown className="ml-2 h-5 w-5 group-hover:translate-y-1 transition-transform" />
              </Button>
            </div>
          </div>

          {/* Right Column: MeepleAvatar (Desktop) */}
          <div className="hidden md:flex justify-center items-center animate-scale-in">
            <div className="relative group">
              <MeepleAvatar
                state="confident"
                size="lg"
                className="w-72 h-72 lg:w-80 lg:h-80 drop-shadow-2xl transition-transform duration-500 group-hover:scale-105"
              />
              {/* Decorative background with multiple layers */}
              <div className="absolute inset-0 bg-gradient-to-br from-primary/20 via-secondary/10 to-accent/20 rounded-full blur-3xl -z-10 scale-110 animate-pulse-slow" />
              <div className="absolute inset-0 bg-primary/10 rounded-full blur-2xl -z-10 scale-125 group-hover:scale-150 transition-transform duration-700" />
            </div>
          </div>
        </div>
      </div>

      {/* Scroll indicator */}
      <div className="absolute bottom-8 left-1/2 -translate-x-1/2 hidden md:block animate-bounce">
        <button
          onClick={scrollToFeatures}
          className="text-muted-foreground hover:text-primary transition-colors p-2 hover:bg-primary/10 rounded-full"
          aria-label={t('home.hero.scrollToFeatures')}
        >
          <ChevronDown className="h-8 w-8" />
        </button>
      </div>
    </section>
  );
}
