/**
 * App Router Root Layout (Server Component)
 *
 * This is the root layout for the Next.js App Router.
 * It wraps all pages with shared providers and global styles.
 *
 * Issue #1077: FE-IMP-001 - Bootstrap App Router + Shared Providers
 * @see https://nextjs.org/docs/app/building-your-application/routing/layouts-and-pages
 */

import { Quicksand, Inter } from 'next/font/google';

import { AppProviders } from './providers';

import type { Metadata, Viewport } from 'next';
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

const inter = Inter({
  subsets: ['latin'],
  weight: ['400', '500', '600', '700'],
  variable: '--font-inter',
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
  return (
    <html lang="it" suppressHydrationWarning>
      <body className={`${quicksand.variable} ${inter.variable}`} suppressHydrationWarning>
        <AppProviders>{children}</AppProviders>
      </body>
    </html>
  );
}
