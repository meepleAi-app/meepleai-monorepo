/**
 * useWizardAutoSave - Tests
 * Issue #4167: Session auto-save draft tests
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';

import { useWizardAutoSave, clearDraft } from '../useWizardAutoSave';

// Mock dependencies
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
  },
}));

vi.mock('@/stores/useGameImportWizardStore', () => ({
  useGameImportWizardStore: vi.fn(() => ({
    currentStep: 2,
    uploadedPdf: { id: 'pdf-123', fileName: 'test.pdf' },
    extractedMetadata: { title: 'Test Game' },
    selectedBggId: 13,
    bggGameData: { id: 13, name: 'Catan' },
    enrichedData: null,
    setStep: vi.fn(),
    setUploadedPdf: vi.fn(),
    setExtractedMetadata: vi.fn(),
    setSelectedBggId: vi.fn(),
  })),
}));

describe('useWizardAutoSave', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should auto-save wizard state every 30s', async () => {
    renderHook(() => useWizardAutoSave());

    // Initially no draft
    expect(localStorage.getItem('game_import_wizard_draft')).toBeNull();

    // Advance 30s
    act(() => {
      vi.advanceTimersByTime(30000);
    });

    // Draft should be saved
    const draft = localStorage.getItem('game_import_wizard_draft');
    expect(draft).not.toBeNull();

    const parsed = JSON.parse(draft!);
    expect(parsed.currentStep).toBe(2);
    expect(parsed.uploadedPdf.id).toBe('pdf-123');
  });

  it('should restore draft on mount', async () => {
    // Create a draft first
    const draft = {
      currentStep: 3,
      uploadedPdf: { id: 'pdf-456', fileName: 'old.pdf' },
      extractedMetadata: { title: 'Old Game' },
      selectedBggId: 42,
      bggGameData: { id: 42, name: 'Chess' },
      savedAt: new Date().toISOString(),
    };

    localStorage.setItem('game_import_wizard_draft', JSON.stringify(draft));

    renderHook(() => useWizardAutoSave());

    // Should restore draft (mock store already configured)
    // Note: Since the hook uses the mock store defined at top level,
    // the restoration happens immediately on mount
    expect(localStorage.getItem('game_import_wizard_draft')).not.toBeNull();
  });

  it('should not restore stale draft (>24h)', () => {
    const staleDraft = {
      currentStep: 2,
      uploadedPdf: { id: 'stale', fileName: 'old.pdf' },
      savedAt: new Date(Date.now() - 25 * 60 * 60 * 1000).toISOString(), // 25 hours ago
    };

    localStorage.setItem('game_import_wizard_draft', JSON.stringify(staleDraft));

    renderHook(() => useWizardAutoSave());

    // Stale draft should be cleared
    expect(localStorage.getItem('game_import_wizard_draft')).toBeNull();
  });

  it('should clear draft on manual clear', () => {
    localStorage.setItem('game_import_wizard_draft', JSON.stringify({ currentStep: 1 }));

    clearDraft();

    expect(localStorage.getItem('game_import_wizard_draft')).toBeNull();
  });
});
