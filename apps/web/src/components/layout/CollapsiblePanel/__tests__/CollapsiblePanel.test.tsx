import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { CollapsiblePanel } from '../CollapsiblePanel';

describe('CollapsiblePanel', () => {
  it('renders children when expanded', () => {
    render(
      <CollapsiblePanel side="left" isCollapsed={false} onToggle={vi.fn()}>
        <p>Panel content</p>
      </CollapsiblePanel>
    );
    expect(screen.getByText('Panel content')).toBeInTheDocument();
  });

  it('hides children when collapsed', () => {
    render(
      <CollapsiblePanel side="left" isCollapsed={true} onToggle={vi.fn()}>
        <p>Panel content</p>
      </CollapsiblePanel>
    );
    expect(screen.queryByText('Panel content')).not.toBeInTheDocument();
  });

  it('shows collapse strip at 44px when collapsed', () => {
    render(<CollapsiblePanel side="left" isCollapsed={true} onToggle={vi.fn()} />);
    const panel = screen.getByTestId('collapsible-panel');
    expect(panel.className).toContain('w-[44px]');
  });

  it('calls onToggle when toggle button clicked', async () => {
    const onToggle = vi.fn();
    const user = userEvent.setup();
    render(
      <CollapsiblePanel side="right" isCollapsed={false} onToggle={onToggle}>
        Content
      </CollapsiblePanel>
    );
    await user.click(screen.getByRole('button', { name: /comprimi/i }));
    expect(onToggle).toHaveBeenCalledOnce();
  });
});
