/**
 * ConfigAuditLogDialog Component Tests (Issue #1836)
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ConfigAuditLogDialog } from '../ConfigAuditLogDialog';
import { api } from '../../../lib/api';
import type { ConfigurationHistoryDto } from '../../../lib/api/schemas/config.schemas';

vi.mock('../../../lib/api', async importOriginal => {
  const actual = await importOriginal<typeof import('../../../lib/api')>();
  return {
    ...actual,
    api: {
      config: {
        getHistory: vi.fn(),
      },
    },
  };
});

vi.mock('sonner', () => ({
  toast: {
    error: vi.fn(),
    success: vi.fn(),
  },
}));

const mockApi = api as Mocked<typeof api>;

function entry(overrides: Partial<ConfigurationHistoryDto>): ConfigurationHistoryDto {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    configurationId: '11111111-1111-1111-1111-111111111111',
    key: 'Features:RagCaching',
    oldValue: 'false',
    newValue: 'true',
    version: 2,
    changedAt: '2026-06-01T10:00:00.000Z',
    changedByUserId: '22222222-2222-2222-2222-222222222222',
    changeReason: 'admin toggled flag',
    ...overrides,
  };
}

describe('ConfigAuditLogDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('does not fetch history when closed', () => {
    const onOpenChange = vi.fn();
    render(
      <ConfigAuditLogDialog
        open={false}
        onOpenChange={onOpenChange}
        configurationIds={['11111111-1111-1111-1111-111111111111']}
      />
    );

    expect(mockApi.config.getHistory).not.toHaveBeenCalled();
  });

  it('aggregates history from all configs and renders the merged timeline sorted desc', async () => {
    const olderEntry = entry({
      id: '00000000-0000-0000-0000-000000000003',
      configurationId: '11111111-1111-1111-1111-111111111111',
      key: 'Features:RagCaching',
      changedAt: '2026-06-01T08:00:00.000Z',
      version: 1,
      oldValue: '',
      newValue: 'true',
      changeReason: 'Configuration created',
    });
    const newerEntry = entry({
      id: '00000000-0000-0000-0000-000000000002',
      configurationId: '33333333-3333-3333-3333-333333333333',
      key: 'Features:StreamingResponses',
      changedAt: '2026-06-02T15:30:00.000Z',
      version: 5,
      oldValue: 'true',
      newValue: 'false',
      changeReason: 'admin toggled flag',
    });

    mockApi.config.getHistory = vi
      .fn()
      .mockResolvedValueOnce([olderEntry])
      .mockResolvedValueOnce([newerEntry]);

    render(
      <ConfigAuditLogDialog
        open
        onOpenChange={vi.fn()}
        configurationIds={[
          '11111111-1111-1111-1111-111111111111',
          '33333333-3333-3333-3333-333333333333',
        ]}
      />
    );

    await waitFor(() => {
      expect(screen.getByTestId('config-audit-log-entries')).toBeInTheDocument();
    });

    const rendered = screen
      .getAllByTestId(/^config-audit-log-entry-/)
      .map(el => el.getAttribute('data-testid'));

    expect(rendered).toEqual([
      'config-audit-log-entry-00000000-0000-0000-0000-000000000002',
      'config-audit-log-entry-00000000-0000-0000-0000-000000000003',
    ]);
    expect(screen.getByText('StreamingResponses')).toBeInTheDocument();
    expect(screen.getByText('RagCaching')).toBeInTheDocument();
  });

  it('renders an empty state when configurationIds is empty', () => {
    render(<ConfigAuditLogDialog open onOpenChange={vi.fn()} configurationIds={[]} />);

    expect(screen.getByTestId('config-audit-log-empty')).toBeInTheDocument();
    expect(mockApi.config.getHistory).not.toHaveBeenCalled();
  });

  it('renders an empty state when all configs return no history', async () => {
    mockApi.config.getHistory = vi.fn().mockResolvedValue([]);

    render(
      <ConfigAuditLogDialog
        open
        onOpenChange={vi.fn()}
        configurationIds={['11111111-1111-1111-1111-111111111111']}
      />
    );

    await waitFor(() => {
      expect(screen.getByTestId('config-audit-log-empty')).toBeInTheDocument();
    });
    expect(screen.getByText(/No history available yet/i)).toBeInTheDocument();
  });

  it('keeps the dialog usable when one config fails (per-config catch)', async () => {
    mockApi.config.getHistory = vi
      .fn()
      .mockRejectedValueOnce(new Error('boom'))
      .mockResolvedValueOnce([entry({})]);

    render(
      <ConfigAuditLogDialog
        open
        onOpenChange={vi.fn()}
        configurationIds={[
          '11111111-1111-1111-1111-111111111111',
          '33333333-3333-3333-3333-333333333333',
        ]}
      />
    );

    await waitFor(() => {
      expect(screen.getByTestId('config-audit-log-entries')).toBeInTheDocument();
    });
    // Successful config rendered, failed one silently dropped.
    expect(screen.getAllByTestId(/^config-audit-log-entry-/)).toHaveLength(1);
  });

  it('closes via the footer button', async () => {
    const onOpenChange = vi.fn();
    const user = userEvent.setup();
    mockApi.config.getHistory = vi.fn().mockResolvedValue([]);

    render(
      <ConfigAuditLogDialog
        open
        onOpenChange={onOpenChange}
        configurationIds={['11111111-1111-1111-1111-111111111111']}
      />
    );

    await user.click(screen.getByTestId('btn-close-audit-log'));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });
});
