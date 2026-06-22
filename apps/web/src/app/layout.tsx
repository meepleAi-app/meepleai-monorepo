/**
 * App Router Root Layout (Server Component)
 *
 * This is the root layout for the Next.js App Router.
 * It wraps all pages with shared providers and global styles.
 *
 * Issue #1077: FE-IMP-001 - Bootstrap App Router + Shared Providers
 * @see https://nextjs.org/docs/app/building-your-application/routing/layouts-and-pages
 */

import { Quicksand, Nunito, JetBrains_Mono } from 'next/font/google';

import { AppProviders } from './providers';

import type { Metadata, Viewport } from 'next';
// Token system load order (DS-16, 2026-05-12-token-canonicalization.md):
//   1. design-tokens-canonical.css — single source of truth (mockup-faithful)
//   2. globals.css                 — Tailwind layer + component utilities
//   3. domain-specific stylesheets
// Bridge layer (token-bridge.css) was removed in DS-16 after the codemod
// renamed all consumer references to canonical names.
import '../styles/design-tokens-canonical.css';
import '../styles/globals.css';
import '../styles/diff-viewer.css';
import '../styles/agent-theme.css';
import '../styles/agent-typography.css';
import '../styles/agent-animations.css';
import 'prismjs/themes/prism-tomorrow.css';

const quicksand = Quicksand({
  subsets: ['latin'],
  weight: ['400', '500', '600', '700'],
  variable: '--font-quicksand',
  display: 'swap',
});

const nunito = Nunito({
  subsets: ['latin'],
  weight: ['400', '500', '600', '700', '800'],
  variable: '--font-nunito',
  display: 'swap',
});

// Mockup handoff DS-step 2a (2026-05-24): JetBrains Mono caricato esplicitamente
// per soddisfare `--f-mono` token (design-tokens-canonical.css) usato in 15+ file
// (sse parser, log viewer, code blocks, monospace badges). Pre-fix il fallback
// `ui-monospace`/`SF Mono`/`monospace` veniva applicato silenziosamente.
const jetbrainsMono = JetBrains_Mono({
  subsets: ['latin'],
  weight: ['400', '500', '600', '700', '800'],
  variable: '--font-jetbrains-mono',
  display: 'swap',
});

export const metadata: Metadata = {
  metadataBase: new URL(process.env.NEXT_PUBLIC_BASE_URL || 'https://meepleai.com'),
  title: 'MeepleAI - AI-Powered Board Game Rules Assistant',
  description:
    "Never argue about rules again. Get instant, accurate answers from any game's rulebook with AI-powered semantic search.",
  keywords: ['board games', 'rules', 'AI', 'assistant', 'semantic search'],
  authors: [{ name: 'MeepleAI Team' }],
  // PWA manifest (Issue #3346)
  manifest: '/manifest.json',
  appleWebApp: {
    capable: true,
    statusBarStyle: 'default',
    title: 'MeepleAI',
  },
  formatDetection: {
    telephone: false,
  },
};

export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
  viewportFit: 'cover',
  themeColor: [
    { media: '(prefers-color-scheme: light)', color: 'white' },
    { media: '(prefers-color-scheme: dark)', color: 'black' },
  ],
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  // SSR theme hint (DS-1): `data-theme="light"` matches the mockup default.
  // next-themes will rewrite this attribute client-side based on user
  // preference / localStorage. The hint prevents FOUC on first paint.
  return (
    <html lang="it" data-theme="light" suppressHydrationWarning>
      <body
        className={`${quicksand.variable} ${nunito.variable} ${jetbrainsMono.variable}`}
        suppressHydrationWarning
      >
        <AppProviders>{children}</AppProviders>
      </body>
    </html>
  );
}
