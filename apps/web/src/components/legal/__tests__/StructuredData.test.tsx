/**
 * StructuredData Component Tests
 *
 * Tests JSON-LD output and XSS prevention.
 */

import { render } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import {
  StructuredData,
  legalPageSchema,
  organizationSchema,
  breadcrumbSchema,
  faqPageSchema,
} from '../StructuredData';

describe('StructuredData', () => {
  it('renders a script tag with type application/ld+json', () => {
    const { container } = render(<StructuredData data={{ '@type': 'WebPage', name: 'Test' }} />);
    const script = container.querySelector('script[type="application/ld+json"]');
    expect(script).toBeInTheDocument();
  });

  it('contains valid JSON in script content', () => {
    const data = { '@type': 'WebPage', name: 'Privacy Policy' };
    const { container } = render(<StructuredData data={data} />);
    const script = container.querySelector('script[type="application/ld+json"]');
    const content = script?.innerHTML ?? '';
    // Should parse as valid JSON (with \\u003c unescaped by JSON.parse)
    const parsed = JSON.parse(content);
    expect(parsed['@type']).toBe('WebPage');
    expect(parsed.name).toBe('Privacy Policy');
  });

  it('escapes </script> to prevent JSON-LD injection', () => {
    const data = { name: '</script><script>alert(1)</script>' };
    const { container } = render(<StructuredData data={data} />);
    const script = container.querySelector('script[type="application/ld+json"]');
    const content = script?.innerHTML ?? '';
    // Must NOT contain literal </script>
    expect(content).not.toContain('</script>');
    // Must contain escaped version
    expect(content).toContain('\\u003c');
  });
});

describe('legalPageSchema', () => {
  it('generates correct WebPage schema', () => {
    const schema = legalPageSchema({
      name: 'Privacy Policy',
      description: 'Our privacy policy',
      url: 'https://meepleai.com/privacy',
      lastUpdated: '2026-03-09',
    });
    expect(schema['@context']).toBe('https://schema.org');
    expect(schema['@type']).toBe('WebPage');
    expect(schema.name).toBe('Privacy Policy');
    expect(schema.dateModified).toBe('2026-03-09');
  });
});

describe('organizationSchema', () => {
  it('generates correct Organization schema', () => {
    const schema = organizationSchema();
    expect(schema['@type']).toBe('Organization');
    expect(schema.name).toBe('MeepleAI');
    expect(schema.contactPoint.email).toBe('privacy@meepleai.com');
  });
});

describe('breadcrumbSchema', () => {
  it('generates correct BreadcrumbList schema', () => {
    const schema = breadcrumbSchema([
      { name: 'Home', url: 'https://meepleai.com' },
      { name: 'Privacy', url: 'https://meepleai.com/privacy' },
    ]);
    expect(schema['@type']).toBe('BreadcrumbList');
    expect(schema.itemListElement).toHaveLength(2);
    expect(schema.itemListElement[0].position).toBe(1);
    expect(schema.itemListElement[1].name).toBe('Privacy');
  });
});

describe('faqPageSchema', () => {
  it('generates correct FAQPage schema', () => {
    const schema = faqPageSchema([
      { question: 'What is MeepleAI?', answer: 'An AI board game assistant.' },
    ]);
    expect(schema['@type']).toBe('FAQPage');
    expect(schema.mainEntity).toHaveLength(1);
    expect(schema.mainEntity[0]['@type']).toBe('Question');
  });
});
