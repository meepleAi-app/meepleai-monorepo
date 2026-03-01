/**
 * Unit tests for WidgetCard — reusable widget wrapper.
 * Issue #5156 — Epic B13.
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import React from 'react';

import { Dices } from 'lucide-react';
import { WidgetCard } from '../WidgetCard';

describe('WidgetCard', () => {
  it('renders title and children', () => {
    render(
      <WidgetCard title="Test Widget" icon={<Dices data-testid="icon" />} isEnabled={true}>
        <span>widget content</span>
      </WidgetCard>
    );

    expect(screen.getByText('Test Widget')).toBeInTheDocument();
    expect(screen.getByText('widget content')).toBeInTheDocument();
  });

  it('calls onToggle when switch is clicked', () => {
    const onToggle = vi.fn();
    render(
      <WidgetCard title="Toggle Widget" icon={<Dices />} isEnabled={true} onToggle={onToggle}>
        <span>content</span>
      </WidgetCard>
    );

    const toggle = screen.getByRole('switch', { name: /toggle toggle widget/i });
    fireEvent.click(toggle);
    expect(onToggle).toHaveBeenCalledWith(false);
  });

  it('shows configure button when onConfigure is provided', () => {
    const onConfigure = vi.fn();
    render(
      <WidgetCard title="Config Widget" icon={<Dices />} isEnabled={true} onConfigure={onConfigure}>
        <span>content</span>
      </WidgetCard>
    );

    const configBtn = screen.getByRole('button', { name: /configure config widget/i });
    fireEvent.click(configBtn);
    expect(onConfigure).toHaveBeenCalledTimes(1);
  });

  it('applies reduced opacity when disabled', () => {
    const { container } = render(
      <WidgetCard title="Disabled Widget" icon={<Dices />} isEnabled={false}>
        <span>content</span>
      </WidgetCard>
    );

    expect(container.firstChild).toHaveClass('opacity-60');
  });

  it('passes data-testid to root element', () => {
    render(
      <WidgetCard title="Widget" icon={<Dices />} isEnabled={true} data-testid="my-widget">
        <span>content</span>
      </WidgetCard>
    );

    expect(screen.getByTestId('my-widget')).toBeInTheDocument();
  });
});
