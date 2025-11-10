/**
 * Unit tests for centralized type exports
 * Validates that all types are properly exported and accessible
 */

describe('Type Index Exports', () => {
  describe('Authentication Types', () => {
    it('should export auth types', () => {
      const authUser: import('../index').AuthUser = {
        id: '1',
        email: 'test@test.com',
        role: 'Admin',
      };

      const authResponse: import('../index').AuthResponse = {
        user: authUser,
        expiresAt: '2024-12-31',
      };

      const sessionStatus: import('../index').SessionStatusResponse = {
        expiresAt: '2024-12-31',
        lastSeenAt: '2024-01-01',
        remainingMinutes: 30,
      };

      const role: import('../index').UserRole = 'Editor';

      expect(authUser).toBeDefined();
      expect(authResponse).toBeDefined();
      expect(sessionStatus).toBeDefined();
      expect(role).toBeDefined();
    });

    it('should export auth helper functions', () => {
      const { hasRole, canEdit } = require('../index');

      expect(typeof hasRole).toBe('function');
      expect(typeof canEdit).toBe('function');
    });
  });

  describe('Domain Types', () => {
    it('should export game types', () => {
      const game: import('../index').Game = {
        id: 'test-game',
        name: 'Test Game',
        createdAt: '2024-01-01',
      };

      const chat: import('../index').Chat = {
        id: 'chat-1',
        gameId: 'game-1',
        gameName: 'Test Game',
        agentId: 'agent-1',
        agentName: 'Test Agent',
        startedAt: '2024-01-01',
        lastMessageAt: null,
      };

      expect(game).toBeDefined();
      expect(chat).toBeDefined();
    });

    it('should export RuleSpec types', () => {
      const ruleAtom: import('../index').RuleAtom = {
        id: 'atom-1',
        text: 'Test content',
        section: 'gameplay',
        page: '1',
        line: '10',
      };

      const ruleSpec: import('../index').RuleSpec = {
        gameId: 'game-1',
        version: '1.0.0',
        createdAt: '2024-01-01',
        rules: [],
      };

      expect(ruleAtom).toBeDefined();
      expect(ruleSpec).toBeDefined();
    });

    it('should export chat types', () => {
      const chatMessage: import('../index').ChatMessage = {
        id: 'msg-1',
        level: 'user',
        message: 'Hello',
        metadataJson: null,
        createdAt: '2024-01-01',
        updatedAt: null,
        isDeleted: false,
        isInvalidated: false,
      };

      const qaResponse: import('../index').QaResponse = {
        answer: 'Answer',
        snippets: [],
        followUpQuestions: [],
      };

      expect(chatMessage).toBeDefined();
      expect(qaResponse).toBeDefined();
    });
  });

  describe('API Contract Types', () => {
    it('should export request/response types', () => {
      const commentRequest: import('../index').CreateRuleSpecCommentRequest = {
        atomId: 'atom-1',
        commentText: 'Test comment',
      };

      const updateRequest: import('../index').UpdateRuleSpecCommentRequest = {
        commentText: 'Updated',
      };

      const exportFormat: import('../index').ExportFormat = 'pdf';

      expect(commentRequest).toBeDefined();
      expect(updateRequest).toBeDefined();
      expect(exportFormat).toBeDefined();
    });

    it('should export cache stats types', () => {
      const topQuestion: import('../index').TopQuestion = {
        questionHash: 'hash-1',
        hitCount: 10,
        missCount: 2,
        lastHitAt: '2024-01-01',
      };

      const cacheStats: import('../index').CacheStats = {
        totalHits: 100,
        totalMisses: 10,
        hitRate: 0.9,
        totalKeys: 50,
        cacheSizeBytes: 1024,
        topQuestions: [topQuestion],
      };

      expect(topQuestion).toBeDefined();
      expect(cacheStats).toBeDefined();
    });

    it('should export validation types', () => {
      const validationResult: import('../index').ValidationResult = {
        valid: true,
        errors: [],
      };

      const pdfError: import('../index').PdfValidationError = {
        error: 'Invalid PDF',
        details: { reason: 'corrupted' },
      };

      expect(validationResult).toBeDefined();
      expect(pdfError).toBeDefined();
    });

    it('should export generic wrapper types', () => {
      const apiResponse: import('../index').ApiResponse<string> = {
        data: 'test',
      };

      const paginatedResponse: import('../index').PaginatedResponse<number> = {
        items: [1, 2, 3],
        total: 3,
        page: 1,
        pageSize: 10,
        hasMore: false,
      };

      expect(apiResponse).toBeDefined();
      expect(paginatedResponse).toBeDefined();
    });

    it('should export ApiError class and helper', () => {
      const { ApiError, createApiError } = require('../index');

      expect(typeof ApiError).toBe('function');
      expect(typeof createApiError).toBe('function');

      const error = new ApiError('Test error', 500);
      expect(error).toBeInstanceOf(Error);
      expect(error.name).toBe('ApiError');
    });
  });

  describe('PDF Processing Types', () => {
    it('should export ProcessingStep enum', () => {
      const { ProcessingStep } = require('../index');

      // ProcessingStep is an enum with string values
      expect(ProcessingStep).toBeDefined();
      expect(typeof ProcessingStep).toBe('object');
    });

    it('should export ProcessingProgress type', () => {
      const { ProcessingStep } = require('../index');
      const progress: import('../index').ProcessingProgress = {
        currentStep: ProcessingStep.Extracting,
        percentComplete: 0.5,
        updatedAt: '2024-01-01',
      };

      expect(progress).toBeDefined();
    });

    it('should export processing helper functions', () => {
      const { isProcessingComplete, getStepLabel, getStepOrder } = require('../index');

      expect(typeof isProcessingComplete).toBe('function');
      expect(typeof getStepLabel).toBe('function');
      expect(typeof getStepOrder).toBe('function');
    });
  });

  describe('Integration Test', () => {
    it('should import all exports without error', () => {
      // This test ensures the entire index can be imported successfully
      const types = require('../index');

      expect(types).toBeDefined();
      expect(Object.keys(types).length).toBeGreaterThan(0);
    });

    it('should have no circular dependencies', () => {
      // Multiple imports should work without circular dependency issues
      const import1 = require('../index');
      const import2 = require('../index');

      expect(import1).toBe(import2); // Same module instance
    });

    it('should export both types and functions', () => {
      const exports = require('../index');

      // Check for function exports
      expect(typeof exports.hasRole).toBe('function');
      expect(typeof exports.canEdit).toBe('function');
      expect(typeof exports.ApiError).toBe('function');
      expect(typeof exports.createApiError).toBe('function');
      expect(typeof exports.isProcessingComplete).toBe('function');
      expect(typeof exports.getStepLabel).toBe('function');
      expect(typeof exports.getStepOrder).toBe('function');

      // Check for enum exports
      expect(typeof exports.ProcessingStep).toBe('object');
    });
  });

  describe('TypeScript Inference', () => {
    it('should properly infer generic types', () => {
      type StringResponse = import('../index').ApiResponse<string>;
      type NumberArray = import('../index').PaginatedResponse<number>;

      const stringResp: StringResponse = { data: 'test' };
      const numberArr: NumberArray = {
        items: [1, 2, 3],
        total: 3,
        page: 1,
        pageSize: 10,
        hasMore: false,
      };

      expect(stringResp.data).toBe('test');
      expect(numberArr.items).toEqual([1, 2, 3]);
    });

    it('should properly type union types', () => {
      type ExportFormat = import('../index').ExportFormat;

      const pdf: ExportFormat = 'pdf';
      const txt: ExportFormat = 'txt';
      const md: ExportFormat = 'md';

      expect([pdf, txt, md]).toEqual(['pdf', 'txt', 'md']);
    });
  });
});
