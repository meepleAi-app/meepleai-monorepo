/**
 * PDF Wizard Store Tests - Issue #4141
 *
 * Test coverage:
 * - Initial state
 * - Step navigation
 * - Step 1 data (PDF upload)
 * - Step 2 data (manual fields)
 * - Step 3 data (BGG match)
 * - Reset functionality
 *
 * Target: >90% coverage
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';

import { usePdfWizardStore } from '../pdf-wizard-store';

describe('usePdfWizardStore', () => {
  beforeEach(() => {
    const { result } = renderHook(() => usePdfWizardStore());
    act(() => {
      result.current.reset();
    });
  });

  describe('Initial State', () => {
    it('should initialize with correct default values', () => {
      const { result } = renderHook(() => usePdfWizardStore());

      expect(result.current.currentStep).toBe(1);
      expect(result.current.pdfDocumentId).toBeNull();
      expect(result.current.qualityScore).toBe(0);
      expect(result.current.extractedTitle).toBe('');
      expect(result.current.manualFields).toEqual({});
      expect(result.current.duplicateWarnings).toEqual([]);
      expect(result.current.selectedBggId).toBeNull();
      expect(result.current.bggDetails).toBeNull();
    });
  });

  describe('Step Navigation', () => {
    it('should update current step', () => {
      const { result } = renderHook(() => usePdfWizardStore());

      act(() => {
        result.current.setCurrentStep(2);
      });

      expect(result.current.currentStep).toBe(2);

      act(() => {
        result.current.setCurrentStep(4);
      });

      expect(result.current.currentStep).toBe(4);
    });
  });

  describe('Step 1: PDF Upload', () => {
    it('should set step 1 data correctly', () => {
      const { result } = renderHook(() => usePdfWizardStore());

      const mockData = {
        pdfDocumentId: 'pdf-123',
        qualityScore: 0.85,
        extractedTitle: 'Catan',
      };

      act(() => {
        result.current.setStep1Data(mockData);
      });

      expect(result.current.pdfDocumentId).toBe('pdf-123');
      expect(result.current.qualityScore).toBe(0.85);
      expect(result.current.extractedTitle).toBe('Catan');
    });

    it('should handle quality score edge cases', () => {
      const { result } = renderHook(() => usePdfWizardStore());

      // High quality
      act(() => {
        result.current.setStep1Data({
          pdfDocumentId: 'pdf-1',
          qualityScore: 0.95,
          extractedTitle: 'High Quality Game',
        });
      });
      expect(result.current.qualityScore).toBe(0.95);

      // Low quality
      act(() => {
        result.current.setStep1Data({
          pdfDocumentId: 'pdf-2',
          qualityScore: 0.35,
          extractedTitle: 'Low Quality Game',
        });
      });
      expect(result.current.qualityScore).toBe(0.35);
    });
  });

  describe('Step 2: Manual Fields', () => {
    it('should set step 2 data with all fields', () => {
      const { result } = renderHook(() => usePdfWizardStore());

      const mockData = {
        manualFields: {
          minPlayers: 2,
          maxPlayers: 4,
          playingTime: 60,
          minAge: 10,
          description: 'A great strategy game',
        },
        duplicateWarnings: ['Similar game: Catan (ID: 123)'],
      };

      act(() => {
        result.current.setStep2Data(mockData);
      });

      expect(result.current.manualFields).toEqual(mockData.manualFields);
      expect(result.current.duplicateWarnings).toEqual(['Similar game: Catan (ID: 123)']);
    });

    it('should set step 2 data with partial fields', () => {
      const { result } = renderHook(() => usePdfWizardStore());

      const mockData = {
        manualFields: {
          minPlayers: 1,
          maxPlayers: 6,
        },
      };

      act(() => {
        result.current.setStep2Data(mockData);
      });

      expect(result.current.manualFields.minPlayers).toBe(1);
      expect(result.current.manualFields.maxPlayers).toBe(6);
      expect(result.current.manualFields.playingTime).toBeUndefined();
      expect(result.current.duplicateWarnings).toEqual([]);
    });

    it('should handle empty duplicate warnings', () => {
      const { result } = renderHook(() => usePdfWizardStore());

      act(() => {
        result.current.setStep2Data({
          manualFields: { minPlayers: 2 },
        });
      });

      expect(result.current.duplicateWarnings).toEqual([]);
    });
  });

  describe('Step 3: BGG Match', () => {
    it('should set step 3 data with BGG match', () => {
      const { result } = renderHook(() => usePdfWizardStore());

      const mockBggDetails = {
        id: 13,
        name: 'Catan',
        yearPublished: 1995,
        minPlayers: 3,
        maxPlayers: 4,
        playingTime: 90,
        minAge: 10,
        rating: 7.2,
        thumbnail: 'https://example.com/catan.jpg',
      };

      act(() => {
        result.current.setStep3Data({
          selectedBggId: 13,
          bggDetails: mockBggDetails,
        });
      });

      expect(result.current.selectedBggId).toBe(13);
      expect(result.current.bggDetails).toEqual(mockBggDetails);
    });

    it('should handle skip BGG (no match)', () => {
      const { result } = renderHook(() => usePdfWizardStore());

      act(() => {
        result.current.setStep3Data({
          selectedBggId: null,
          bggDetails: null,
        });
      });

      expect(result.current.selectedBggId).toBeNull();
      expect(result.current.bggDetails).toBeNull();
    });

    it('should update BGG selection', () => {
      const { result } = renderHook(() => usePdfWizardStore());

      // First selection
      act(() => {
        result.current.setStep3Data({
          selectedBggId: 13,
          bggDetails: {
            id: 13,
            name: 'Catan',
            yearPublished: 1995,
            minPlayers: 3,
            maxPlayers: 4,
            playingTime: 90,
            minAge: 10,
            rating: 7.2,
            thumbnail: null,
          },
        });
      });
      expect(result.current.selectedBggId).toBe(13);

      // Change selection
      act(() => {
        result.current.setStep3Data({
          selectedBggId: 822,
          bggDetails: {
            id: 822,
            name: 'Carcassonne',
            yearPublished: 2000,
            minPlayers: 2,
            maxPlayers: 5,
            playingTime: 45,
            minAge: 7,
            rating: 7.4,
            thumbnail: null,
          },
        });
      });
      expect(result.current.selectedBggId).toBe(822);
      expect(result.current.bggDetails?.name).toBe('Carcassonne');
    });
  });

  describe('Reset', () => {
    it('should reset all state to initial values', () => {
      const { result } = renderHook(() => usePdfWizardStore());

      // Set all steps
      act(() => {
        result.current.setStep1Data({
          pdfDocumentId: 'pdf-123',
          qualityScore: 0.85,
          extractedTitle: 'Catan',
        });
        result.current.setCurrentStep(2);
        result.current.setStep2Data({
          manualFields: { minPlayers: 2, maxPlayers: 4 },
          duplicateWarnings: ['Warning'],
        });
        result.current.setStep3Data({
          selectedBggId: 13,
          bggDetails: {
            id: 13,
            name: 'Catan',
            yearPublished: 1995,
            minPlayers: 3,
            maxPlayers: 4,
            playingTime: 90,
            minAge: 10,
            rating: 7.2,
            thumbnail: null,
          },
        });
      });

      // Verify data set
      expect(result.current.currentStep).toBe(2);
      expect(result.current.pdfDocumentId).toBe('pdf-123');

      // Reset
      act(() => {
        result.current.reset();
      });

      // Verify reset
      expect(result.current.currentStep).toBe(1);
      expect(result.current.pdfDocumentId).toBeNull();
      expect(result.current.qualityScore).toBe(0);
      expect(result.current.extractedTitle).toBe('');
      expect(result.current.manualFields).toEqual({});
      expect(result.current.duplicateWarnings).toEqual([]);
      expect(result.current.selectedBggId).toBeNull();
      expect(result.current.bggDetails).toBeNull();
    });
  });

  describe('Integration Flow', () => {
    it('should handle complete wizard flow', () => {
      const { result } = renderHook(() => usePdfWizardStore());

      // Step 1: Upload PDF
      act(() => {
        result.current.setStep1Data({
          pdfDocumentId: 'pdf-456',
          qualityScore: 0.78,
          extractedTitle: 'Ticket to Ride',
        });
        result.current.setCurrentStep(2);
      });

      expect(result.current.currentStep).toBe(2);
      expect(result.current.extractedTitle).toBe('Ticket to Ride');

      // Step 2: Manual fields
      act(() => {
        result.current.setStep2Data({
          manualFields: {
            minPlayers: 2,
            maxPlayers: 5,
            playingTime: 45,
            minAge: 8,
            description: 'Cross-country train adventure',
          },
        });
        result.current.setCurrentStep(3);
      });

      expect(result.current.currentStep).toBe(3);
      expect(result.current.manualFields.minPlayers).toBe(2);

      // Step 3: BGG match
      act(() => {
        result.current.setStep3Data({
          selectedBggId: 9209,
          bggDetails: {
            id: 9209,
            name: 'Ticket to Ride',
            yearPublished: 2004,
            minPlayers: 2,
            maxPlayers: 5,
            playingTime: 60,
            minAge: 8,
            rating: 7.4,
            thumbnail: 'https://example.com/ttr.jpg',
          },
        });
        result.current.setCurrentStep(4);
      });

      expect(result.current.currentStep).toBe(4);
      expect(result.current.selectedBggId).toBe(9209);

      // Verify all data persisted
      expect(result.current.pdfDocumentId).toBe('pdf-456');
      expect(result.current.qualityScore).toBe(0.78);
      expect(result.current.extractedTitle).toBe('Ticket to Ride');
      expect(result.current.manualFields.maxPlayers).toBe(5);
      expect(result.current.bggDetails?.name).toBe('Ticket to Ride');
    });
  });
});
