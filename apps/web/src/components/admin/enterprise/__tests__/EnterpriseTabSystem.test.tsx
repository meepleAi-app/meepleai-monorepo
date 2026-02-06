/**
 * EnterpriseTabSystem Tests
 * Issue #3689 - Layout Base & Navigation System
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';

import { EnterpriseTabSystem } from '../EnterpriseTabSystem';
import { ENTERPRISE_SECTIONS } from '@/config/enterprise-navigation';

const mockPush = vi.fn();
const mockSearchParams = new URLSearchParams();

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
  usePathname: () => '/admin/resources',
  useSearchParams: () => mockSearchParams,
}));

const resourcesSection = ENTERPRISE_SECTIONS.find((s) => s.id === 'resources')!;

describe('EnterpriseTabSystem', () => {
  beforeEach(() => {
    mockPush.mockClear();
  });

  it('renders all tabs for a section', () => {
    render(
      <EnterpriseTabSystem tabs={resourcesSection.tabs} activeTab="tokens">
        <div>Tab content</div>
      </EnterpriseTabSystem>
    );

    expect(screen.getByTestId('enterprise-tab-tokens')).toBeInTheDocument();
    expect(screen.getByTestId('enterprise-tab-database')).toBeInTheDocument();
    expect(screen.getByTestId('enterprise-tab-cache')).toBeInTheDocument();
    expect(screen.getByTestId('enterprise-tab-vectors')).toBeInTheDocument();
    expect(screen.getByTestId('enterprise-tab-services')).toBeInTheDocument();
  });

  it('highlights the active tab', () => {
    render(
      <EnterpriseTabSystem tabs={resourcesSection.tabs} activeTab="tokens">
        <div>Tab content</div>
      </EnterpriseTabSystem>
    );

    const activeTab = screen.getByTestId('enterprise-tab-tokens');
    expect(activeTab).toHaveAttribute('aria-selected', 'true');

    const inactiveTab = screen.getByTestId('enterprise-tab-database');
    expect(inactiveTab).toHaveAttribute('aria-selected', 'false');
  });

  it('navigates to new tab on click', async () => {
    const user = userEvent.setup();

    render(
      <EnterpriseTabSystem tabs={resourcesSection.tabs} activeTab="tokens">
        <div>Tab content</div>
      </EnterpriseTabSystem>
    );

    await user.click(screen.getByTestId('enterprise-tab-database'));

    expect(mockPush).toHaveBeenCalledWith(
      expect.stringContaining('tab=database'),
      { scroll: false }
    );
  });

  it('renders children in tab panel', () => {
    render(
      <EnterpriseTabSystem tabs={resourcesSection.tabs} activeTab="tokens">
        <div data-testid="tab-content">Token management content</div>
      </EnterpriseTabSystem>
    );

    expect(screen.getByTestId('tab-content')).toBeInTheDocument();
  });

  it('has correct ARIA roles', () => {
    render(
      <EnterpriseTabSystem tabs={resourcesSection.tabs} activeTab="tokens">
        <div>Tab content</div>
      </EnterpriseTabSystem>
    );

    expect(screen.getByRole('tablist')).toBeInTheDocument();

    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(5);
  });

  it('has correct tab panel', () => {
    render(
      <EnterpriseTabSystem tabs={resourcesSection.tabs} activeTab="tokens">
        <div>Tab content</div>
      </EnterpriseTabSystem>
    );

    expect(screen.getByRole('tabpanel')).toBeInTheDocument();
  });

  it('handles keyboard ArrowRight navigation', async () => {
    const user = userEvent.setup();

    render(
      <EnterpriseTabSystem tabs={resourcesSection.tabs} activeTab="tokens">
        <div>Tab content</div>
      </EnterpriseTabSystem>
    );

    const firstTab = screen.getByTestId('enterprise-tab-tokens');
    firstTab.focus();
    await user.keyboard('{ArrowRight}');

    expect(mockPush).toHaveBeenCalledWith(
      expect.stringContaining('tab=database'),
      { scroll: false }
    );
  });

  it('handles keyboard ArrowLeft navigation', async () => {
    const user = userEvent.setup();

    render(
      <EnterpriseTabSystem tabs={resourcesSection.tabs} activeTab="database">
        <div>Tab content</div>
      </EnterpriseTabSystem>
    );

    const secondTab = screen.getByTestId('enterprise-tab-database');
    secondTab.focus();
    await user.keyboard('{ArrowLeft}');

    expect(mockPush).toHaveBeenCalledWith(
      expect.stringContaining('tab=tokens'),
      { scroll: false }
    );
  });

  it('wraps around on ArrowRight at last tab', async () => {
    const user = userEvent.setup();

    render(
      <EnterpriseTabSystem tabs={resourcesSection.tabs} activeTab="services">
        <div>Tab content</div>
      </EnterpriseTabSystem>
    );

    const lastTab = screen.getByTestId('enterprise-tab-services');
    lastTab.focus();
    await user.keyboard('{ArrowRight}');

    expect(mockPush).toHaveBeenCalledWith(
      expect.stringContaining('tab=tokens'),
      { scroll: false }
    );
  });

  it('handles Home key to first tab', async () => {
    const user = userEvent.setup();

    render(
      <EnterpriseTabSystem tabs={resourcesSection.tabs} activeTab="cache">
        <div>Tab content</div>
      </EnterpriseTabSystem>
    );

    const middleTab = screen.getByTestId('enterprise-tab-cache');
    middleTab.focus();
    await user.keyboard('{Home}');

    expect(mockPush).toHaveBeenCalledWith(
      expect.stringContaining('tab=tokens'),
      { scroll: false }
    );
  });

  it('handles End key to last tab', async () => {
    const user = userEvent.setup();

    render(
      <EnterpriseTabSystem tabs={resourcesSection.tabs} activeTab="tokens">
        <div>Tab content</div>
      </EnterpriseTabSystem>
    );

    const firstTab = screen.getByTestId('enterprise-tab-tokens');
    firstTab.focus();
    await user.keyboard('{End}');

    expect(mockPush).toHaveBeenCalledWith(
      expect.stringContaining('tab=services'),
      { scroll: false }
    );
  });

  it('displays tab labels with icons', () => {
    render(
      <EnterpriseTabSystem tabs={resourcesSection.tabs} activeTab="tokens">
        <div>Tab content</div>
      </EnterpriseTabSystem>
    );

    expect(screen.getByText('Tokens')).toBeInTheDocument();
    expect(screen.getByText('Database')).toBeInTheDocument();
    expect(screen.getByText('Cache')).toBeInTheDocument();
    expect(screen.getByText('Vectors')).toBeInTheDocument();
    expect(screen.getByText('Services')).toBeInTheDocument();
  });
});
