import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { WidgetShell } from '../WidgetShell';

describe('WidgetShell', () => {
  const baseProps = {
    icon: '🎲',
    title: 'Test Widget',
    typeLabel: 'TestType',
    collapsed: false,
    onHeaderClick: vi.fn(),
    headerAriaLabel: 'Espandi o collassa Test Widget',
  };

  it('renders header with icon + title + type pill', () => {
    render(
      <WidgetShell {...baseProps}>
        <p>body</p>
      </WidgetShell>
    );
    expect(screen.getByText('Test Widget')).toBeInTheDocument();
    expect(screen.getByText('TestType')).toBeInTheDocument();
    expect(screen.getByText('🎲')).toBeInTheDocument();
  });

  it('renders body when collapsed=false', () => {
    render(
      <WidgetShell {...baseProps} collapsed={false}>
        <p data-testid="body">visible</p>
      </WidgetShell>
    );
    expect(screen.getByTestId('body')).toBeInTheDocument();
  });

  it('unmounts body when collapsed=true', () => {
    render(
      <WidgetShell {...baseProps} collapsed={true}>
        <p data-testid="body">hidden</p>
      </WidgetShell>
    );
    expect(screen.queryByTestId('body')).not.toBeInTheDocument();
  });

  it('aria-expanded reflects !collapsed', () => {
    const { rerender, container } = render(
      <WidgetShell {...baseProps} collapsed={false}>
        body
      </WidgetShell>
    );
    expect(container.querySelector('button')?.getAttribute('aria-expanded')).toBe('true');

    rerender(
      <WidgetShell {...baseProps} collapsed={true}>
        body
      </WidgetShell>
    );
    expect(container.querySelector('button')?.getAttribute('aria-expanded')).toBe('false');
  });

  it('header click calls onHeaderClick', () => {
    const onHeaderClick = vi.fn();
    render(
      <WidgetShell {...baseProps} onHeaderClick={onHeaderClick}>
        body
      </WidgetShell>
    );
    fireEvent.click(screen.getByRole('button'));
    expect(onHeaderClick).toHaveBeenCalled();
  });

  it('data-collapsed attribute reflects collapsed state', () => {
    const { container, rerender } = render(
      <WidgetShell {...baseProps} collapsed={false}>
        body
      </WidgetShell>
    );
    expect(
      container.querySelector('[data-slot="widget-shell"]')?.getAttribute('data-collapsed')
    ).toBeNull();

    rerender(
      <WidgetShell {...baseProps} collapsed={true}>
        body
      </WidgetShell>
    );
    expect(
      container.querySelector('[data-slot="widget-shell"]')?.getAttribute('data-collapsed')
    ).toBe('true');
  });
});
