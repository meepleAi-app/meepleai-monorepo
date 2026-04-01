import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { MauTab } from '../MauTab';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getActiveAiUsers: vi.fn().mockResolvedValue({
        totalActiveUsers: 42,
        aiChatUsers: 10,
        pdfUploadUsers: 15,
        agentUsers: 17,
        dailyBreakdown: [],
        periodStart: '2026-01-01',
        periodEnd: '2026-01-31',
      }),
    },
  },
}));

describe('MauTab', () => {
  it('renders MauDashboard content', async () => {
    render(<MauTab />);
    expect(await screen.findByText('Active AI Users')).toBeInTheDocument();
  });
});
