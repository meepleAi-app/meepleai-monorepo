/**
 * App Router Root Layout (Server Component)
 *
 * This is the root layout for the Next.js App Router.
 * It wraps all pages with shared providers and global styles.
 *
 * Issue #1077: FE-IMP-001 - Bootstrap App Router + Shared Providers
 *
 * @see https://nextjs.org/docs/app/building-your-application/routing/layouts-and-pages
 */

import type { Metadata, Viewport } from 'next';
import { AppProviders } from './providers';
import '../styles/globals.css';
import '../styles/diff-viewer.css';
import 'prismjs/themes/prism-tomorrow.css';

export const metadata: Metadata = {
  title: 'MeepleAI - AI-Powered Board Game Rules Assistant',
  description: 'Never argue about rules again. Get instant, accurate answers from any game\'s rulebook with AI-powered semantic search.',
  keywords: ['board games', 'rules', 'AI', 'assistant', 'semantic search'],
  authors: [{ name: 'MeepleAI Team' }],
};

export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
  themeColor: [
    { media: '(prefers-color-scheme: light)', color: 'white' },
    { media: '(prefers-color-scheme: dark)', color: 'black' },
  ],
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="it" suppressHydrationWarning>
      <body>
        <AppProviders>{children}</AppProviders>
      </body>
    </html>
  );
}
