/**
 * LegalMarkdown Component Tests
 *
 * Tests the lightweight markdown renderer for legal content.
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { LegalMarkdown } from '../LegalMarkdown';

describe('LegalMarkdown', () => {
  it('renders plain text as paragraph', () => {
    render(<LegalMarkdown content="Hello world" />);
    expect(screen.getByText('Hello world')).toBeInTheDocument();
  });

  it('renders null for empty content', () => {
    const { container } = render(<LegalMarkdown content="" />);
    expect(container.firstChild).toBeNull();
  });

  it('renders **bold** text', () => {
    render(<LegalMarkdown content="This is **important** text" />);
    const strong = screen.getByText('important');
    expect(strong.tagName).toBe('STRONG');
  });

  it('renders unordered lists', () => {
    render(<LegalMarkdown content={`- First item\n- Second item\n- Third item`} />);
    const items = screen.getAllByRole('listitem');
    expect(items).toHaveLength(3);
    expect(items[0]).toHaveTextContent('First item');
    expect(items[2]).toHaveTextContent('Third item');
  });

  it('renders ordered lists', () => {
    render(<LegalMarkdown content={`1. Step one\n2. Step two\n3. Step three`} />);
    const items = screen.getAllByRole('listitem');
    expect(items).toHaveLength(3);
    expect(items[0]).toHaveTextContent('Step one');
  });

  it('renders tables with headers', () => {
    render(
      <LegalMarkdown
        content={`| Provider | Location |\n|---|---|\n| Ollama | EU |\n| OpenRouter | USA |`}
      />
    );
    expect(screen.getByText('Provider')).toBeInTheDocument();
    expect(screen.getByText('Ollama')).toBeInTheDocument();
    expect(screen.getByText('USA')).toBeInTheDocument();
  });

  it('renders links', () => {
    render(<LegalMarkdown content="Visit [our site](https://meepleai.com)" />);
    const link = screen.getByText('our site');
    expect(link.tagName).toBe('A');
    expect(link).toHaveAttribute('href', 'https://meepleai.com');
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  });

  it('renders internal links without target=_blank', () => {
    render(<LegalMarkdown content="Go to [settings](/settings/ai-consent)" />);
    const link = screen.getByText('settings');
    expect(link).toHaveAttribute('href', '/settings/ai-consent');
    expect(link).not.toHaveAttribute('target');
  });

  it('renders sub-headings', () => {
    render(<LegalMarkdown content="### Section Title" />);
    expect(screen.getByText('Section Title').tagName).toBe('H4');
  });

  it('renders mixed content correctly', () => {
    const content = `MeepleAI uses AI to help you.\n\n**What we process:**\n\n- Your questions\n- Game context\n\n| Data | Retention |\n|---|---|\n| Logs | 30 days |`;

    render(<LegalMarkdown content={content} />);

    expect(screen.getByText(/MeepleAI uses AI/)).toBeInTheDocument();
    expect(screen.getByText('What we process:')).toBeInTheDocument();
    expect(screen.getByText('Your questions')).toBeInTheDocument();
    expect(screen.getByText('Logs')).toBeInTheDocument();
    expect(screen.getByText('30 days')).toBeInTheDocument();
  });

  it('applies custom className', () => {
    const { container } = render(<LegalMarkdown content="test" className="custom-class" />);
    expect(container.firstChild).toHaveClass('custom-class');
  });

  it('strips javascript: protocol links as plain text (XSS prevention)', () => {
    render(<LegalMarkdown content="Click [here](javascript:alert(1))" />);
    const text = screen.getByText('here');
    // Should be rendered as a span, not a link
    expect(text.tagName).toBe('SPAN');
    expect(text.closest('a')).toBeNull();
  });

  it('strips data: protocol links as plain text (XSS prevention)', () => {
    render(<LegalMarkdown content="Click [here](data:text/html,test)" />);
    const text = screen.getByText('here');
    expect(text.tagName).toBe('SPAN');
    expect(text.closest('a')).toBeNull();
  });

  it('allows mailto: links', () => {
    render(<LegalMarkdown content="Email [us](mailto:privacy@meepleai.com)" />);
    const link = screen.getByText('us');
    expect(link.tagName).toBe('A');
    expect(link).toHaveAttribute('href', 'mailto:privacy@meepleai.com');
  });

  it('allows anchor (#) links', () => {
    render(<LegalMarkdown content="Go to [section](#top)" />);
    const link = screen.getByText('section');
    expect(link.tagName).toBe('A');
    expect(link).toHaveAttribute('href', '#top');
  });
});
