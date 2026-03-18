import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AdminBreadcrumb } from '../AdminBreadcrumb';

const { mockPush, mockUsePathname } = vi.hoisted(() => ({
  mockPush: vi.fn(),
  mockUsePathname: vi.fn(() => '/admin/agents/pipeline'),
}));

vi.mock('next/navigation', () => ({
  usePathname: mockUsePathname,
  useRouter: () => ({ push: mockPush }),
}));

describe('AdminBreadcrumb', () => {
  beforeEach(() => {
    mockPush.mockClear();
    mockUsePathname.mockReturnValue('/admin/agents/pipeline');
  });

  it('renders section and active sub-item from pathname', () => {
    render(<AdminBreadcrumb />);
    expect(screen.getByText('AI')).toBeInTheDocument();
    expect(screen.getByText('Pipeline Explorer')).toBeInTheDocument();
  });

  it('shows chevron when section has multiple sub-items', () => {
    render(<AdminBreadcrumb />);
    expect(screen.getByRole('button', { name: /show sub-sections/i })).toBeInTheDocument();
  });

  it('opens dropdown on chevron click showing all sub-items', async () => {
    render(<AdminBreadcrumb />);
    await userEvent.click(screen.getByRole('button', { name: /show sub-sections/i }));
    // AI section has 13 items — check a few
    expect(screen.getByText('All Agents')).toBeInTheDocument();
    expect(screen.getByText('Debug Console')).toBeInTheDocument();
    expect(screen.getByText('Usage & Costs')).toBeInTheDocument();
  });

  it('navigates when dropdown item is clicked', async () => {
    render(<AdminBreadcrumb />);
    await userEvent.click(screen.getByRole('button', { name: /show sub-sections/i }));
    await userEvent.click(screen.getByText('Debug Console'));
    expect(mockPush).toHaveBeenCalledWith('/admin/agents/debug');
  });

  it('closes dropdown on outside click', async () => {
    render(<AdminBreadcrumb />);
    await userEvent.click(screen.getByRole('button', { name: /show sub-sections/i }));
    expect(screen.getByText('All Agents')).toBeInTheDocument();
    // Click outside
    await userEvent.click(document.body);
    expect(screen.queryByText('All Agents')).not.toBeInTheDocument();
  });

  it('shows fallback for unmapped paths', () => {
    mockUsePathname.mockReturnValue('/admin/unknown/deep');
    render(<AdminBreadcrumb />);
    // Should capitalize URL segment as fallback
    expect(screen.getByText('Unknown')).toBeInTheDocument();
  });

  it('shows only section name for root admin path', () => {
    mockUsePathname.mockReturnValue('/admin/overview');
    render(<AdminBreadcrumb />);
    expect(screen.getByText('Overview')).toBeInTheDocument();
  });

  it('navigates to section hub when section name is clicked', async () => {
    render(<AdminBreadcrumb />);
    const sectionLink = screen.getByText('AI').closest('a');
    expect(sectionLink).toHaveAttribute('href', '/admin/agents');
  });
});
