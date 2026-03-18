import { render, screen } from '@testing-library/react';
import { AdminTabSidebar } from '../AdminTabSidebar';

vi.mock('next/navigation', () => ({
  usePathname: () => '/admin/overview',
}));

describe('AdminTabSidebar', () => {
  it('renders all 6 admin sections', () => {
    render(<AdminTabSidebar />);
    expect(screen.getByText('Overview')).toBeInTheDocument();
    expect(screen.getByText('Content')).toBeInTheDocument();
    expect(screen.getByText('AI')).toBeInTheDocument();
    expect(screen.getByText('Users')).toBeInTheDocument();
    expect(screen.getByText('System')).toBeInTheDocument();
    expect(screen.getByText('Analytics')).toBeInTheDocument();
  });

  it('highlights the active section based on pathname', () => {
    render(<AdminTabSidebar />);
    const overviewTab = screen.getByTestId('admin-tab-overview');
    expect(overviewTab.dataset.active).toBe('true');
  });

  it('shows sub-items for the active section', () => {
    render(<AdminTabSidebar />);
    // Overview section should show its sidebar items
    expect(screen.getByText('Dashboard')).toBeInTheDocument();
  });

  it('has responsive classes to hide on mobile', () => {
    render(<AdminTabSidebar />);
    const sidebar = screen.getByTestId('admin-tab-sidebar');
    expect(sidebar.className).toMatch(/hidden/);
    expect(sidebar.className).toMatch(/md:flex/);
  });
});
