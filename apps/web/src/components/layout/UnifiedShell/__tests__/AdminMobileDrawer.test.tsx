import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AdminMobileDrawer } from '../AdminMobileDrawer';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(() => '/admin/overview'),
}));

describe('AdminMobileDrawer', () => {
  it('renders AdminTabSidebar inside Sheet when open', () => {
    render(<AdminMobileDrawer open={true} onOpenChange={vi.fn()} />);
    expect(screen.getByTestId('admin-tab-sidebar')).toBeInTheDocument();
  });

  it('does not render when open is false', () => {
    const { container } = render(<AdminMobileDrawer open={false} onOpenChange={vi.fn()} />);
    // Component returns null when closed
    expect(container.firstElementChild).toBeNull();
  });

  it('calls onOpenChange when close is triggered', async () => {
    const onOpenChange = vi.fn();
    render(<AdminMobileDrawer open={true} onOpenChange={onOpenChange} />);
    // shadcn Sheet has a close button with sr-only text "Close"
    const closeBtn = screen.getByRole('button', { name: /close/i });
    await userEvent.click(closeBtn);
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });
});
