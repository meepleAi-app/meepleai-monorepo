/**
 * GrafanaEmbed Component Tests
 * Issue #901 - Grafana embed iframe setup
 *
 * Tests:
 * - Component rendering
 * - Dashboard tab switching
 * - Iframe URL generation
 * - Error handling
 * - Loading states
 * - External link functionality
 * - Refresh functionality
 * - Localization (IT/EN)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { GrafanaEmbed } from '../GrafanaEmbed';
import { GRAFANA_DASHBOARDS } from '@/config/grafana-dashboards';

// Mock window.open
const mockWindowOpen = vi.fn();
vi.stubGlobal('open', mockWindowOpen);

describe('GrafanaEmbed', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render with default props', () => {
      render(<GrafanaEmbed />);

      expect(screen.getByText('Grafana Dashboards')).toBeInTheDocument();
      expect(screen.getByText('View real-time metrics from Grafana')).toBeInTheDocument();
    });

    it('should render in Italian locale', () => {
      render(<GrafanaEmbed locale="it" />);

      expect(screen.getByText('Dashboard Grafana')).toBeInTheDocument();
      expect(screen.getByText('Visualizza metriche in tempo reale da Grafana')).toBeInTheDocument();
    });

    it('should render all dashboard tabs', () => {
      render(<GrafanaEmbed />);

      GRAFANA_DASHBOARDS.forEach(dashboard => {
        expect(screen.getByText(dashboard.name.en)).toBeInTheDocument();
      });
    });

    it('should render default dashboard (infrastructure)', () => {
      render(<GrafanaEmbed />);

      const infrastructureDashboard = GRAFANA_DASHBOARDS.find(d => d.id === 'infrastructure');
      expect(screen.getByText(infrastructureDashboard!.description.en)).toBeInTheDocument();
    });
  });

  describe('Dashboard Switching', () => {
    it('should render all dashboard tabs', () => {
      render(<GrafanaEmbed />);

      const infrastructureTab = screen.getByRole('tab', { name: /Infrastructure/i });
      const llmCostTab = screen.getByRole('tab', { name: /LLM Cost/i });
      const apiTab = screen.getByRole('tab', { name: /API Performance/i });
      const ragTab = screen.getByRole('tab', { name: /RAG Operations/i });

      expect(infrastructureTab).toBeInTheDocument();
      expect(llmCostTab).toBeInTheDocument();
      expect(apiTab).toBeInTheDocument();
      expect(ragTab).toBeInTheDocument();
    });

    it('should render correct iframe initially', () => {
      const { container } = render(<GrafanaEmbed />);

      const iframe = container.querySelector('iframe');
      expect(iframe?.src).toContain('infrastructure-monitoring');
    });

    it('should render LLM Cost dashboard when selected by default', () => {
      render(<GrafanaEmbed defaultDashboard="llm-cost" />);

      const llmDashboard = GRAFANA_DASHBOARDS.find(d => d.id === 'llm-cost');
      expect(screen.getByText(llmDashboard!.description.en)).toBeInTheDocument();
    });
  });

  describe('Iframe URL Generation', () => {
    it('should generate correct iframe URL with kiosk mode', () => {
      const { container } = render(<GrafanaEmbed />);

      const iframe = container.querySelector('iframe');
      expect(iframe?.src).toContain('kiosk=tv');
    });

    it('should include auto-refresh parameter', () => {
      const { container } = render(<GrafanaEmbed autoRefresh="1m" />);

      const iframe = container.querySelector('iframe');
      expect(iframe?.src).toContain('refresh=1m');
    });

    it('should include time range parameters', () => {
      const { container } = render(<GrafanaEmbed timeRange={{ from: 'now-6h', to: 'now' }} />);

      const iframe = container.querySelector('iframe');
      expect(iframe?.src).toContain('from=now-6h');
      expect(iframe?.src).toContain('to=now');
    });

    it('should include dashboard UID in URL', () => {
      const { container } = render(<GrafanaEmbed />);

      const iframe = container.querySelector('iframe');
      expect(iframe?.src).toContain('infrastructure-monitoring');
    });
  });

  describe('Loading States', () => {
    it('should show loading skeleton initially', () => {
      render(<GrafanaEmbed />);

      expect(screen.getByText('Loading dashboard...')).toBeInTheDocument();
    });

    it('should show loading skeleton in Italian locale', () => {
      render(<GrafanaEmbed locale="it" />);

      expect(screen.getByText('Caricamento dashboard...')).toBeInTheDocument();
    });

    it('should hide loading skeleton after iframe loads', async () => {
      const { container } = render(<GrafanaEmbed />);

      const iframe = container.querySelector('iframe');
      fireEvent.load(iframe!);

      await waitFor(() => {
        expect(screen.queryByText('Loading dashboard...')).not.toBeInTheDocument();
      });
    });
  });

  describe('Error Handling', () => {
    it('should render iframe with error handler', () => {
      const { container } = render(<GrafanaEmbed />);

      const iframe = container.querySelector('iframe');
      expect(iframe).toBeInTheDocument();
    });

    it('should show external link button for fallback', () => {
      render(<GrafanaEmbed />);

      const externalLinkButton = screen.getByTitle('Open in Grafana');
      expect(externalLinkButton).toBeInTheDocument();
    });
  });

  describe('External Link Functionality', () => {
    it('should open Grafana in new window when clicking external link button', () => {
      render(<GrafanaEmbed />);

      const externalLinkButton = screen.getByTitle('Open in Grafana');
      fireEvent.click(externalLinkButton);

      expect(mockWindowOpen).toHaveBeenCalled();
      const callArg = mockWindowOpen.mock.calls[0][0];
      expect(callArg).toContain('infrastructure-monitoring');
    });

    it('should open correct dashboard URL in external window', () => {
      render(<GrafanaEmbed defaultDashboard="llm-cost" />);

      const externalLinkButton = screen.getByTitle('Open in Grafana');
      fireEvent.click(externalLinkButton);

      expect(mockWindowOpen).toHaveBeenCalled();
      const callArg = mockWindowOpen.mock.calls[0][0];
      expect(callArg).toContain('llm-cost-monitoring');
    });

    it('should open external link without kiosk TV mode', () => {
      render(<GrafanaEmbed />);

      const externalLinkButton = screen.getByTitle('Open in Grafana');
      fireEvent.click(externalLinkButton);

      expect(mockWindowOpen).toHaveBeenCalled();
      const callArg = mockWindowOpen.mock.calls[0][0];
      // External links should not have kiosk=tv (uses kiosk= empty for no kiosk)
      expect(callArg).toContain('kiosk=');
      expect(callArg).not.toContain('kiosk=tv');
    });
  });

  describe('Refresh Functionality', () => {
    it('should reload iframe when clicking refresh button', async () => {
      const { container } = render(<GrafanaEmbed />);

      const refreshButton = screen.getByTitle('Refresh');
      fireEvent.click(refreshButton);

      await waitFor(() => {
        const iframe = container.querySelector('iframe');
        expect(iframe).toBeInTheDocument();
      });
    });

    it('should show loading state after refresh', async () => {
      render(<GrafanaEmbed />);

      const refreshButton = screen.getByTitle('Refresh');
      fireEvent.click(refreshButton);

      await waitFor(() => {
        expect(screen.getByText('Loading dashboard...')).toBeInTheDocument();
      });
    });
  });

  describe('Accessibility', () => {
    it('should have proper iframe title', () => {
      const { container } = render(<GrafanaEmbed />);

      const iframe = container.querySelector('iframe');
      expect(iframe?.title).toBe('Infrastructure Monitoring');
    });

    it('should have proper iframe sandbox attributes', () => {
      const { container } = render(<GrafanaEmbed />);

      const iframe = container.querySelector('iframe');
      const sandboxAttr = iframe?.getAttribute('sandbox');
      expect(sandboxAttr).toContain('allow-same-origin');
      expect(sandboxAttr).toContain('allow-scripts');
    });

    it('should have proper buttons with titles', () => {
      render(<GrafanaEmbed />);

      expect(screen.getByTitle('Refresh')).toBeInTheDocument();
      expect(screen.getByTitle('Open in Grafana')).toBeInTheDocument();
    });
  });

  describe('Responsive Design', () => {
    it('should hide dashboard names on small screens', () => {
      render(<GrafanaEmbed />);

      const tabs = screen.getAllByRole('tab');
      tabs.forEach(tab => {
        const spanElement = tab.querySelector('span.hidden.sm\\:inline');
        expect(spanElement).toBeInTheDocument();
      });
    });
  });

  describe('Time Range Display', () => {
    it('should display auto-refresh interval', () => {
      render(<GrafanaEmbed autoRefresh="1m" />);

      expect(screen.getByText(/Auto-refresh:/)).toBeInTheDocument();
      expect(screen.getByText(/1m/)).toBeInTheDocument();
    });

    it('should display time range', () => {
      render(<GrafanaEmbed timeRange={{ from: 'now-6h', to: 'now' }} />);

      expect(screen.getByText(/Time range:/)).toBeInTheDocument();
      expect(screen.getByText(/now-6h → now/)).toBeInTheDocument();
    });

    it('should display in Italian locale', () => {
      render(<GrafanaEmbed locale="it" autoRefresh="1m" />);

      expect(screen.getByText(/Auto-aggiornamento:/)).toBeInTheDocument();
      expect(screen.getByText(/Intervallo:/)).toBeInTheDocument();
    });
  });
});
