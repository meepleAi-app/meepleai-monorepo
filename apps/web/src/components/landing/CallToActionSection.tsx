/**
 * CallToActionSection - Landing Page CTA
 *
 * Issue #2233: Extracted from LandingFooter to be used as page content section.
 * CTA section with gradient background and register/login buttons.
 */

import { Heart } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/button';

export function CallToActionSection() {
  return (
    <section className="py-16 px-4 bg-gradient-to-b from-muted/50 to-muted/80 border-t border-border">
      <div className="container mx-auto max-w-4xl text-center space-y-6 animate-fade-in">
        <div className="inline-flex items-center justify-center px-4 py-2 mb-2 rounded-full bg-primary/10 text-primary text-sm font-semibold">
          🎮 Inizia la tua avventura
        </div>
        <h2 className="font-heading text-3xl md:text-4xl font-bold text-foreground">
          Pronto a iniziare?
        </h2>
        <p
          className="text-muted-foreground text-lg max-w-2xl mx-auto"
          aria-label="Unisciti a migliaia di giocatori che non litigano più sulle regole"
        >
          Unisciti a migliaia di giocatori che non litigano più sulle regole
        </p>
        <div className="flex flex-col sm:flex-row gap-4 justify-center pt-6">
          <Button
            asChild
            size="lg"
            className="bg-gradient-to-r from-primary to-primary/90 hover:from-primary/90 hover:to-primary text-white shadow-xl hover:shadow-2xl hover:scale-105 transition-all duration-300"
          >
            <Link href="/register">
              Registrati Ora
              <Heart className="ml-2 h-5 w-5 fill-current" />
            </Link>
          </Button>
          <Button
            asChild
            variant="outline"
            size="lg"
            className="border-2 hover:bg-primary/5 hover:border-primary/50 transition-all duration-300"
          >
            <Link href="/login">Accedi</Link>
          </Button>
        </div>
      </div>
    </section>
  );
}
