import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { CoverAttributionChip } from '../CoverAttributionChip';

describe('CoverAttributionChip — Issue #1823 Wave 3 M14', () => {
  it('renders nothing when attribution prop is undefined', () => {
    const { container } = render(<CoverAttributionChip />);
    expect(container.firstChild).toBeNull();
  });

  it('renders nothing when both author and license are missing', () => {
    const { container } = render(
      <CoverAttributionChip attribution={{ author: null, license: null, sourceUrl: null }} />
    );
    expect(container.firstChild).toBeNull();
  });

  it('renders author only when license is missing', () => {
    render(<CoverAttributionChip attribution={{ author: 'John Doe' }} />);
    expect(screen.getByText('John Doe')).toBeInTheDocument();
    expect(screen.queryByText(/CC/)).not.toBeInTheDocument();
  });

  it('renders license only when author is missing', () => {
    render(<CoverAttributionChip attribution={{ license: 'CC0' }} />);
    expect(screen.getByText('CC0')).toBeInTheDocument();
  });

  it('renders license as a link with rel="license" when sourceUrl is present', () => {
    render(
      <CoverAttributionChip
        attribution={{
          author: 'Jane Smith',
          license: 'CC BY-SA 4.0',
          sourceUrl: 'https://commons.wikimedia.org/wiki/File:Cover.jpg',
        }}
      />
    );

    const link = screen.getByRole('link', { name: /CC BY-SA 4.0/i });
    expect(link).toHaveAttribute('href', 'https://commons.wikimedia.org/wiki/File:Cover.jpg');
    expect(link).toHaveAttribute('target', '_blank');
    // rel attribute may contain multiple tokens; check inclusion
    expect(link.getAttribute('rel')?.split(/\s+/)).toEqual(
      expect.arrayContaining(['noopener', 'noreferrer', 'license'])
    );
  });

  it('renders both author and license with a separator', () => {
    render(<CoverAttributionChip attribution={{ author: 'John Doe', license: 'CC BY 4.0' }} />);
    expect(screen.getByText('John Doe')).toBeInTheDocument();
    expect(screen.getByText('CC BY 4.0')).toBeInTheDocument();
    // Separator is an aria-hidden interpunct.
    expect(screen.getByText('·', { ignore: false })).toBeInTheDocument();
  });
});
