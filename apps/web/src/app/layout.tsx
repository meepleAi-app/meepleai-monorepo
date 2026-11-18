/**
 * App Router Root Layout (Server Component)
 *
 * This is the root layout for the Next.js App Router.
 * It wraps all pages with shared providers and global styles.
 *
 * Issue #1077: FE-IMP-001 - Bootstrap App Router + Shared Providers
 * @see https://nextjs.org/docs/app/building-your-application/routing/layouts-and-pages
 */

import { preload } from 'react-dom';

import { AppProviders } from './providers';

import type { Metadata, Viewport } from 'next';
// Token system load order (DS-16, 2026-05-12-token-canonicalization.md):
//   0. fonts.css                   — self-hosted @font-face + --font-* variables (#4997)
//   1. design-tokens-canonical.css — single source of truth (mockup-faithful)
//   2. globals.css                 — Tailwind layer + component utilities
//   3. domain-specific stylesheets
// Bridge layer (token-bridge.css) was removed in DS-16 after the codemod
// renamed all consumer references to canonical names.
import '../styles/fonts.css';
import '../styles/design-tokens-canonical.css';
import '../styles/globals.css';
import '../styles/diff-viewer.css';
import 'prismjs/themes/prism-tomorrow.css';
// Agent neon-brutalist styles (agent-theme/typography/animations) are NOT loaded
// globally — they are scoped to the agent feature via
// `@/components/agent/agent-theme-styles`, imported by the agent components that
// consume `--agent-*` (issue #2852). This keeps ~450 LOC + a Google Fonts
// @import off every non-agent page.

// #4997: the fonts are self-hosted (styles/fonts.css), not loaded through next/font/google. These are the
// latin subsets, the ones next/font preloaded; the other subsets load only when a page uses their characters.
// JetBrains Mono backs the `--f-mono` token (design-tokens-canonical.css, DS-step 2a).
const PRELOADED_FONTS = [
  '/fonts/quicksand/quicksand-v37-latin.woff2',
  '/fonts/nunito/nunito-v32-latin.woff2',
  '/fonts/jetbrains-mono/jetbrains-mono-v24-latin.woff2',
] as const;

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
  for (const href of PRELOADED_FONTS) {
    preload(href, { as: 'font', type: 'font/woff2', crossOrigin: 'anonymous' });
  }

  // SSR theme hint (DS-1): `data-theme="light"` matches the mockup default.
  // next-themes will rewrite this attribute client-side based on user
  // preference / localStorage. The hint prevents FOUC on first paint.
  return (
    <html lang="it" data-theme="light" suppressHydrationWarning>
      <body suppressHydrationWarning>
        <AppProviders>{children}</AppProviders>
      </body>
    </html>
  );
}
