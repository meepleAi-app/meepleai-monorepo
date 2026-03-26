/**
 * PublicFooter Component - Issue #2230
 *
 * Footer per il layout pubblico con layout 3 colonne responsive.
 * Features:
 * - Layout 3 colonne: About, Quick Links, Legal
 * - Social links
 * - Copyright
 * - Responsive (colonne stack su mobile)
 * - Dark mode support
 */

'use client';

import { useState } from 'react';

import { ExternalLinkIcon } from 'lucide-react';
import Link from 'next/link';

import { CookieConsentBanner } from '@/components/legal/CookieConsentBanner';
import { MeepleLogo } from '@/components/ui/meeple/meeple-logo';
import { cn } from '@/lib/utils';

export interface PublicFooterProps {
  /** Show newsletter subscription */
  showNewsletter?: boolean;
  /** Additional className */
  className?: string;
}

interface FooterLink {
  label: string;
  href: string;
}

const ABOUT_LINKS: FooterLink[] = [
  { label: 'Chi Siamo', href: '/about' },
  { label: 'Come Funziona', href: '/how-it-works' },
  { label: 'Blog', href: '/blog' },
  { label: 'Contatti', href: '/contact' },
];

const QUICK_LINKS: FooterLink[] = [
  { label: 'Giochi', href: '/games' },
  { label: 'Chat AI', href: '/chat/new' },
  { label: 'Dashboard', href: '/library' },
  { label: 'FAQ', href: '/faq' },
];

const LEGAL_LINKS: FooterLink[] = [
  { label: 'Privacy Policy', href: '/privacy' },
  { label: 'Termini di Servizio', href: '/terms' },
  { label: 'Cookie Policy', href: '/cookies' },
];

const SOCIAL_LINKS = [
  { label: 'GitHub', href: 'https://github.com/meepleai' },
  { label: 'Twitter', href: 'https://twitter.com/meepleai' },
  { label: 'Discord', href: 'https://discord.gg/meepleai' },
];

export function PublicFooter({
  showNewsletter: _showNewsletter = false,
  className,
}: PublicFooterProps) {
  const currentYear = new Date().getFullYear();
  const [showCookieManager, setShowCookieManager] = useState(false);

  return (
    <>
      <footer className={cn('mt-auto border-t bg-background', className)}>
        <div className="container mx-auto px-4 sm:px-6 lg:px-8 py-12">
          {/* Main Footer Content - 3 Columns */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-8 mb-8">
            {/* Column 1: About */}
            <div>
              <div className="mb-4">
                <MeepleLogo variant="full" size="sm" />
              </div>
              <p className="text-sm text-muted-foreground mb-4">
                Il tuo assistente AI per le regole dei giochi da tavolo. Impara, gioca e scopri
                nuovi giochi con l'aiuto dell'intelligenza artificiale.
              </p>
              <div className="flex gap-4">
                {SOCIAL_LINKS.map(social => (
                  <Link
                    key={social.label}
                    href={social.href}
                    target="_blank"
                    rel="noopener noreferrer"
                    className={cn(
                      'text-sm text-muted-foreground hover:text-foreground transition-colors inline-flex items-center gap-1',
                      'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring rounded-sm'
                    )}
                    aria-label={social.label}
                  >
                    <ExternalLinkIcon className="h-4 w-4" aria-hidden="true" />
                    <span>{social.label}</span>
                  </Link>
                ))}
              </div>
            </div>

            {/* Column 2: Quick Links */}
            <div>
              <h3 className="text-sm font-semibold mb-4">Link Rapidi</h3>
              <ul className="space-y-2">
                {QUICK_LINKS.map(link => (
                  <li key={link.href}>
                    <Link
                      href={link.href}
                      className={cn(
                        'text-sm text-muted-foreground hover:text-foreground transition-colors',
                        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring rounded-sm'
                      )}
                    >
                      {link.label}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>

            {/* Column 3: About & Legal */}
            <div className="space-y-6">
              {/* About Section */}
              <div>
                <h3 className="text-sm font-semibold mb-4">Chi Siamo</h3>
                <ul className="space-y-2">
                  {ABOUT_LINKS.map(link => (
                    <li key={link.href}>
                      <Link
                        href={link.href}
                        className={cn(
                          'text-sm text-muted-foreground hover:text-foreground transition-colors',
                          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring rounded-sm'
                        )}
                      >
                        {link.label}
                      </Link>
                    </li>
                  ))}
                </ul>
              </div>

              {/* Legal Section */}
              <div>
                <h3 className="text-sm font-semibold mb-4">Legale</h3>
                <ul className="space-y-2">
                  {LEGAL_LINKS.map(link => (
                    <li key={link.href}>
                      <Link
                        href={link.href}
                        className={cn(
                          'text-sm text-muted-foreground hover:text-foreground transition-colors',
                          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring rounded-sm'
                        )}
                      >
                        {link.label}
                      </Link>
                    </li>
                  ))}
                  <li>
                    <button
                      onClick={() => setShowCookieManager(true)}
                      className={cn(
                        'text-sm text-muted-foreground hover:text-foreground transition-colors',
                        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring rounded-sm'
                      )}
                      type="button"
                    >
                      Gestisci Cookie
                    </button>
                  </li>
                </ul>
              </div>
            </div>
          </div>

          {/* Bottom Bar - Copyright */}
          <div className="pt-8 border-t">
            <div className="flex flex-col sm:flex-row justify-between items-center gap-4">
              <p className="text-sm text-muted-foreground text-center sm:text-left">
                © {currentYear} MeepleAI. Tutti i diritti riservati.
              </p>
              <p className="text-xs text-muted-foreground text-center sm:text-right">
                Realizzato con ❤️ per gli appassionati di giochi da tavolo
              </p>
            </div>
          </div>
        </div>
      </footer>
      {showCookieManager && (
        <CookieConsentBanner forceShow onDismiss={() => setShowCookieManager(false)} />
      )}
    </>
  );
}
