/**
 * Test helpers for test-utils tests
 */

/**
 * Sample status codes for API response testing
 */
export const statusCodes = {
  success: {
    ok: 200,
    created: 201,
    noContent: 204,
    edge: 299,
  },
  error: {
    badRequest: 400,
    unauthorized: 401,
    notFound: 404,
    serverError: 500,
  },
  redirect: {
    multipleChoices: 300,
  },
};

/**
 * Sample payloads for testing different data types
 */
export const samplePayloads = {
  object: { id: '123', name: 'Test' },
  array: [1, 2, 3],
  string: 'plain text',
  emptyString: '',
  number: 42,
  boolean: true,
  null: null,
  undefined: undefined,
};

/**
 * Event type and status cycles for mock event generation
 */
export const eventCycles = {
  types: ['message', 'rag_search', 'rag_retrieval', 'rag_rerank', 'rag_complete', 'error'],
  statuses: ['pending', 'success', 'error', 'completed'],
};
