import { renderHook, act } from '@testing-library/react';
import { useWizard, type WizardAction } from '@/hooks/wizard/useWizard';

describe('useWizard', () => {
  describe('Initial State', () => {
    it('initializes with upload step', () => {
      const { result } = renderHook(() => useWizard());

      expect(result.current.state.currentStep).toBe('upload');
      expect(result.current.state.documentId).toBeNull();
      expect(result.current.state.processingStatus).toBeNull();
      expect(result.current.state.processingError).toBeNull();
      expect(result.current.state.error).toBeNull();
    });
  });

  describe('UPLOAD_SUCCESS Action', () => {
    it('sets documentId and advances to parse step', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({
          type: 'UPLOAD_SUCCESS',
          documentId: 'doc-123'
        });
      });

      expect(result.current.state.documentId).toBe('doc-123');
      expect(result.current.state.currentStep).toBe('parse');
      expect(result.current.state.processingStatus).toBe('pending');
      expect(result.current.state.error).toBeNull();
    });

    it('clears previous errors on upload success', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({ type: 'ERROR', error: 'Previous error' });
      });

      expect(result.current.state.error).toBe('Previous error');

      act(() => {
        result.current.dispatch({
          type: 'UPLOAD_SUCCESS',
          documentId: 'doc-123'
        });
      });

      expect(result.current.state.error).toBeNull();
    });
  });

  describe('PROCESSING_UPDATE Action', () => {
    it('updates processing status', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({
          type: 'PROCESSING_UPDATE',
          status: 'processing'
        });
      });

      expect(result.current.state.processingStatus).toBe('processing');
    });

    it('can update through all processing statuses', () => {
      const { result } = renderHook(() => useWizard());

      const statuses: Array<'pending' | 'processing' | 'completed' | 'failed'> = [
        'pending',
        'processing',
        'completed'
      ];

      statuses.forEach((status) => {
        act(() => {
          result.current.dispatch({
            type: 'PROCESSING_UPDATE',
            status
          });
        });

        expect(result.current.state.processingStatus).toBe(status);
      });
    });
  });

  describe('PROCESSING_ERROR Action', () => {
    it('sets processing error and status to failed', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({
          type: 'PROCESSING_ERROR',
          error: 'Processing failed'
        });
      });

      expect(result.current.state.processingError).toBe('Processing failed');
      expect(result.current.state.processingStatus).toBe('failed');
    });
  });

  describe('PARSING_COMPLETE Action', () => {
    it('advances to review step and clears errors', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({ type: 'PARSING_COMPLETE' });
      });

      expect(result.current.state.currentStep).toBe('review');
      expect(result.current.state.processingStatus).toBe('completed');
      expect(result.current.state.processingError).toBeNull();
    });
  });

  describe('PUBLISH_COMPLETE Action', () => {
    it('advances to publish step', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({ type: 'PUBLISH_COMPLETE' });
      });

      expect(result.current.state.currentStep).toBe('publish');
    });
  });

  describe('Step Navigation', () => {
    it('advances to next step with NEXT_STEP action', () => {
      const { result } = renderHook(() => useWizard());

      expect(result.current.state.currentStep).toBe('upload');

      act(() => {
        result.current.dispatch({ type: 'NEXT_STEP' });
      });

      expect(result.current.state.currentStep).toBe('parse');

      act(() => {
        result.current.dispatch({ type: 'NEXT_STEP' });
      });

      expect(result.current.state.currentStep).toBe('review');
    });

    it('does not advance beyond last step', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({ type: 'SET_STEP', step: 'publish' });
      });

      expect(result.current.state.currentStep).toBe('publish');

      act(() => {
        result.current.dispatch({ type: 'NEXT_STEP' });
      });

      expect(result.current.state.currentStep).toBe('publish');
    });

    it('goes to previous step with PREV_STEP action', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({ type: 'SET_STEP', step: 'review' });
      });

      expect(result.current.state.currentStep).toBe('review');

      act(() => {
        result.current.dispatch({ type: 'PREV_STEP' });
      });

      expect(result.current.state.currentStep).toBe('parse');
    });

    it('does not go before first step', () => {
      const { result } = renderHook(() => useWizard());

      expect(result.current.state.currentStep).toBe('upload');

      act(() => {
        result.current.dispatch({ type: 'PREV_STEP' });
      });

      expect(result.current.state.currentStep).toBe('upload');
    });

    it('sets specific step with SET_STEP action', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({ type: 'SET_STEP', step: 'review' });
      });

      expect(result.current.state.currentStep).toBe('review');
    });
  });

  describe('Error Handling', () => {
    it('sets error message with ERROR action', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({
          type: 'ERROR',
          error: 'Something went wrong'
        });
      });

      expect(result.current.state.error).toBe('Something went wrong');
    });

    it('can clear errors', () => {
      const { result } = renderHook(() => useWizard());

      act(() => {
        result.current.dispatch({ type: 'ERROR', error: 'Error' });
      });

      expect(result.current.state.error).toBe('Error');

      act(() => {
        result.current.dispatch({ type: 'ERROR', error: null as any });
      });

      expect(result.current.state.error).toBeNull();
    });
  });

  describe('RESET Action', () => {
    it('resets to initial state', () => {
      const { result } = renderHook(() => useWizard());

      // Make some changes
      act(() => {
        result.current.dispatch({ type: 'UPLOAD_SUCCESS', documentId: 'doc-123' });
        result.current.dispatch({ type: 'PROCESSING_UPDATE', status: 'completed' });
        result.current.dispatch({ type: 'ERROR', error: 'Test error' });
      });

      expect(result.current.state.currentStep).toBe('parse');
      expect(result.current.state.documentId).toBe('doc-123');

      // Reset
      act(() => {
        result.current.dispatch({ type: 'RESET' });
      });

      expect(result.current.state.currentStep).toBe('upload');
      expect(result.current.state.documentId).toBeNull();
      expect(result.current.state.processingStatus).toBeNull();
      expect(result.current.state.processingError).toBeNull();
      expect(result.current.state.error).toBeNull();
    });
  });

  describe('Complex Workflows', () => {
    it('handles complete wizard flow', () => {
      const { result } = renderHook(() => useWizard());

      // Start: upload step
      expect(result.current.state.currentStep).toBe('upload');

      // Upload success → parse step
      act(() => {
        result.current.dispatch({ type: 'UPLOAD_SUCCESS', documentId: 'doc-123' });
      });
      expect(result.current.state.currentStep).toBe('parse');
      expect(result.current.state.processingStatus).toBe('pending');

      // Processing updates
      act(() => {
        result.current.dispatch({ type: 'PROCESSING_UPDATE', status: 'processing' });
      });
      expect(result.current.state.processingStatus).toBe('processing');

      // Parsing complete → review step
      act(() => {
        result.current.dispatch({ type: 'PARSING_COMPLETE' });
      });
      expect(result.current.state.currentStep).toBe('review');
      expect(result.current.state.processingStatus).toBe('completed');

      // Publish complete → publish step
      act(() => {
        result.current.dispatch({ type: 'PUBLISH_COMPLETE' });
      });
      expect(result.current.state.currentStep).toBe('publish');
    });

    it('handles error recovery flow', () => {
      const { result } = renderHook(() => useWizard());

      // Upload success
      act(() => {
        result.current.dispatch({ type: 'UPLOAD_SUCCESS', documentId: 'doc-123' });
      });

      // Processing error
      act(() => {
        result.current.dispatch({ type: 'PROCESSING_ERROR', error: 'Parse failed' });
      });

      expect(result.current.state.processingStatus).toBe('failed');
      expect(result.current.state.processingError).toBe('Parse failed');

      // Reset and try again
      act(() => {
        result.current.dispatch({ type: 'RESET' });
      });

      expect(result.current.state.currentStep).toBe('upload');
      expect(result.current.state.processingError).toBeNull();
    });
  });
});
