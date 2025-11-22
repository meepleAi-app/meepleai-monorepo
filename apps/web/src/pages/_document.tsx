import { Html, Head, Main, NextScript } from 'next/document';

/**
 * Custom Document component for Next.js
 *
 * This file customizes the HTML document structure:
 * - Sets lang="en" on <html> for accessibility (WCAG 2.1 AA)
 * - Provides default <meta> tags for SEO
 *
 * Issue: #306 (UI-05 - Accessibility baseline audit)
 */
export default function Document() {
  return (
    <Html lang="en">
      <Head>
        {/* Default meta tags - can be overridden by individual pages */}
        <meta charSet="utf-8" />
        <meta name="description" content="AI-powered board game rules assistant" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
      </Head>
      <body>
        <Main />
        <NextScript />
      </body>
    </Html>
  );
}
