import { describe, expect, it } from 'vitest';
import { type UiStep, LABELS, deriveUiStep, isAbortableStep } from '../TranslateViewer.steps';

describe('TranslateViewer.steps', () => {
  describe('deriveUiStep', () => {
    it('returns "uploading" when phase is uploading', () => {
      const uiStep = deriveUiStep('uploading', { appliedTerms: [], isComplete: false });
      expect(uiStep).toBe('uploading');
    });

    it('returns "ocr" when phase is segmenting', () => {
      const uiStep = deriveUiStep('segmenting', { appliedTerms: [], isComplete: false });
      expect(uiStep).toBe('ocr');
    });

    it('returns "translating" when phase is translating with empty appliedTerms', () => {
      const uiStep = deriveUiStep('translating', { appliedTerms: [], isComplete: false });
      expect(uiStep).toBe('translating');
    });

    it('returns "glossary-check" when phase is translating with appliedTerms present', () => {
      const uiStep = deriveUiStep('translating', {
        appliedTerms: ['sentinel', 'gold'],
        isComplete: false,
      });
      expect(uiStep).toBe('glossary-check');
    });

    it('returns "glossary-check" when appliedTerms has even one entry', () => {
      const uiStep = deriveUiStep('translating', {
        appliedTerms: ['single-term'],
        isComplete: false,
      });
      expect(uiStep).toBe('glossary-check');
    });

    it('returns null when phase is idle', () => {
      const uiStep = deriveUiStep('idle', { appliedTerms: [], isComplete: false });
      expect(uiStep).toBeNull();
    });

    it('returns null when phase is segments_ready', () => {
      const uiStep = deriveUiStep('segments_ready', { appliedTerms: [], isComplete: false });
      expect(uiStep).toBeNull();
    });

    it('returns null when phase is translated', () => {
      const uiStep = deriveUiStep('translated', { appliedTerms: [], isComplete: false });
      expect(uiStep).toBeNull();
    });

    it('handles appliedTerms and isComplete state independently', () => {
      // isComplete=true should not affect glossary-check trigger
      const uiStep = deriveUiStep('translating', {
        appliedTerms: ['word1'],
        isComplete: true,
      });
      expect(uiStep).toBe('glossary-check');
    });
  });

  describe('isAbortableStep', () => {
    it('returns true for "ocr" step', () => {
      expect(isAbortableStep('ocr')).toBe(true);
    });

    it('returns true for "translating" step', () => {
      expect(isAbortableStep('translating')).toBe(true);
    });

    it('returns true for "glossary-check" step', () => {
      expect(isAbortableStep('glossary-check')).toBe(true);
    });

    it('returns false for "uploading" step (DEC-3: abort hidden in step 1)', () => {
      expect(isAbortableStep('uploading')).toBe(false);
    });

    it('returns false when uiStep is null', () => {
      expect(isAbortableStep(null)).toBe(false);
    });
  });

  describe('LABELS', () => {
    it('has entry for all UiStep values', () => {
      const uiSteps: UiStep[] = ['uploading', 'ocr', 'translating', 'glossary-check'];
      uiSteps.forEach(step => {
        expect(LABELS[step]).toBeDefined();
        expect(typeof LABELS[step]).toBe('string');
        expect(LABELS[step].length).toBeGreaterThan(0);
      });
    });

    it('has abort control labels', () => {
      expect(LABELS.abort).toBe('Annulla');
      expect(LABELS.abortAriaLabel).toBe('Annulla la traduzione in corso');
    });

    it('has timeoutError label', () => {
      expect(LABELS.timeoutError).toContain('20 secondi');
    });

    it('all labels are non-empty strings', () => {
      Object.values(LABELS).forEach(label => {
        expect(typeof label).toBe('string');
        expect(label.length).toBeGreaterThan(0);
      });
    });
  });
});
