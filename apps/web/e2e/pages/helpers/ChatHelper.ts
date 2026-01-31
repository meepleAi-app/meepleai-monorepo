/**
 * ChatHelper - Centralized chat/RAG utilities
 *
 * Handles all chat-related mocking operations including:
 * - Chat message sending and receiving
 * - SSE streaming responses
 * - Citations and sources
 * - Message history and threading
 * - Export functionality
 *
 * Replaces legacy mockChat* functions scattered across chat test files.
 */

import { Page } from '@playwright/test';

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
  citations?: Array<{ title: string; page: number; snippet: string }>;
}

export class ChatHelper {
  constructor(private readonly page: Page) {}

  /**
   * Mock chat POST endpoint (non-streaming)
   */
  async mockChatResponse(response: {
    answer: string;
    sources?: Array<{ title: string; page: number; snippet: string }>;
    confidence?: number;
  }): Promise<void> {
    await this.page.route(`${apiBase}/api/v1/chat-threads/*/messages`, async route => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            answer: response.answer,
            sources: response.sources || [],
            confidence: response.confidence || 0.9,
          }),
        });
      }
    });
  }

  /**
   * Mock SSE streaming chat endpoint
   */
  async mockChatStreaming(chunks: string[], sources?: Array<any>): Promise<void> {
    await this.page.route(`${apiBase}/api/v1/chat-threads/*/messages/stream`, async route => {
      // Build SSE response with data chunks
      let sseBody = '';

      // Send text chunks
      for (const chunk of chunks) {
        sseBody += `data: ${JSON.stringify({ type: 'chunk', content: chunk })}\n\n`;
      }

      // Send sources if provided
      if (sources) {
        sseBody += `data: ${JSON.stringify({ type: 'sources', sources })}\n\n`;
      }

      // Send done event
      sseBody += `data: ${JSON.stringify({ type: 'done' })}\n\n`;

      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
          'Cache-Control': 'no-cache',
          Connection: 'keep-alive',
        },
        body: sseBody,
      });
    });
  }

  /**
   * Mock QA agent SSE streaming with citations (Issue #859 pattern)
   * Builds SSE response with token chunks + citations event
   */
  async mockQAStreamWithCitations(
    tokens: string[],
    citations: Array<{
      documentId: string;
      pageNumber: number;
      snippet: string;
      relevanceScore: number;
    }>,
    confidence: number = 0.9
  ): Promise<void> {
    let sseBody = '';

    // Send token chunks
    for (const token of tokens) {
      sseBody += `event: token\ndata: ${JSON.stringify({ token })}\n\n`;
    }

    // Send citations event
    if (citations.length > 0) {
      sseBody += `event: citations\ndata: ${JSON.stringify({ citations })}\n\n`;
    }

    // Send complete event
    sseBody += `event: complete\ndata: ${JSON.stringify({ totalTokens: tokens.length * 5, confidence })}\n\n`;

    await this.page.route('**/api/v1/agents/qa/stream', async route => {
      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
          'Cache-Control': 'no-cache',
          Connection: 'keep-alive',
        },
        body: sseBody,
      });
    });
  }

  /**
   * Mock QA agent endpoint
   */
  async mockQAAgent(answer: string, sources?: Array<any>): Promise<void> {
    await this.page.route(`${apiBase}/api/v1/agents/qa`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          answer,
          sources: sources || [{ title: 'Game Manual', snippet: 'Sample text', page: 1 }],
        }),
      });
    });
  }

  /**
   * Mock chat history endpoint
   */
  async mockChatHistory(messages: ChatMessage[]): Promise<void> {
    await this.page.route(`${apiBase}/api/v1/chat-threads/*/history`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ messages }),
      });
    });
  }

  /**
   * Mock chat thread operations (create, delete, update)
   */
  async mockChatThreads(threads?: Array<any>): Promise<void> {
    const defaultThreads = threads || [
      {
        id: 'thread-1',
        title: 'Game Rules Discussion',
        messageCount: 5,
        createdAt: new Date().toISOString(),
      },
    ];

    await this.page.route(`${apiBase}/api/v1/chat-threads*`, async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ threads: defaultThreads }),
        });
      } else if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ id: 'thread-new', ...route.request().postDataJSON() }),
        });
      } else if (route.request().method() === 'DELETE') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ success: true }),
        });
      }
    });
  }

  /**
   * Mock chat export endpoint
   */
  async mockChatExport(format: 'txt' | 'json' | 'md' = 'txt'): Promise<void> {
    const exportData = format === 'json' ? JSON.stringify({ messages: [] }) : 'Chat export data';

    await this.page.route(`${apiBase}/api/v1/chat-threads/*/export`, async route => {
      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': format === 'json' ? 'application/json' : 'text/plain',
          'Content-Disposition': `attachment; filename="chat-export.${format}"`,
        },
        body: exportData,
      });
    });
  }

  /**
   * Mock feedback endpoint
   */
  async mockFeedback(success: boolean = true): Promise<void> {
    await this.page.route(`${apiBase}/api/v1/chat-threads/*/feedback`, async route => {
      if (success) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ success: true }),
        });
      } else {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Feedback failed' }),
        });
      }
    });
  }

  /**
   * Mock citations endpoint
   */
  async mockCitations(citations: Array<any>): Promise<void> {
    await this.page.route(`${apiBase}/api/v1/chat-threads/*/citations`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ citations }),
      });
    });
  }

  /**
   * Mock message edit endpoint
   * @param success - Whether edit should succeed (default: true)
   * @param statusCode - HTTP status code for failures (default: 403)
   */
  async mockMessageEdit(success: boolean = true, statusCode: number = 403): Promise<void> {
    await this.page.route('**/api/v1/chat-threads/*/messages/*', async route => {
      const method = route.request().method();
      if (method === 'PUT') {
        if (success) {
          const requestData = route.request().postDataJSON();
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              messageId: 'test-message-id',
              content: requestData?.content || 'Edited message',
              isEdited: true,
              editedAt: new Date().toISOString(),
            }),
          });
        } else {
          await route.fulfill({
            status: statusCode,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'Edit forbidden' }),
          });
        }
      } else {
        await route.continue();
      }
    });
  }

  /**
   * Mock message delete endpoint
   * @param success - Whether delete should succeed (default: true)
   * @param statusCode - HTTP status code for failures (default: 403)
   */
  async mockMessageDelete(success: boolean = true, statusCode: number = 403): Promise<void> {
    await this.page.route('**/api/v1/chat-threads/*/messages/*', async route => {
      const method = route.request().method();
      if (method === 'DELETE') {
        if (success) {
          await route.fulfill({
            status: 204, // No Content for successful delete
          });
        } else {
          await route.fulfill({
            status: statusCode,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'Delete forbidden' }),
          });
        }
      } else {
        await route.continue();
      }
    });
  }

  /**
   * Mock both edit and delete operations (convenience method)
   * @param editSuccess - Whether edit should succeed
   * @param deleteSuccess - Whether delete should succeed
   */
  async mockMessageEditDelete(
    editSuccess: boolean = true,
    deleteSuccess: boolean = true
  ): Promise<void> {
    await this.page.route('**/api/v1/chat-threads/*/messages/*', async route => {
      const method = route.request().method();

      if (method === 'PUT') {
        if (editSuccess) {
          const requestData = route.request().postDataJSON();
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              messageId: 'test-message-id',
              content: requestData?.content || 'Edited message',
              isEdited: true,
              editedAt: new Date().toISOString(),
            }),
          });
        } else {
          await route.fulfill({
            status: 403,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'Edit forbidden' }),
          });
        }
      } else if (method === 'DELETE') {
        if (deleteSuccess) {
          await route.fulfill({ status: 204 });
        } else {
          await route.fulfill({
            status: 403,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'Delete forbidden' }),
          });
        }
      } else {
        await route.continue();
      }
    });
  }
}
