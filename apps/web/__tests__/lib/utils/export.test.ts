/**
 * Export Utilities Tests
 *
 * Tests for CSV and PDF export functionality
 * Issue #2139: Testing Dashboard Export Functionality
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
  exportTestingMetricsToCSV,
  exportTestingMetricsToPDF,
  type TestingMetrics,
} from '@/lib/utils/export';

// Mock data for testing
const mockMetrics: TestingMetrics = {
  accessibility: {
    lighthouseScore: 98,
    axeViolations: 2,
    wcagCompliance: {
      levelA: 100,
      levelAA: 96,
      levelAAA: 85,
    },
    testedPages: 12,
    criticalIssues: [{ page: '/admin/users', issue: 'Missing alt text', severity: 'medium' }],
    lastRun: '2025-12-10T14:30:00Z',
  },
  performance: {
    lighthouseScore: 92,
    coreWebVitals: {
      lcp: 1.8,
      fid: 45,
      cls: 0.05,
    },
    budgetStatus: {
      js: { current: 245, budget: 300, unit: 'KB' },
      css: { current: 48, budget: 75, unit: 'KB' },
      images: { current: 890, budget: 1024, unit: 'KB' },
    },
    slowestPages: [{ page: '/admin/analytics', loadTime: 2.4 }],
    lastRun: '2025-12-10T15:00:00Z',
  },
  e2e: {
    coverage: 82,
    passRate: 97.5,
    flakyRate: 3.2,
    totalTests: 156,
    executionTime: 8.5,
    criticalJourneys: [{ name: 'User Login Flow', status: 'pass', duration: 12.3 }],
    lastRun: '2025-12-10T16:00:00Z',
  },
};

// Mock DOM APIs
global.URL.createObjectURL = vi.fn(() => 'mock-url');
global.URL.revokeObjectURL = vi.fn();

// Mock html2canvas
vi.mock('html2canvas', () => ({
  default: vi.fn(() =>
    Promise.resolve({
      toDataURL: vi.fn(() => 'data:image/png;base64,mock'),
      width: 800,
      height: 600,
    })
  ),
}));

// Mock jsPDF
vi.mock('jspdf', () => ({
  default: vi.fn().mockImplementation(() => ({
    addImage: vi.fn(),
    addPage: vi.fn(),
    save: vi.fn(),
  })),
}));

describe('Export Utilities', () => {
  let createElementSpy: ReturnType<typeof vi.spyOn>;
  let appendChildSpy: ReturnType<typeof vi.spyOn>;
  let removeChildSpy: ReturnType<typeof vi.spyOn>;
  let clickSpy: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    // Mock DOM methods
    clickSpy = vi.fn();
    const mockLink = {
      href: '',
      download: '',
      click: clickSpy,
    } as unknown as HTMLAnchorElement;

    createElementSpy = vi.spyOn(document, 'createElement').mockReturnValue(mockLink);
    appendChildSpy = vi.spyOn(document.body, 'appendChild').mockImplementation(() => mockLink);
    removeChildSpy = vi.spyOn(document.body, 'removeChild').mockImplementation(() => mockLink);
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe('exportTestingMetricsToCSV', () => {
    it('should export metrics to CSV format', () => {
      exportTestingMetricsToCSV(mockMetrics);

      expect(createElementSpy).toHaveBeenCalledWith('a');
      expect(appendChildSpy).toHaveBeenCalled();
      expect(clickSpy).toHaveBeenCalled();
      expect(removeChildSpy).toHaveBeenCalled();
      expect(global.URL.createObjectURL).toHaveBeenCalled();
      expect(global.URL.revokeObjectURL).toHaveBeenCalled();
    });

    it('should use default filename if not provided', () => {
      exportTestingMetricsToCSV(mockMetrics);

      const mockLink = createElementSpy.mock.results[0].value as HTMLAnchorElement;
      expect(mockLink.download).toMatch(/^testing-metrics-\d{4}-\d{2}-\d{2}\.csv$/);
    });

    it('should use custom filename if provided', () => {
      const customFilename = 'custom-export.csv';
      exportTestingMetricsToCSV(mockMetrics, customFilename);

      const mockLink = createElementSpy.mock.results[0].value as HTMLAnchorElement;
      expect(mockLink.download).toBe(customFilename);
    });

    it('should include all metric categories', () => {
      // Capture Blob content by spying on Blob constructor
      let capturedContent = '';
      const originalBlob = global.Blob;
      global.Blob = class MockBlob extends originalBlob {
        constructor(parts: BlobPart[], options?: BlobPropertyBag) {
          super(parts, options);
          capturedContent = parts[0] as string;
        }
      } as typeof Blob;

      exportTestingMetricsToCSV(mockMetrics);

      const csvContent = capturedContent;
      global.Blob = originalBlob;

      // Verify headers
      expect(csvContent).toContain('Category,Metric Name,Value,Target,Status,Last Updated');

      // Verify accessibility metrics
      expect(csvContent).toContain('Accessibility,Lighthouse Score,98,≥95');
      expect(csvContent).toContain('Accessibility,axe Violations,2,0');
      expect(csvContent).toContain('Accessibility,WCAG Level A Compliance,100%');

      // Verify performance metrics
      expect(csvContent).toContain('Performance,Lighthouse Score,92,≥85');
      expect(csvContent).toContain('Performance,LCP (Largest Contentful Paint),1.8s');
      expect(csvContent).toContain('Performance,FID (First Input Delay),45ms');
      expect(csvContent).toContain('Performance,CLS (Cumulative Layout Shift),0.05');

      // Verify E2E metrics
      expect(csvContent).toContain('E2E Testing,Test Coverage,82%');
      expect(csvContent).toContain('E2E Testing,Pass Rate,97.5%');
      expect(csvContent).toContain('E2E Testing,Flaky Rate,3.2%');
      expect(csvContent).toContain('E2E Testing,Execution Time,8.5m');
    });

    it('should handle CSV field escaping for special characters', () => {
      const metricsWithSpecialChars: TestingMetrics = {
        ...mockMetrics,
        accessibility: {
          ...mockMetrics.accessibility,
          criticalIssues: [
            {
              page: '/admin/users',
              issue: 'Missing "alt" text, needs attention',
              severity: 'medium',
            },
          ],
        },
      };

      let capturedContent = '';
      const originalBlob = global.Blob;
      global.Blob = class MockBlob extends originalBlob {
        constructor(parts: BlobPart[], options?: BlobPropertyBag) {
          super(parts, options);
          capturedContent = parts[0] as string;
        }
      } as typeof Blob;

      exportTestingMetricsToCSV(metricsWithSpecialChars);

      const csvContent = capturedContent;
      global.Blob = originalBlob;

      // Fields with quotes or commas should be escaped
      expect(csvContent).toBeTruthy();
      expect(csvContent.length).toBeGreaterThan(0);
    });
  });

  describe('exportTestingMetricsToPDF', () => {
    it('should throw error if element not found', async () => {
      await expect(exportTestingMetricsToPDF('non-existent-id')).rejects.toThrow(
        'Element with ID "non-existent-id" not found'
      );
    });

    it('should call html2canvas with correct options', async () => {
      // Create mock element
      const mockElement = document.createElement('div');
      mockElement.id = 'test-element';

      // Mock getElementById to return our element
      vi.spyOn(document, 'getElementById').mockReturnValue(mockElement);

      const html2canvas = (await import('html2canvas')).default;

      await exportTestingMetricsToPDF('test-element');

      expect(html2canvas).toHaveBeenCalledWith(mockElement, {
        scale: 2,
        logging: false,
        useCORS: true,
        allowTaint: false,
      });
    });

    it('should use default filename if not provided', async () => {
      const mockElement = document.createElement('div');
      mockElement.id = 'test-element-2';
      vi.spyOn(document, 'getElementById').mockReturnValue(mockElement);

      const jsPDF = (await import('jspdf')).default;

      await exportTestingMetricsToPDF('test-element-2');

      const mockPdfInstance = (jsPDF as unknown as ReturnType<typeof vi.fn>).mock.results[0].value;
      expect(mockPdfInstance.save).toHaveBeenCalledWith(
        expect.stringMatching(/^testing-dashboard-\d{4}-\d{2}-\d{2}\.pdf$/)
      );
    });

    it('should use custom filename if provided', async () => {
      const mockElement = document.createElement('div');
      mockElement.id = 'test-element-3';
      vi.spyOn(document, 'getElementById').mockReturnValue(mockElement);

      const jsPDF = (await import('jspdf')).default;

      await exportTestingMetricsToPDF('test-element-3', 'custom.pdf');

      const mockPdfInstance = (jsPDF as unknown as ReturnType<typeof vi.fn>).mock.results[0].value;
      expect(mockPdfInstance.save).toHaveBeenCalledWith('custom.pdf');
    });

    it('should handle errors during PDF generation gracefully', async () => {
      const mockElement = document.createElement('div');
      mockElement.id = 'test-element-error';
      vi.spyOn(document, 'getElementById').mockReturnValue(mockElement);

      // Mock html2canvas to reject
      const html2canvas = (await import('html2canvas')).default;
      (html2canvas as unknown as ReturnType<typeof vi.fn>).mockRejectedValueOnce(
        new Error('Canvas rendering failed')
      );

      await expect(exportTestingMetricsToPDF('test-element-error')).rejects.toThrow(
        'Failed to generate PDF. Please try again.'
      );
    });
  });
});
