/**
 * Export Utilities
 *
 * Provides CSV and PDF export functionality for dashboard data.
 * Supports browser-based downloads using Blob API.
 *
 * Issue #2139: Testing Dashboard Export Functionality
 */

import html2canvas from 'html2canvas';
import jsPDF from 'jspdf';

import { logger } from '@/lib/logger';

/**
 * Testing metrics data structure for export
 */
export interface TestingMetrics {
  accessibility: {
    lighthouseScore: number;
    axeViolations: number;
    wcagCompliance: {
      levelA: number;
      levelAA: number;
      levelAAA: number;
    };
    testedPages: number;
    criticalIssues: Array<{
      page: string;
      issue: string;
      severity: string;
    }>;
    lastRun: string;
  };
  performance: {
    lighthouseScore: number;
    coreWebVitals: {
      lcp: number;
      fid: number;
      cls: number;
    };
    budgetStatus: {
      js: { current: number; budget: number; unit: string };
      css: { current: number; budget: number; unit: string };
      images: { current: number; budget: number; unit: string };
    };
    slowestPages: Array<{
      page: string;
      loadTime: number;
    }>;
    lastRun: string;
  };
  e2e: {
    coverage: number;
    passRate: number;
    flakyRate: number;
    totalTests: number;
    executionTime: number;
    criticalJourneys: Array<{
      name: string;
      status: string;
      duration: number;
    }>;
    lastRun: string;
  };
}

/**
 * CSV row data structure
 */
interface CSVRow {
  category: string;
  metric: string;
  value: string;
  target: string;
  status: string;
  lastUpdated: string;
}

/**
 * Convert testing metrics to CSV format and trigger download
 *
 * @param metrics - Testing metrics data
 * @param filename - Optional filename (default: testing-metrics-YYYY-MM-DD.csv)
 */
export function exportTestingMetricsToCSV(metrics: TestingMetrics, filename?: string): void {
  const rows: CSVRow[] = [];

  // Accessibility Metrics
  rows.push({
    category: 'Accessibility',
    metric: 'Lighthouse Score',
    value: metrics.accessibility.lighthouseScore.toString(),
    target: '≥95',
    status: metrics.accessibility.lighthouseScore >= 95 ? 'Pass' : 'Needs Work',
    lastUpdated: new Date(metrics.accessibility.lastRun).toLocaleString(),
  });

  rows.push({
    category: 'Accessibility',
    metric: 'axe Violations',
    value: metrics.accessibility.axeViolations.toString(),
    target: '0',
    status: metrics.accessibility.axeViolations === 0 ? 'Pass' : 'Issues Found',
    lastUpdated: new Date(metrics.accessibility.lastRun).toLocaleString(),
  });

  rows.push({
    category: 'Accessibility',
    metric: 'WCAG Level A Compliance',
    value: `${metrics.accessibility.wcagCompliance.levelA}%`,
    target: '100%',
    status: metrics.accessibility.wcagCompliance.levelA === 100 ? 'Pass' : 'Needs Work',
    lastUpdated: new Date(metrics.accessibility.lastRun).toLocaleString(),
  });

  rows.push({
    category: 'Accessibility',
    metric: 'WCAG Level AA Compliance',
    value: `${metrics.accessibility.wcagCompliance.levelAA}%`,
    target: '100%',
    status: metrics.accessibility.wcagCompliance.levelAA === 100 ? 'Pass' : 'Needs Work',
    lastUpdated: new Date(metrics.accessibility.lastRun).toLocaleString(),
  });

  rows.push({
    category: 'Accessibility',
    metric: 'WCAG Level AAA Compliance',
    value: `${metrics.accessibility.wcagCompliance.levelAAA}%`,
    target: '100%',
    status: metrics.accessibility.wcagCompliance.levelAAA === 100 ? 'Pass' : 'Needs Work',
    lastUpdated: new Date(metrics.accessibility.lastRun).toLocaleString(),
  });

  // Performance Metrics
  rows.push({
    category: 'Performance',
    metric: 'Lighthouse Score',
    value: metrics.performance.lighthouseScore.toString(),
    target: '≥85',
    status: metrics.performance.lighthouseScore >= 85 ? 'Pass' : 'Needs Work',
    lastUpdated: new Date(metrics.performance.lastRun).toLocaleString(),
  });

  rows.push({
    category: 'Performance',
    metric: 'LCP (Largest Contentful Paint)',
    value: `${metrics.performance.coreWebVitals.lcp}s`,
    target: '<2.5s',
    status: metrics.performance.coreWebVitals.lcp < 2.5 ? 'Good' : 'Needs Work',
    lastUpdated: new Date(metrics.performance.lastRun).toLocaleString(),
  });

  rows.push({
    category: 'Performance',
    metric: 'FID (First Input Delay)',
    value: `${metrics.performance.coreWebVitals.fid}ms`,
    target: '<100ms',
    status: metrics.performance.coreWebVitals.fid < 100 ? 'Good' : 'Needs Work',
    lastUpdated: new Date(metrics.performance.lastRun).toLocaleString(),
  });

  rows.push({
    category: 'Performance',
    metric: 'CLS (Cumulative Layout Shift)',
    value: metrics.performance.coreWebVitals.cls.toString(),
    target: '<0.1',
    status: metrics.performance.coreWebVitals.cls < 0.1 ? 'Good' : 'Needs Work',
    lastUpdated: new Date(metrics.performance.lastRun).toLocaleString(),
  });

  // E2E Metrics
  rows.push({
    category: 'E2E Testing',
    metric: 'Test Coverage',
    value: `${metrics.e2e.coverage}%`,
    target: '≥80%',
    status: metrics.e2e.coverage >= 80 ? 'Pass' : 'Needs Work',
    lastUpdated: new Date(metrics.e2e.lastRun).toLocaleString(),
  });

  rows.push({
    category: 'E2E Testing',
    metric: 'Pass Rate',
    value: `${metrics.e2e.passRate}%`,
    target: '>95%',
    status: metrics.e2e.passRate >= 95 ? 'Excellent' : 'Needs Work',
    lastUpdated: new Date(metrics.e2e.lastRun).toLocaleString(),
  });

  rows.push({
    category: 'E2E Testing',
    metric: 'Flaky Rate',
    value: `${metrics.e2e.flakyRate}%`,
    target: '<5%',
    status: metrics.e2e.flakyRate < 5 ? 'Good' : 'High',
    lastUpdated: new Date(metrics.e2e.lastRun).toLocaleString(),
  });

  rows.push({
    category: 'E2E Testing',
    metric: 'Execution Time',
    value: `${metrics.e2e.executionTime}m`,
    target: '<10min',
    status: metrics.e2e.executionTime < 10 ? 'Fast' : 'Slow',
    lastUpdated: new Date(metrics.e2e.lastRun).toLocaleString(),
  });

  // Convert to CSV format
  const headers = ['Category', 'Metric Name', 'Value', 'Target', 'Status', 'Last Updated'];
  const csvContent = [
    headers.join(','),
    ...rows.map(row =>
      [
        escapeCSVField(row.category),
        escapeCSVField(row.metric),
        escapeCSVField(row.value),
        escapeCSVField(row.target),
        escapeCSVField(row.status),
        escapeCSVField(row.lastUpdated),
      ].join(',')
    ),
  ].join('\n');

  // Trigger download
  const defaultFilename = `testing-metrics-${new Date().toISOString().split('T')[0]}.csv`;
  downloadFile(csvContent, filename || defaultFilename, 'text/csv;charset=utf-8;');
}

