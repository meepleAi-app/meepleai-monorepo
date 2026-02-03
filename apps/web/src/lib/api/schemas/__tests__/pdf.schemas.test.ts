/**
 * PDF Schema Tests (Issue #3371)
 *
 * Validates that frontend Zod schemas correctly parse backend JSON responses.
 * Tests real-world JSON formats from .NET API serialization.
 */

import { describe, it, expect } from 'vitest';
import {
  ProcessingProgressSchema,
  ProcessingStepSchema,
  PdfDocumentDtoSchema,
} from '../pdf.schemas';

describe('ProcessingStepSchema', () => {
  it('should accept all valid processing steps', () => {
    const validSteps = [
      'Uploading',
      'Extracting',
      'Chunking',
      'Embedding',
      'Indexing',
      'Completed',
      'Failed',
    ];

    validSteps.forEach(step => {
      const result = ProcessingStepSchema.safeParse(step);
      expect(result.success, `Step "${step}" should be valid`).toBe(true);
    });
  });

  it('should reject invalid processing steps', () => {
    const invalidSteps = ['Pending', 'Processing', 'Unknown', 'uploading', '', null, 123];

    invalidSteps.forEach(step => {
      const result = ProcessingStepSchema.safeParse(step);
      expect(result.success, `Step "${step}" should be invalid`).toBe(false);
    });
  });
});

describe('ProcessingProgressSchema', () => {
  it('should parse valid backend JSON response', () => {
    // Real-world backend response format
    const backendResponse = {
      currentStep: 'Extracting',
      percentComplete: 35,
      elapsedTime: '00:01:23.4567890',
      estimatedTimeRemaining: '00:02:15.0000000',
      pagesProcessed: 5,
      totalPages: 15,
      startedAt: '2026-02-01T10:30:00.000Z',
      completedAt: null,
      errorMessage: null,
    };

    const result = ProcessingProgressSchema.safeParse(backendResponse);
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.currentStep).toBe('Extracting');
      expect(result.data.percentComplete).toBe(35);
      expect(result.data.pagesProcessed).toBe(5);
      expect(result.data.totalPages).toBe(15);
    }
  });

  it('should parse completed processing response', () => {
    const completedResponse = {
      currentStep: 'Completed',
      percentComplete: 100,
      elapsedTime: '00:05:30.1234567',
      estimatedTimeRemaining: null,
      pagesProcessed: 25,
      totalPages: 25,
      startedAt: '2026-02-01T10:30:00.000Z',
      completedAt: '2026-02-01T10:35:30.123Z',
      errorMessage: null,
    };

    const result = ProcessingProgressSchema.safeParse(completedResponse);
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.currentStep).toBe('Completed');
      expect(result.data.percentComplete).toBe(100);
      expect(result.data.completedAt).toBe('2026-02-01T10:35:30.123Z');
    }
  });

  it('should parse failed processing response with error message', () => {
    const failedResponse = {
      currentStep: 'Failed',
      percentComplete: 42,
      elapsedTime: '00:02:15.0000000',
      estimatedTimeRemaining: null,
      pagesProcessed: 8,
      totalPages: 20,
      startedAt: '2026-02-01T10:30:00.000Z',
      completedAt: '2026-02-01T10:32:15.000Z',
      errorMessage: 'Failed to extract text from page 9: Invalid PDF structure',
    };

    const result = ProcessingProgressSchema.safeParse(failedResponse);
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.currentStep).toBe('Failed');
      expect(result.data.errorMessage).toBe(
        'Failed to extract text from page 9: Invalid PDF structure'
      );
    }
  });

  it('should parse uploading step at start of processing', () => {
    const uploadingResponse = {
      currentStep: 'Uploading',
      percentComplete: 10,
      elapsedTime: '00:00:05.0000000',
      estimatedTimeRemaining: '00:00:45.0000000',
      pagesProcessed: 0,
      totalPages: 0,
      startedAt: '2026-02-01T10:30:00.000Z',
      completedAt: null,
      errorMessage: null,
    };

    const result = ProcessingProgressSchema.safeParse(uploadingResponse);
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.currentStep).toBe('Uploading');
      expect(result.data.pagesProcessed).toBe(0);
      expect(result.data.totalPages).toBe(0);
    }
  });

  it('should accept errorMessage being undefined (optional)', () => {
    const responseWithoutError = {
      currentStep: 'Indexing',
      percentComplete: 85,
      elapsedTime: '00:03:00.0000000',
      estimatedTimeRemaining: '00:00:30.0000000',
      pagesProcessed: 20,
      totalPages: 20,
      startedAt: '2026-02-01T10:30:00.000Z',
      completedAt: null,
      // errorMessage not present
    };

    const result = ProcessingProgressSchema.safeParse(responseWithoutError);
    expect(result.success).toBe(true);
  });

  it('should reject invalid percentComplete values', () => {
    const invalidResponses = [
      { ...validBaseResponse(), percentComplete: -1 },
      { ...validBaseResponse(), percentComplete: 101 },
      { ...validBaseResponse(), percentComplete: 50.5 }, // must be int
    ];

    invalidResponses.forEach(response => {
      const result = ProcessingProgressSchema.safeParse(response);
      expect(result.success).toBe(false);
    });
  });

  it('should reject invalid currentStep values', () => {
    const response = {
      ...validBaseResponse(),
      currentStep: 'InvalidStep',
    };

    const result = ProcessingProgressSchema.safeParse(response);
    expect(result.success).toBe(false);
  });

  it('should reject missing required fields', () => {
    const missingFields = [
      { percentComplete: 50 }, // missing currentStep
      { currentStep: 'Extracting' }, // missing percentComplete
      { currentStep: 'Extracting', percentComplete: 50 }, // missing other required fields
    ];

    missingFields.forEach(response => {
      const result = ProcessingProgressSchema.safeParse(response);
      expect(result.success).toBe(false);
    });
  });

  it('should reject invalid datetime format for startedAt', () => {
    const response = {
      ...validBaseResponse(),
      startedAt: '2026-02-01', // incomplete datetime
    };

    const result = ProcessingProgressSchema.safeParse(response);
    expect(result.success).toBe(false);
  });

  it('should parse all processing steps through the pipeline', () => {
    const steps = ['Uploading', 'Extracting', 'Chunking', 'Embedding', 'Indexing'];

    steps.forEach(step => {
      const response = {
        ...validBaseResponse(),
        currentStep: step,
      };

      const result = ProcessingProgressSchema.safeParse(response);
      expect(result.success, `Step "${step}" should parse successfully`).toBe(true);
    });
  });
});

