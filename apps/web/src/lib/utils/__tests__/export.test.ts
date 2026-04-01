/**
 * export.ts — Tests for lazy-loaded PDF export and CSV export
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('html2canvas', () => ({
  default: vi.fn().mockResolvedValue({
    height: 400,
    width: 800,
    toDataURL: vi.fn().mockReturnValue('data:image/png;base64,MOCK'),
  }),
}));

vi.mock('jspdf', () => ({
  default: vi.fn().mockImplementation(() => ({
    addImage: vi.fn(),
    addPage: vi.fn(),
    save: vi.fn(),
  })),
}));

describe('export.ts', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('exportTestingMetricsToPDF resolves when element exists', async () => {
    const el = document.createElement('div');
    el.id = 'test-export-element';
    el.style.height = '400px';
    el.textContent = 'Dashboard content';
    document.body.appendChild(el);

    const { exportTestingMetricsToPDF } = await import('../export');
    await expect(exportTestingMetricsToPDF('test-export-element')).resolves.toBeUndefined();

    document.body.removeChild(el);
  });

  it('exportTestingMetricsToPDF throws when element not found', async () => {
    const { exportTestingMetricsToPDF } = await import('../export');
    await expect(exportTestingMetricsToPDF('element-does-not-exist')).rejects.toThrow(
      'Element with ID "element-does-not-exist" not found'
    );
  });

  it('exportTestingMetricsToCSV does not throw with valid metrics', async () => {
    const mockCreateObjectURL = vi.fn(() => 'blob:mock-url');
    const mockRevokeObjectURL = vi.fn();
    vi.stubGlobal('URL', {
      createObjectURL: mockCreateObjectURL,
      revokeObjectURL: mockRevokeObjectURL,
    });

    const appendSpy = vi
      .spyOn(document.body, 'appendChild')
      .mockReturnValue(document.body as unknown as HTMLElement);
    const removeSpy = vi
      .spyOn(document.body, 'removeChild')
      .mockReturnValue(document.body as unknown as ChildNode);

    const { exportTestingMetricsToCSV } = await import('../export');
    const metrics = {
      accessibility: {
        lighthouseScore: 95,
        axeViolations: 0,
        wcagCompliance: { levelA: 100, levelAA: 100, levelAAA: 80 },
        testedPages: 5,
        criticalIssues: [],
        lastRun: new Date().toISOString(),
      },
      performance: {
        lighthouseScore: 90,
        coreWebVitals: { lcp: 1.5, fid: 50, cls: 0.05 },
        budgetStatus: {
          js: { current: 100, budget: 200, unit: 'KB' },
          css: { current: 20, budget: 50, unit: 'KB' },
          images: { current: 500, budget: 1000, unit: 'KB' },
        },
        slowestPages: [],
        lastRun: new Date().toISOString(),
      },
      e2e: {
        coverage: 85,
        passRate: 98,
        flakyRate: 2,
        totalTests: 200,
        executionTime: 8,
        criticalJourneys: [],
        lastRun: new Date().toISOString(),
      },
    };

    expect(() => exportTestingMetricsToCSV(metrics)).not.toThrow();

    appendSpy.mockRestore();
    removeSpy.mockRestore();
    vi.unstubAllGlobals();
  });
});
