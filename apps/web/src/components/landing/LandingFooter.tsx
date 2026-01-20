/**
 * Landing Footer - Landing Page
 *
 * Footer with CTA buttons and legal links.
 */

import { Github, Twitter, Mail, Heart } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';

export function LandingFooter() {
  const year = new Date().getFullYear();

  return (
    <footer className="bg-gradient-to-b from-muted/50 to-muted/80 border-t border-border relative overflow-hidden">
      {/* Decorative elements */}
      <div className="absolute top-0 left-0 w-full h-px bg-gradient-to-r from-transparent via-primary/50 to-transparent" />

      {/* CTA Section */}
      <div className="py-16 px-4 relative z-10">
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
      </div>

      {/* Links & Social */}
      <div className="border-t border-border/50 py-8 px-4 bg-muted/30">
        <div className="container mx-auto max-w-6xl">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-8 mb-8">
            {/* Brand */}
            <div className="text-center md:text-left">
              <h3 className="font-heading text-lg font-bold text-foreground mb-2">MeepleAI</h3>
              <p className="text-sm text-muted-foreground">
                L&apos;assistente AI per i tuoi giochi da tavolo preferiti
              </p>
            </div>

            {/* Quick Links */}
            <div className="text-center">
              <h4 className="font-semibold text-foreground mb-3 text-sm">Link Utili</h4>
              <nav className="flex flex-col gap-2">
                <Link
                  href="/privacy"
                  className="text-sm text-muted-foreground hover:text-primary transition-colors"
                >
                  Privacy Policy
                </Link>
                <Link
                  href="/terms"
                  className="text-sm text-muted-foreground hover:text-primary transition-colors"
                >
                  Termini di Servizio
                </Link>
                <Link
                  href="/api/docs"
                  className="text-sm text-muted-foreground hover:text-primary transition-colors"
                >
                  API Docs
                </Link>
              </nav>
            </div>

            {/* Social */}
            <div className="text-center md:text-right">
              <h4 className="font-semibold text-foreground mb-3 text-sm">Seguici</h4>
              <div className="flex justify-center md:justify-end gap-3">
                <a
                  href="https://github.com/meepleai"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="w-10 h-10 rounded-full bg-muted border border-border flex items-center justify-center hover:bg-primary hover:border-primary hover:text-white transition-all duration-300 hover:scale-110"
                  aria-label="GitHub"
                >
                  <Github className="h-5 w-5" />
                </a>
                <a
                  href="https://twitter.com/meepleai"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="w-10 h-10 rounded-full bg-muted border border-border flex items-center justify-center hover:bg-primary hover:border-primary hover:text-white transition-all duration-300 hover:scale-110"
                  aria-label="Twitter"
                >
                  <Twitter className="h-5 w-5" />
                </a>
                <a
                  href="mailto:info@meepleai.dev"
                  className="w-10 h-10 rounded-full bg-muted border border-border flex items-center justify-center hover:bg-primary hover:border-primary hover:text-white transition-all duration-300 hover:scale-110"
                  aria-label="Email"
                >
                  <Mail className="h-5 w-5" />
                </a>
              </div>
            </div>
          </div>

          {/* Copyright */}
          <div className="pt-6 border-t border-border/50 text-center">
            <p className="text-sm text-muted-foreground">
              <span>© {year} MeepleAI. Tutti i diritti riservati.</span> Fatto con{' '}
              <Heart className="inline h-4 w-4 text-red-500 fill-current mx-1" /> per i giocatori
            </p>
          </div>
        </div>
      </div>
    </footer>
  );
}
