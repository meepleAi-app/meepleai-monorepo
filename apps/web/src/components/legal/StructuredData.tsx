/**
 * StructuredData - JSON-LD structured data for SEO
 *
 * Renders JSON-LD schema.org structured data in a script tag.
 * Used on legal pages, FAQ, and about pages for rich search results.
 *
 * Security note: Content is generated from our own code via JSON.stringify,
 * never from user input. This is safe for dangerouslySetInnerHTML.
 */

interface StructuredDataProps {
  data: Record<string, unknown>;
}

/**
 * Render JSON-LD structured data.
 * The content is always generated server-side from trusted application data
 * via JSON.stringify — no user input is involved.
 */
export function StructuredData({ data }: StructuredDataProps) {
  // Escape </script> sequences to prevent JSON-LD injection via closing script tags
  const jsonLd = JSON.stringify(data).replace(/</g, '\\u003c');
  return <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: jsonLd }} />;
}

/**
 * Generate WebPage schema for legal pages
 */
export function legalPageSchema({
  name,
  description,
  url,
  lastUpdated,
}: {
  name: string;
  description: string;
  url: string;
  lastUpdated: string;
}) {
  return {
    '@context': 'https://schema.org',
    '@type': 'WebPage',
    name,
    description,
    url,
    dateModified: lastUpdated,
    publisher: organizationSchema(),
    inLanguage: ['it', 'en'],
    isPartOf: {
      '@type': 'WebSite',
      name: 'MeepleAI',
      url: 'https://meepleai.com',
    },
  };
}

/**
 * Generate Organization schema
 */
export function organizationSchema() {
  return {
    '@context': 'https://schema.org',
    '@type': 'Organization',
    name: 'MeepleAI',
    url: 'https://meepleai.com',
    logo: 'https://meepleai.com/logo.png',
    description:
      'AI-powered board game rules assistant. Upload rulebooks, ask questions, get precise answers.',
    sameAs: [
      'https://github.com/meepleai',
      'https://twitter.com/meepleai',
      'https://discord.gg/meepleai',
    ],
    contactPoint: {
      '@type': 'ContactPoint',
      email: 'privacy@meepleai.com',
      contactType: 'customer support',
      availableLanguage: ['Italian', 'English'],
    },
  };
}

/**
 * Generate FAQPage schema from FAQ data
 */
export function faqPageSchema(faqs: Array<{ question: string; answer: string }>) {
  return {
    '@context': 'https://schema.org',
    '@type': 'FAQPage',
    mainEntity: faqs.map(faq => ({
      '@type': 'Question',
      name: faq.question,
      acceptedAnswer: {
        '@type': 'Answer',
        text: faq.answer,
      },
    })),
  };
}

/**
 * Generate LearningResource schema for explainer / how-it-works pages.
 *
 * Emits a schema.org `LearningResource` (JSON-LD) so search engines can surface
 * the page as educational content. `learningResourceType` describes the format
 * (e.g. "explainer"); `inLanguage` mirrors the site's bilingual support.
 */
export function learningResourceSchema({
  name,
  description,
  url,
  learningResourceType = 'explainer',
  inLanguage = ['it', 'en'],
}: {
  name: string;
  description: string;
  url: string;
  learningResourceType?: string;
  inLanguage?: string[];
}) {
  return {
    '@context': 'https://schema.org',
    '@type': 'LearningResource',
    name,
    description,
    url,
    learningResourceType,
    inLanguage,
    provider: organizationSchema(),
    isPartOf: {
      '@type': 'WebSite',
      name: 'MeepleAI',
      url: 'https://meepleai.com',
    },
  };
}

/**
 * Generate BreadcrumbList schema
 */
export function breadcrumbSchema(items: Array<{ name: string; url: string }>) {
  return {
    '@context': 'https://schema.org',
    '@type': 'BreadcrumbList',
    itemListElement: items.map((item, index) => ({
      '@type': 'ListItem',
      position: index + 1,
      name: item.name,
      item: item.url,
    })),
  };
}
