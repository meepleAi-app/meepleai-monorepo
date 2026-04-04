import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ContextBar } from '@/components/layout/ContextBar';
import { useContextBarStore } from '@/lib/stores/context-bar-store';

// Mock the scroll direction hook to avoid window.scrollY issues in tests
vi.mock('@/hooks/useScrollDirection', () => ({
  useScrollDirection: () => 'up',
}));

describe('ContextBar', () => {
  beforeEach(() => {
    useContextBarStore.setState({ content: null, options: { alwaysVisible: false } });
  });

  it('renders nothing when content is null', () => {
    render(<ContextBar />);
    expect(screen.queryByTestId('context-bar')).toBeNull();
  });

  it('renders content from store', () => {
    useContextBarStore.getState().setContent(<div>Test Content</div>);
    render(<ContextBar />);
    expect(screen.getByText('Test Content')).toBeDefined();
  });

  it('has correct height class', () => {
    useContextBarStore.getState().setContent(<div>Content</div>);
    render(<ContextBar />);
    const bar = screen.getByTestId('context-bar');
    expect(bar.className).toContain('h-11');
  });
});