/**
 * Export testing dashboard to PDF using html2canvas
 *
 * @param elementId - DOM element ID to capture
 * @param filename - Optional filename (default: testing-dashboard-YYYY-MM-DD.pdf)
 */
export async function exportTestingMetricsToPDF(
  elementId: string,
  filename?: string
): Promise<void> {
  const element = document.getElementById(elementId);
  if (!element) {
    throw new Error(`Element with ID "${elementId}" not found`);
  }

  try {
    // Capture the element as canvas
    const canvas = await html2canvas(element, {
      scale: 2, // Higher quality
      logging: false,
      useCORS: true,
      allowTaint: false,
    });

    // Calculate PDF dimensions (A4 portrait)
    const imgWidth = 210; // A4 width in mm
    const pageHeight = 297; // A4 height in mm
    const imgHeight = (canvas.height * imgWidth) / canvas.width;
    let heightLeft = imgHeight;

    const imgData = canvas.toDataURL('image/png');
    const pdf = new jsPDF('p', 'mm', 'a4');
    let position = 0;

    // Add first page
    pdf.addImage(imgData, 'PNG', 0, position, imgWidth, imgHeight);
    heightLeft -= pageHeight;

    // Add additional pages if content overflows
    while (heightLeft > 0) {
      position = heightLeft - imgHeight;
      pdf.addPage();
      pdf.addImage(imgData, 'PNG', 0, position, imgWidth, imgHeight);
      heightLeft -= pageHeight;
    }

    // Trigger download
    const defaultFilename = `testing-dashboard-${new Date().toISOString().split('T')[0]}.pdf`;
    pdf.save(filename || defaultFilename);
  } catch (error) {
    logger.error('PDF export failed:', error);
    throw new Error('Failed to generate PDF. Please try again.');
  }
}

/**
 * Escape CSV field to handle commas, quotes, and newlines
 */
function escapeCSVField(field: string): string {
  if (field.includes(',') || field.includes('"') || field.includes('\n')) {
    return `"${field.replace(/"/g, '""')}"`;
  }
  return field;
}

/**
 * Trigger browser download for a file
 *
 * @param content - File content
 * @param filename - Filename for download
 * @param mimeType - MIME type of the file
 */
function downloadFile(content: string, filename: string, mimeType: string): void {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}
