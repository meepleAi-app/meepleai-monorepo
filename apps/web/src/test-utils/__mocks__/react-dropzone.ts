/**
 * Mock for react-dropzone (not installed as dependency)
 * Used by Step1PdfUpload component
 */

import { vi } from 'vitest';

export const useDropzone = vi.fn((options?: {
  onDrop?: (files: File[]) => void;
  accept?: Record<string, string[]>;
  maxFiles?: number;
  disabled?: boolean;
}) => ({
  getRootProps: () => ({
    role: 'button' as const,
    'aria-label': 'Upload PDF file',
    onClick: vi.fn(),
    onDragOver: vi.fn(),
    onDragEnter: vi.fn(),
    onDragLeave: vi.fn(),
    onDrop: vi.fn(),
    tabIndex: 0,
  }),
  getInputProps: () => ({
    type: 'file' as const,
    accept: 'application/pdf',
    multiple: false,
    disabled: options?.disabled,
    style: { display: 'none' },
  }),
  isDragActive: false,
  acceptedFiles: [],
  fileRejections: [],
  open: vi.fn(),
}));
