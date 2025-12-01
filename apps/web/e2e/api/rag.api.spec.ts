/**
 * RAG (Retrieval-Augmented Generation) API Tests - Playwright Native
 *
 * Native Playwright API tests for Knowledge Base RAG endpoints.
 *
 * @see apps/api/src/Api/BoundedContexts/KnowledgeBase
 */

import { test, expect, APIRequestContext } from './fixtures/chromatic';

const BASE_URL = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('RAG API (KnowledgeBase)', () => {
  let apiContext: APIRequestContext;
  let sessionCookie: string;
  let testGameId: string;

  test.beforeAll(async ({ playwright }) => {
    apiContext = await playwright.request.newContext({
      baseURL: BASE_URL,
      extraHTTPHeaders: {
        'Content-Type': 'application/json',
      },
    });

    // Login to get session
    const loginResponse = await apiContext.post('/api/v1/auth/login', {
      data: {
        email: 'demo@meepleai.dev',
        password: 'Demo123!',
      },
    });

    sessionCookie = loginResponse.headers()['set-cookie']?.split(';')[0] || '';

    // Get a game ID for testing
    const gamesResponse = await apiContext.get('/api/v1/games', {
      headers: {
        Cookie: sessionCookie,
      },
    });

    const games = await gamesResponse.json();
    testGameId = games[0]?.id;
  });

  test.afterAll(async () => {
    await apiContext.dispose();
  });

  test.describe('POST /api/v1/agents/qa', () => {
    test('should answer question with RAG pipeline', async () => {
      // Skip if no game ID available
      if (!testGameId) {
        test.skip();
        return;
      }

      const response = await apiContext.post('/api/v1/agents/qa', {
        headers: {
          Cookie: sessionCookie,
        },
        data: {
          gameId: testGameId,
          query: 'How do you win the game?',
          searchMode: 'Hybrid',
        },
      });

      expect(response.status()).toBe(200);

      const data = await response.json();
      expect(data.answer).toBeDefined();
      expect(typeof data.answer).toBe('string');
      expect(data.answer.length).toBeGreaterThan(10);
      expect(data.snippets).toBeDefined();
      expect(Array.isArray(data.snippets)).toBeTruthy();
      expect(data.confidence).toBeGreaterThan(0);
      expect(data.confidence).toBeLessThanOrEqual(1);
    });

    test('should support vector-only search mode', async () => {
      if (!testGameId) {
        test.skip();
        return;
      }

      const response = await apiContext.post('/api/v1/agents/qa', {
        headers: {
          Cookie: sessionCookie,
        },
        data: {
          gameId: testGameId,
          query: 'What are the rules?',
          searchMode: 'Vector',
        },
      });

      expect(response.status()).toBe(200);

      const data = await response.json();
      expect(data.answer).toBeDefined();
    });

    test('should support hybrid search mode', async () => {
      if (!testGameId) {
        test.skip();
        return;
      }

      const response = await apiContext.post('/api/v1/agents/qa', {
        headers: {
          Cookie: sessionCookie,
        },
        data: {
          gameId: testGameId,
          query: 'setup instructions',
          searchMode: 'Hybrid',
        },
      });

      expect(response.status()).toBe(200);

      const data = await response.json();
      expect(data.answer).toBeDefined();
    });

    test('should generate follow-up questions when requested', async () => {
      if (!testGameId) {
        test.skip();
        return;
      }

      const response = await apiContext.post('/api/v1/agents/qa?generateFollowUps=true', {
        headers: {
          Cookie: sessionCookie,
        },
        data: {
          gameId: testGameId,
          query: 'How to play?',
          searchMode: 'Hybrid',
        },
      });

      expect(response.status()).toBe(200);

      const data = await response.json();
      expect(data.followUpQuestions).toBeDefined();
      expect(Array.isArray(data.followUpQuestions)).toBeTruthy();
    });

    test('should fail with invalid gameId', async () => {
      const response = await apiContext.post('/api/v1/agents/qa', {
        headers: {
          Cookie: sessionCookie,
        },
        data: {
          gameId: 'invalid-uuid',
          query: 'How to play?',
          searchMode: 'Hybrid',
        },
      });

      expect(response.status()).toBe(400);
    });

    test('should fail with empty query', async () => {
      if (!testGameId) {
        test.skip();
        return;
      }

      const response = await apiContext.post('/api/v1/agents/qa', {
        headers: {
          Cookie: sessionCookie,
        },
        data: {
          gameId: testGameId,
          query: '',
          searchMode: 'Hybrid',
        },
      });

      expect(response.status()).toBe(400);
    });

    test('should fail without authentication', async () => {
      const response = await apiContext.post('/api/v1/agents/qa', {
        data: {
          gameId: testGameId || '00000000-0000-0000-0000-000000000000',
          query: 'test',
          searchMode: 'Hybrid',
        },
      });

      expect(response.status()).toBe(401);
    });
  });

  test.describe('GET /api/v1/search (Hybrid Search)', () => {
    test('should search with hybrid mode', async () => {
      if (!testGameId) {
        test.skip();
        return;
      }

      const response = await apiContext.get(
        `/api/v1/search?gameId=${testGameId}&query=rules&searchMode=Hybrid&topK=5&minScore=0.5`,
        {
          headers: {
            Cookie: sessionCookie,
          },
        }
      );

      expect(response.status()).toBe(200);

      const data = await response.json();
      expect(data.success).toBe(true);
      expect(data.results).toBeDefined();
      expect(Array.isArray(data.results)).toBeTruthy();
      expect(data.count).toBeGreaterThanOrEqual(0);
      expect(data.searchMode).toBe('Hybrid');
    });

    test('should validate result schema', async () => {
      if (!testGameId) {
        test.skip();
        return;
      }

      const response = await apiContext.get(
        `/api/v1/search?gameId=${testGameId}&query=setup&searchMode=Hybrid`,
        {
          headers: {
            Cookie: sessionCookie,
          },
        }
      );

      const data = await response.json();

      if (data.results.length > 0) {
        const result = data.results[0];
        expect(result.vectorDocumentId).toBeDefined();
        expect(result.textContent).toBeDefined();
        expect(typeof result.textContent).toBe('string');
        expect(result.pageNumber).toBeGreaterThan(0);
        expect(result.relevanceScore).toBeGreaterThan(0);
        expect(result.relevanceScore).toBeLessThanOrEqual(1);
        expect(result.rank).toBeGreaterThan(0);
        expect(result.searchMethod).toBeDefined();
      }
    });

    test('should respect minScore filter', async () => {
      if (!testGameId) {
        test.skip();
        return;
      }

      const minScore = 0.8;
      const response = await apiContext.get(
        `/api/v1/search?gameId=${testGameId}&query=rules&searchMode=Hybrid&minScore=${minScore}`,
        {
          headers: {
            Cookie: sessionCookie,
          },
        }
      );

      const data = await response.json();

      data.results.forEach((result: any) => {
        expect(result.relevanceScore).toBeGreaterThanOrEqual(minScore);
      });
    });

    test('should respect topK limit', async () => {
      if (!testGameId) {
        test.skip();
        return;
      }

      const topK = 3;
      const response = await apiContext.get(
        `/api/v1/search?gameId=${testGameId}&query=game&searchMode=Hybrid&topK=${topK}`,
        {
          headers: {
            Cookie: sessionCookie,
          },
        }
      );

      const data = await response.json();
      expect(data.results.length).toBeLessThanOrEqual(topK);
    });
  });

  test.describe('Performance', () => {
    test('search should complete in under 2 seconds', async () => {
      if (!testGameId) {
        test.skip();
        return;
      }

      const startTime = Date.now();

      await apiContext.get(`/api/v1/search?gameId=${testGameId}&query=rules&searchMode=Hybrid`, {
        headers: {
          Cookie: sessionCookie,
        },
      });

      const duration = Date.now() - startTime;
      console.log(`Search duration: ${duration}ms`);

      expect(duration).toBeLessThan(2000);
    });

    test('Q&A should complete in under 10 seconds', async () => {
      if (!testGameId) {
        test.skip();
        return;
      }

      const startTime = Date.now();

      await apiContext.post('/api/v1/agents/qa', {
        headers: {
          Cookie: sessionCookie,
        },
        data: {
          gameId: testGameId,
          query: 'How to win?',
          searchMode: 'Hybrid',
        },
      });

      const duration = Date.now() - startTime;
      console.log(`Q&A duration: ${duration}ms`);

      expect(duration).toBeLessThan(10000);
    });
  });

  test.describe('Quality Metrics', () => {
    test('should return confidence scores in valid range', async () => {
      if (!testGameId) {
        test.skip();
        return;
      }

      const response = await apiContext.post('/api/v1/agents/qa', {
        headers: {
          Cookie: sessionCookie,
        },
        data: {
          gameId: testGameId,
          query: 'What is the objective?',
          searchMode: 'Hybrid',
        },
      });

      const data = await response.json();

      // Confidence should be between 0 and 1
      expect(data.confidence).toBeGreaterThanOrEqual(0);
      expect(data.confidence).toBeLessThanOrEqual(1);

      // For production, we expect confidence > 0.5 for good answers
      if (data.confidence > 0.5) {
        expect(data.answer.length).toBeGreaterThan(20);
      }
    });

    test('should include citations with page numbers', async () => {
      if (!testGameId) {
        test.skip();
        return;
      }

      const response = await apiContext.post('/api/v1/agents/qa', {
        headers: {
          Cookie: sessionCookie,
        },
        data: {
          gameId: testGameId,
          query: 'setup instructions',
          searchMode: 'Hybrid',
        },
      });

      const data = await response.json();

      if (data.snippets && data.snippets.length > 0) {
        const snippet = data.snippets[0];
        expect(snippet.page).toBeGreaterThan(0);
        expect(snippet.text).toBeDefined();
        expect(snippet.score).toBeGreaterThan(0);
      }
    });
  });
});
