/**
 * useWizard Hook Tests
 * Issue #3005: Frontend Test Coverage Improvement
 *
 * Tests for wizard state management with useReducer.
 */

import { describe, it, expect } from 'vitest';
import { renderHook, act } from '@testing-library/react';

import { useWizard, type WizardState, type WizardStep, type ProcessingStatus } from '../useWizard';

describe('useWizard', () => {
  describe('Initial State', () => {
    it('should have upload as initial step', () => {
      const { result } = renderHook(() => useWizard());

      expect(result.current.state.currentStep).toBe('upload');
    });

    it('should have null documentId initially', () => {
      const { result } = renderHook(() => useWizard());

      expect(result.current.state.documentId).toBeNull();
    });

    it('should have null processingStatus initially', () => {
      const { result } = renderHook(() => useWizard());

      expect(result.current.state.processingStatus).toBeNull();
    });

    it('should have null processingError initially', () => {
      const { result } = renderHook(() => useWizard());

      expect(result.current.state.processingError).toBeNull();
    });

    it('should have null error initially', () => {
      const { result } = renderHook(() => useWizard());

      expect(result.current.state.error).toBeNull();
    });
  });

  describe('UPLOAD_SUCCESS action', () => {
    it('should set documentId', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({ type: 'UPLOAD_SUCCESS', documentId: 'doc-123' });
      });

      expect(result.current.state.documentId).toBe('doc-123');
    });

    it('should transition to parse step', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({ type: 'UPLOAD_SUCCESS', documentId: 'doc-123' });
      });

      expect(result.current.state.currentStep).toBe('parse');
    });

    it('should set processingStatus to pending', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({ type: 'UPLOAD_SUCCESS', documentId: 'doc-123' });
      });

      expect(result.current.state.processingStatus).toBe('pending');
    });

    it('should clear previous errors', () => {
      const { result } = renderHook(() => useWizard());

      // Set an error first
      act(() => {
        result.current.dispatch({ type: 'ERROR', error: 'Some error' });
      });

      expect(result.current.state.error).toBe('Some error');

      // Upload success should clear errors
      act(() => {
        result.current.dispatch({ type: 'UPLOAD_SUCCESS', documentId: 'doc-123' });
      });

      expect(result.current.state.error).toBeNull();
      expect(result.current.state.processingError).toBeNull();
    });
  });

  describe('PROCESSING_UPDATE action', () => {
    it.each([
      ['pending'],
      ['processing'],
      ['completed'],
      ['failed'],
    ] as [ProcessingStatus][])('should update processingStatus to %s', (status) => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({ type: 'PROCESSING_UPDATE', status });
      });

      expect(result.current.state.processingStatus).toBe(status);
    });
  });

  describe('PROCESSING_ERROR action', () => {
    it('should set processingError', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({ type: 'PROCESSING_ERROR', error: 'Processing failed' });
      });

      expect(result.current.state.processingError).toBe('Processing failed');
    });

    it('should set processingStatus to failed', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({ type: 'PROCESSING_ERROR', error: 'Processing failed' });
      });

      expect(result.current.state.processingStatus).toBe('failed');
    });
  });

  describe('PARSING_COMPLETE action', () => {
    it('should transition to review step', () => {
      const { result } = renderHook(() => useWizard());

      // First upload a document
      act(() => {
        result.current.dispatch({ type: 'UPLOAD_SUCCESS', documentId: 'doc-123' });
      });

      // Then complete parsing
      act(() => {
        result.current.dispatch({ type: 'PARSING_COMPLETE' });
      });

      expect(result.current.state.currentStep).toBe('review');
    });

    it('should set processingStatus to completed', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({ type: 'PARSING_COMPLETE' });
      });

      expect(result.current.state.processingStatus).toBe('completed');
    });

    it('should clear processingError', () => {
      const { result } = renderHook(() => useWizard());

      // Set a processing error first
      act(() => {
        result.current.dispatch({ type: 'PROCESSING_ERROR', error: 'Some error' });
      });

      // Then complete parsing
      act(() => {
        result.current.dispatch({ type: 'PARSING_COMPLETE' });
      });

      expect(result.current.state.processingError).toBeNull();
    });
  });

  describe('PUBLISH_COMPLETE action', () => {
    it('should transition to publish step', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({ type: 'PUBLISH_COMPLETE' });
      });

      expect(result.current.state.currentStep).toBe('publish');
    });
  });

  describe('NEXT_STEP action', () => {
    it.each([
      ['upload', 'parse'],
      ['parse', 'review'],
      ['review', 'publish'],
    ] as [WizardStep, WizardStep][])('should move from %s to %s', (from, to) => {
      const { result } = renderHook(() => useWizard());

      // Set to starting step
      act(() => {
        result.current.dispatch({ type: 'SET_STEP', step: from });
      });

      // Go to next step
      act(() => {
        result.current.dispatch({ type: 'NEXT_STEP' });
      });

      expect(result.current.state.currentStep).toBe(to);
    });

    it('should not go past publish step', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({ type: 'SET_STEP', step: 'publish' });
      });

      act(() => {
        result.current.dispatch({ type: 'NEXT_STEP' });
      });

      expect(result.current.state.currentStep).toBe('publish');
    });
  });

  describe('PREV_STEP action', () => {
    it.each([
      ['publish', 'review'],
      ['review', 'parse'],
      ['parse', 'upload'],
    ] as [WizardStep, WizardStep][])('should move from %s to %s', (from, to) => {
      const { result } = renderHook(() => useWizard());

      // Set to starting step
      act(() => {
        result.current.dispatch({ type: 'SET_STEP', step: from });
      });

      // Go to previous step
      act(() => {
        result.current.dispatch({ type: 'PREV_STEP' });
      });

      expect(result.current.state.currentStep).toBe(to);
    });

    it('should not go before upload step', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({ type: 'SET_STEP', step: 'upload' });
      });

      act(() => {
        result.current.dispatch({ type: 'PREV_STEP' });
      });

      expect(result.current.state.currentStep).toBe('upload');
    });
  });

  describe('SET_STEP action', () => {
    it.each([
      ['upload'],
      ['parse'],
      ['review'],
      ['publish'],
    ] as [WizardStep][])('should set step to %s', (step) => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({ type: 'SET_STEP', step });
      });

      expect(result.current.state.currentStep).toBe(step);
    });
  });

  describe('RESET action', () => {
    it('should reset to initial state', () => {
      const { result } = renderHook(() => useWizard());

      // Make some changes
      act(() => {
        result.current.dispatch({ type: 'UPLOAD_SUCCESS', documentId: 'doc-123' });
        result.current.dispatch({ type: 'PROCESSING_UPDATE', status: 'processing' });
        result.current.dispatch({ type: 'ERROR', error: 'Some error' });
      });

      // Reset
      act(() => {
        result.current.dispatch({ type: 'RESET' });
      });

      const expectedInitialState: WizardState = {
        currentStep: 'upload',
        documentId: null,
        processingStatus: null,
        processingError: null,
        error: null,
      };

      expect(result.current.state).toEqual(expectedInitialState);
    });
  });

  describe('ERROR action', () => {
    it('should set error', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({ type: 'ERROR', error: 'Something went wrong' });
      });

      expect(result.current.state.error).toBe('Something went wrong');
    });

    it('should preserve other state when setting error', () => {
      const { result } = renderHook(() => useWizard());

      // Make some changes first
      act(() => {
        result.current.dispatch({ type: 'UPLOAD_SUCCESS', documentId: 'doc-123' });
      });

      // Set error
      act(() => {
        result.current.dispatch({ type: 'ERROR', error: 'Error occurred' });
      });

      // Other state should be preserved
      expect(result.current.state.documentId).toBe('doc-123');
      expect(result.current.state.currentStep).toBe('parse');
      expect(result.current.state.error).toBe('Error occurred');
    });
  });

  describe('Unknown action', () => {
    it('should return current state for unknown action', () => {
      const { result } = renderHook(() => useWizard());

      const stateBefore = result.current.state;

      act(() => {
        // @ts-expect-error Testing unknown action type
        result.current.dispatch({ type: 'UNKNOWN_ACTION' });
      });

      expect(result.current.state).toEqual(stateBefore);
    });
  });

  describe('Full workflow', () => {
    it('should handle complete upload to publish flow', () => {
      const { result } = renderHook(() => useWizard());

      // Start at upload
      expect(result.current.state.currentStep).toBe('upload');

      // Upload success
      act(() => {
        result.current.dispatch({ type: 'UPLOAD_SUCCESS', documentId: 'doc-123' });
      });
      expect(result.current.state.currentStep).toBe('parse');
      expect(result.current.state.documentId).toBe('doc-123');
      expect(result.current.state.processingStatus).toBe('pending');

      // Processing starts
      act(() => {
        result.current.dispatch({ type: 'PROCESSING_UPDATE', status: 'processing' });
      });
      expect(result.current.state.processingStatus).toBe('processing');

      // Parsing complete
      act(() => {
        result.current.dispatch({ type: 'PARSING_COMPLETE' });
      });
      expect(result.current.state.currentStep).toBe('review');
      expect(result.current.state.processingStatus).toBe('completed');

      // Publish
      act(() => {
        result.current.dispatch({ type: 'PUBLISH_COMPLETE' });
      });
      expect(result.current.state.currentStep).toBe('publish');
    });

    it('should handle error recovery flow', () => {
      const { result } = renderHook(() => useWizard());

      // Upload and start processing
      act(() => {
        result.current.dispatch({ type: 'UPLOAD_SUCCESS', documentId: 'doc-123' });
        result.current.dispatch({ type: 'PROCESSING_UPDATE', status: 'processing' });
      });

      // Processing error
      act(() => {
        result.current.dispatch({ type: 'PROCESSING_ERROR', error: 'PDF processing failed' });
      });
      expect(result.current.state.processingStatus).toBe('failed');
      expect(result.current.state.processingError).toBe('PDF processing failed');

      // Reset and retry
      act(() => {
        result.current.dispatch({ type: 'RESET' });
      });
      expect(result.current.state.currentStep).toBe('upload');
      expect(result.current.state.processingError).toBeNull();
    });
  });
});
