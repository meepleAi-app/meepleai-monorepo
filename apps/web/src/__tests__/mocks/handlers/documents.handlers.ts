/**
 * MSW handlers for document/PDF endpoints
 *
 * Covers: /api/v1/documents/*, /api/v1/ingest/* routes
 * - PDF upload, processing
 * - Document management
 * - Processing status tracking
 */

import { http, HttpResponse } from 'msw';
import {
  createMockPdfDocument,
  createMockPdfList,
} from '../../fixtures/common-fixtures';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// In-memory document store
let documents = createMockPdfList(3);

export const documentsHandlers = [
  // GET /api/v1/documents - List documents
  http.get(`${API_BASE}/api/v1/documents`, () => {
    return HttpResponse.json(documents, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // GET /api/v1/documents/:id - Get document details
  http.get(`${API_BASE}/api/v1/documents/:id`, ({ params }) => {
    const { id } = params;
    const document = documents.find((d) => d.id === id);

    if (!document) {
      return HttpResponse.json(
        { error: 'Document not found' },
        { status: 404 }
      );
    }

    return HttpResponse.json(document, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // POST /api/v1/ingest/pdf - Upload PDF (multipart/form-data)
  http.post(`${API_BASE}/api/v1/ingest/pdf`, async ({ request }) => {
    const formData = await request.formData();
    const file = formData.get('file') as File;

    if (!file) {
      return HttpResponse.json(
        { error: 'No file provided' },
        { status: 400 }
      );
    }

    // Validate file type
    if (!file.name.endsWith('.pdf')) {
      return HttpResponse.json(
        { error: 'Invalid file type. Only PDF files are allowed.' },
        { status: 400 }
      );
    }

    // Validate file size (max 10MB)
    const maxSize = 10 * 1024 * 1024;
    if (file.size > maxSize) {
      return HttpResponse.json(
        { error: 'File too large. Maximum size is 10MB.' },
        { status: 413 }
      );
    }

    // Create mock document
    const newDocument = createMockPdfDocument({
      id: `pdf-${Date.now()}`,
      fileName: file.name,
      fileSizeBytes: file.size,
      processingStatus: 'pending',
    });

    documents.push(newDocument);

    return HttpResponse.json({
      documentId: newDocument.id,
      fileName: newDocument.fileName,
      status: 'pending',
      message: 'PDF uploaded successfully. Processing started.',
    }, {
      status: 201,
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // POST /api/v1/documents/upload - Alternative upload endpoint
  http.post(`${API_BASE}/api/v1/documents/upload`, async ({ request }) => {
    const formData = await request.formData();
    const file = formData.get('pdf') as File;

    if (!file) {
      return HttpResponse.json(
        { error: 'No file provided' },
        { status: 400 }
      );
    }

    const newDocument = createMockPdfDocument({
      id: `pdf-${Date.now()}`,
      fileName: file.name,
      fileSizeBytes: file.size,
      processingStatus: 'processing',
    });

    documents.push(newDocument);

    return HttpResponse.json({
      id: newDocument.id,
      fileName: newDocument.fileName,
      status: 'processing',
    }, {
      status: 201,
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // GET /api/v1/documents/:id/status - Get processing status
  http.get(`${API_BASE}/api/v1/documents/:id/status`, ({ params }) => {
    const { id } = params;
    const document = documents.find((d) => d.id === id);

    if (!document) {
      return HttpResponse.json(
        { error: 'Document not found' },
        { status: 404 }
      );
    }

    return HttpResponse.json({
      id: document.id,
      status: document.processingStatus || 'completed',
      progress: document.processingStatus === 'processing' ? 50 : 100,
    }, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // DELETE /api/v1/documents/:id - Delete document
  http.delete(`${API_BASE}/api/v1/documents/:id`, ({ params }) => {
    const { id } = params;
    const documentIndex = documents.findIndex((d) => d.id === id);

    if (documentIndex === -1) {
      return HttpResponse.json(
        { error: 'Document not found' },
        { status: 404 }
      );
    }

    documents.splice(documentIndex, 1);

    return HttpResponse.json(
      { success: true },
      {
        headers: {
          'X-Correlation-Id': `test-correlation-${Date.now()}`,
        },
      }
    );
  }),

  // Error simulation handlers
  http.post(`${API_BASE}/api/v1/ingest/pdf-error-500`, () => {
    return HttpResponse.json(
      { error: 'Internal server error during PDF processing' },
      { status: 500 }
    );
  }),
];

// Helper to reset documents state between tests
export const resetDocumentsState = () => {
  documents = createMockPdfList(3);
};
