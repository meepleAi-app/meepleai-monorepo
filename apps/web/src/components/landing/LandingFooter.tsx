/**
 * Landing Footer - Landing Page
 *
 * Footer with CTA buttons and legal links.
 * Design: Playful Boardroom (wireframes-playful-boardroom.md)
 *
 * Features:
 * - Primary CTA: Registrati Ora
 * - Secondary CTA: Accedi
 * - Legal links: Privacy, Termini, API Docs
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md (lines 88-91)
 */

import Link from 'next/link';
import { Button } from '@/components/ui/button';

export function LandingFooter() {
  return (
    <footer className="bg-muted/50 border-t border-border">
      {/* CTA Section */}
      <div className="py-12 px-4">
        <div className="container mx-auto max-w-4xl text-center space-y-6">
          <h2 className="font-heading text-2xl md:text-3xl font-bold text-foreground">
            Pronto a iniziare?
          </h2>
          <p className="text-muted-foreground text-lg">
            Unisciti a migliaia di giocatori che non litigano più sulle regole
          </p>
          <div className="flex flex-col sm:flex-row gap-4 justify-center pt-4">
            <Button asChild size="lg" className="bg-primary hover:bg-primary/90">
              <Link href="/register">Registrati Ora</Link>
            </Button>
            <Button asChild variant="outline" size="lg">
              <Link href="/login">Accedi</Link>
            </Button>
          </div>
        </div>
      </div>

      {/* Legal Links */}
      <div className="border-t border-border py-6 px-4">
        <div className="container mx-auto max-w-6xl">
          <div className="flex flex-col md:flex-row justify-between items-center gap-4 text-sm text-muted-foreground">
            <p>© {new Date().getFullYear()} MeepleAI. Tutti i diritti riservati.</p>
            <nav className="flex gap-6">
              <Link href="/privacy" className="hover:text-primary transition-colors">
                Privacy
              </Link>
              <Link href="/terms" className="hover:text-primary transition-colors">
                Termini
              </Link>
              <Link href="/api/docs" className="hover:text-primary transition-colors">
                API Docs
              </Link>
            </nav>
          </div>
        </div>
      </div>
    </footer>
  );
}