describe('PdfDocumentDtoSchema', () => {
  it('should parse valid PDF document response', () => {
    const pdfDocument = {
      id: '550e8400-e29b-41d4-a716-446655440000',
      gameId: '6ba7b810-9dad-11d1-80b4-00c04fd430c8',
      fileName: 'rulebook.pdf',
      filePath: '/uploads/games/rulebook.pdf',
      fileSizeBytes: 1024000,
      processingStatus: 'Completed',
      uploadedAt: '2026-02-01T10:30:00.000Z',
      processedAt: '2026-02-01T10:35:00.000Z',
      pageCount: 25,
      documentType: 'base',
      isPublic: false,
    };

    const result = PdfDocumentDtoSchema.safeParse(pdfDocument);
    expect(result.success).toBe(true);
  });

  it('should handle nullable fields', () => {
    const pdfDocument = {
      id: '550e8400-e29b-41d4-a716-446655440000',
      gameId: '6ba7b810-9dad-11d1-80b4-00c04fd430c8',
      fileName: 'rulebook.pdf',
      filePath: '/uploads/games/rulebook.pdf',
      fileSizeBytes: 1024000,
      processingStatus: 'Processing',
      uploadedAt: '2026-02-01T10:30:00.000Z',
      processedAt: null,
      pageCount: null,
      documentType: 'base',
      isPublic: false,
    };

    const result = PdfDocumentDtoSchema.safeParse(pdfDocument);
    expect(result.success).toBe(true);
  });
});

// Helper function for creating valid base response
function validBaseResponse() {
  return {
    currentStep: 'Extracting',
    percentComplete: 35,
    elapsedTime: '00:01:23.4567890',
    estimatedTimeRemaining: '00:02:15.0000000',
    pagesProcessed: 5,
    totalPages: 15,
    startedAt: '2026-02-01T10:30:00.000Z',
    completedAt: null,
    errorMessage: null,
  };
}
