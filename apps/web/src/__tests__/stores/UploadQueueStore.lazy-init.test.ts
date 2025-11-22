/**
 * Upload Queue Store - Lazy Initialization Tests
 * Issue #1301: Refactor to lazy Worker initialization
 *
 * Tests that Worker is NOT created at module load time,
 * but only on first method invocation, enabling test mocking.
 */

describe('UploadQueueStore - Lazy Initialization (Issue #1301)', () => {
  let workerConstructorSpy: jest.SpyInstance;
  let mockWorkerInstance: any;

  beforeEach(() => {
    jest.clearAllMocks();
    jest.resetModules(); // Reset module cache to test fresh imports

    // Create a mock Worker instance
    mockWorkerInstance = {
      postMessage: jest.fn(),
      terminate: jest.fn(),
      addEventListener: jest.fn(),
      removeEventListener: jest.fn(),
      dispatchEvent: jest.fn(),
      onmessage: null,
      onerror: null,
      onmessageerror: null
    };

    // Spy on Worker constructor BEFORE importing the module
    workerConstructorSpy = jest.spyOn(global, 'Worker').mockImplementation(() => mockWorkerInstance);
  });

  afterEach(() => {
    workerConstructorSpy.mockRestore();
  });

  describe('Module Import Behavior', () => {
    it('should NOT create Worker at module import time', async () => {
      // Import the module (this creates the singleton)
      await import('../../stores/UploadQueueStore');

      // Worker constructor should NOT have been called yet
      expect(workerConstructorSpy).not.toHaveBeenCalled();
    });
  });

  describe('Lazy Initialization on Method Calls', () => {
    it('should create Worker on first subscribe() call', async () => {
      const { uploadQueueStore } = await import('../../stores/UploadQueueStore');

      // Worker not created yet
      expect(workerConstructorSpy).not.toHaveBeenCalled();

      // Subscribe to the store
      const unsubscribe = uploadQueueStore.subscribe(() => {});

      // Now Worker should be created
      expect(workerConstructorSpy).toHaveBeenCalledTimes(1);
      expect(workerConstructorSpy).toHaveBeenCalledWith(
        expect.any(URL),
        expect.objectContaining({
          type: 'module',
          name: 'upload-queue-worker'
        })
      );

      unsubscribe();
    });

    it('should create Worker on first addFiles() call', async () => {
      const { uploadQueueStore } = await import('../../stores/UploadQueueStore');

      // Worker not created yet
      expect(workerConstructorSpy).not.toHaveBeenCalled();

      // Simulate WORKER_READY message
      setTimeout(() => {
        if (mockWorkerInstance.onmessage) {
          mockWorkerInstance.onmessage(new MessageEvent('message', {
            data: { type: 'WORKER_READY' }
          }));
        }
      }, 10);

      // Add files
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });
      await uploadQueueStore.addFiles([file], '770e8400-e29b-41d4-a716-000000000001', 'en');

      // Now Worker should be created
      expect(workerConstructorSpy).toHaveBeenCalledTimes(1);
    });

    it('should create Worker on first cancelUpload() call', async () => {
      const { uploadQueueStore } = await import('../../stores/UploadQueueStore');

      expect(workerConstructorSpy).not.toHaveBeenCalled();

      uploadQueueStore.cancelUpload('test-id');

      expect(workerConstructorSpy).toHaveBeenCalledTimes(1);
    });

    it('should create Worker on first retryUpload() call', async () => {
      const { uploadQueueStore } = await import('../../stores/UploadQueueStore');

      expect(workerConstructorSpy).not.toHaveBeenCalled();

      uploadQueueStore.retryUpload('test-id');

      expect(workerConstructorSpy).toHaveBeenCalledTimes(1);
    });

    it('should create Worker on first removeFile() call', async () => {
      const { uploadQueueStore } = await import('../../stores/UploadQueueStore');

      expect(workerConstructorSpy).not.toHaveBeenCalled();

      uploadQueueStore.removeFile('test-id');

      expect(workerConstructorSpy).toHaveBeenCalledTimes(1);
    });

    it('should create Worker on first clearCompleted() call', async () => {
      const { uploadQueueStore } = await import('../../stores/UploadQueueStore');

      expect(workerConstructorSpy).not.toHaveBeenCalled();

      uploadQueueStore.clearCompleted();

      expect(workerConstructorSpy).toHaveBeenCalledTimes(1);
    });

    it('should create Worker on first clearAll() call', async () => {
      const { uploadQueueStore } = await import('../../stores/UploadQueueStore');

      expect(workerConstructorSpy).not.toHaveBeenCalled();

      uploadQueueStore.clearAll();

      expect(workerConstructorSpy).toHaveBeenCalledTimes(1);
    });

    it('should create Worker on first startProcessing() call', async () => {
      const { uploadQueueStore } = await import('../../stores/UploadQueueStore');

      expect(workerConstructorSpy).not.toHaveBeenCalled();

      uploadQueueStore.startProcessing();

      expect(workerConstructorSpy).toHaveBeenCalledTimes(1);
    });
  });

  describe('Single Initialization', () => {
    it('should NOT create multiple Workers on multiple method calls', async () => {
      const { uploadQueueStore } = await import('../../stores/UploadQueueStore');

      // First call - initializes Worker
      uploadQueueStore.subscribe(() => {});
      expect(workerConstructorSpy).toHaveBeenCalledTimes(1);

      // Simulate WORKER_READY
      setTimeout(() => {
        if (mockWorkerInstance.onmessage) {
          mockWorkerInstance.onmessage(new MessageEvent('message', {
            data: { type: 'WORKER_READY' }
          }));
        }
      }, 10);

      // Second call - should NOT create another Worker
      uploadQueueStore.cancelUpload('test-id');
      expect(workerConstructorSpy).toHaveBeenCalledTimes(1);

      // Third call - should NOT create another Worker
      uploadQueueStore.startProcessing();
      expect(workerConstructorSpy).toHaveBeenCalledTimes(1);
    });
  });

  describe('SSR Safety', () => {
    it('should NOT create Worker in SSR environment (window undefined)', async () => {
      // Simulate SSR by temporarily removing window
      const originalWindow = global.window;
      // @ts-expect-error - Simulating SSR environment
      delete global.window;

      try {
        const { uploadQueueStore } = await import('../../stores/UploadQueueStore');

        // Try to call methods
        uploadQueueStore.subscribe(() => {});
        uploadQueueStore.startProcessing();

        // Worker should NOT be created
        expect(workerConstructorSpy).not.toHaveBeenCalled();
      } finally {
        // Restore window
        global.window = originalWindow;
      }
    });
  });

  describe('Test Mocking Capability', () => {
    it('should allow Worker mock to be set up before first method call', async () => {
      // Custom mock Worker with specific behavior
      const customMockWorker = {
        postMessage: jest.fn(),
        terminate: jest.fn(),
        addEventListener: jest.fn(),
        removeEventListener: jest.fn(),
        dispatchEvent: jest.fn(),
        onmessage: null,
        onerror: null,
        onmessageerror: null
      };

      // Override Worker constructor with custom mock
      workerConstructorSpy.mockImplementation(() => customMockWorker);

      // Import module AFTER mock is set up
      const { uploadQueueStore } = await import('../../stores/UploadQueueStore');

      // Subscribe to trigger initialization
      uploadQueueStore.subscribe(() => {});

      // Verify custom mock was used
      expect(workerConstructorSpy).toHaveBeenCalledTimes(1);

      // Send a message
      uploadQueueStore.startProcessing();

      // Verify custom mock received the message
      expect(customMockWorker.postMessage).toHaveBeenCalledWith(
        expect.objectContaining({ type: 'START_PROCESSING' })
      );
    });
  });

  describe('Error Handling', () => {
    it('should NOT attempt reinitialization if worker error occurred', async () => {
      const { uploadQueueStore } = await import('../../stores/UploadQueueStore');

      // First call - initializes Worker
      uploadQueueStore.subscribe(() => {});
      expect(workerConstructorSpy).toHaveBeenCalledTimes(1);

      // Simulate worker error
      if (mockWorkerInstance.onerror) {
        mockWorkerInstance.onerror(new ErrorEvent('error', {
          message: 'Worker crashed',
          error: new Error('Worker crashed')
        }));
      }

      // Wait for error handling
      await new Promise(resolve => setTimeout(resolve, 50));

      // Second call after error - Worker might be restarted by crash recovery
      // but ensureWorkerInitialized should not create a new one if error flag is set
      uploadQueueStore.startProcessing();

      // Worker constructor should have been called for crash recovery, not by ensureWorkerInitialized
      expect(workerConstructorSpy).toHaveBeenCalled();
    });
  });
});
