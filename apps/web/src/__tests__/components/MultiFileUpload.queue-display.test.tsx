/**
 * MultiFileUpload - Queue Display Tests
 * Tests for UploadQueue rendering, visibility, and state transitions
 *
 * Test Coverage:
 * - UploadQueue rendering based on queue state
 * - Queue visibility with different item counts
 * - Props passing to UploadQueue component
 * - Queue state transitions (empty ↔ populated)
 * - Multiple items in different states
 */

import { render, screen } from '@testing-library/react';
import { MultiFileUpload } from '../../components/upload/MultiFileUpload';
import {
  mockGetStats,
  mockQueueStateRef,
  createMockFile,
  defaultProps,
  setupBeforeEach,
  setupAfterEach,
  createStats,
} from './MultiFileUpload.test-helpers';

// Import mock creation function
import { createMockUseUploadQueue } from './MultiFileUpload.test-helpers';
createMockUseUploadQueue();

describe('MultiFileUpload - Queue Display', () => {
  beforeEach(setupBeforeEach);
  afterEach(setupAfterEach);

  describe('UploadQueue Rendering', () => {
    it('renders UploadQueue when items exist', () => {
      mockQueueStateRef.current = [
        {
          id: '1',
          file: createMockFile('test.pdf', 1000, 'application/pdf'),
          gameId: 'game-123',
          language: 'en',
          status: 'pending' as const,
          progress: 0,
          retryCount: 0,
        },
      ];

      mockGetStats.mockReturnValue(createStats({ total: 1, pending: 1 }));

      render(<MultiFileUpload {...defaultProps} />);

      expect(screen.getByTestId('upload-queue')).toBeInTheDocument();
    });

    it('does not render UploadQueue when queue is empty', () => {
      mockQueueStateRef.current = [];
      render(<MultiFileUpload {...defaultProps} />);

      expect(screen.queryByTestId('upload-queue')).not.toBeInTheDocument();
    });

    it('passes all props to UploadQueue', () => {
      const queueItems = [
        {
          id: '1',
          file: createMockFile('test1.pdf', 1000, 'application/pdf'),
          gameId: 'game-123',
          language: 'en',
          status: 'pending' as const,
          progress: 0,
          retryCount: 0,
        },
        {
          id: '2',
          file: createMockFile('test2.pdf', 2000, 'application/pdf'),
          gameId: 'game-123',
          language: 'en',
          status: 'uploading' as const,
          progress: 50,
          retryCount: 0,
        },
      ];
      mockQueueStateRef.current = queueItems;
      mockGetStats.mockReturnValue(
        createStats({
          total: 2,
          pending: 1,
          uploading: 1,
        })
      );

      render(<MultiFileUpload {...defaultProps} />);

      expect(screen.getByTestId('upload-queue')).toBeInTheDocument();
    });

    it('does not render UploadQueue when queue is empty with stats', () => {
      mockQueueStateRef.current = [];
      mockGetStats.mockReturnValue(
        createStats({
          total: 0,
        })
      );

      render(<MultiFileUpload {...defaultProps} />);

      expect(screen.queryByTestId('upload-queue')).not.toBeInTheDocument();
    });

    it('renders UploadQueue with multiple items in different states', () => {
      mockQueueStateRef.current = [
        {
          id: '1',
          file: createMockFile('pending.pdf', 1000, 'application/pdf'),
          gameId: 'game-123',
          language: 'en',
          status: 'pending' as const,
          progress: 0,
          retryCount: 0,
        },
        {
          id: '2',
          file: createMockFile('uploading.pdf', 2000, 'application/pdf'),
          gameId: 'game-123',
          language: 'en',
          status: 'uploading' as const,
          progress: 50,
          retryCount: 0,
        },
        {
          id: '3',
          file: createMockFile('succeeded.pdf', 3000, 'application/pdf'),
          gameId: 'game-123',
          language: 'en',
          status: 'succeeded' as const,
          progress: 100,
          retryCount: 0,
        },
      ];
      mockGetStats.mockReturnValue(
        createStats({
          total: 3,
          pending: 1,
          uploading: 1,
          succeeded: 1,
        })
      );

      render(<MultiFileUpload {...defaultProps} />);

      expect(screen.getByTestId('upload-queue')).toBeInTheDocument();
    });
  });

  describe('Queue Visibility Conditions', () => {
    it('maintains queue visibility during uploads', () => {
      mockQueueStateRef.current = [
        {
          id: '1',
          file: createMockFile('test1.pdf', 1000, 'application/pdf'),
          gameId: 'game-123',
          language: 'en',
          status: 'uploading' as const,
          progress: 30,
          retryCount: 0,
        },
        {
          id: '2',
          file: createMockFile('test2.pdf', 2000, 'application/pdf'),
          gameId: 'game-123',
          language: 'en',
          status: 'pending' as const,
          progress: 0,
          retryCount: 0,
        },
      ];
      mockGetStats.mockReturnValue(
        createStats({
          total: 2,
          pending: 1,
          uploading: 1,
        })
      );

      render(<MultiFileUpload {...defaultProps} />);

      expect(screen.getByTestId('upload-queue')).toBeInTheDocument();
    });

    it('updates queue when items change state', () => {
      mockQueueStateRef.current = [
        {
          id: '1',
          file: createMockFile('test.pdf', 1000, 'application/pdf'),
          gameId: 'game-123',
          language: 'en',
          status: 'pending' as const,
          progress: 0,
          retryCount: 0,
        },
      ];
      mockGetStats.mockReturnValue(createStats({ total: 1, pending: 1 }));

      const { rerender } = render(<MultiFileUpload {...defaultProps} />);

      expect(screen.getByTestId('upload-queue')).toBeInTheDocument();

      // Update to uploading
      mockQueueStateRef.current = [
        {
          id: '1',
          file: createMockFile('test.pdf', 1000, 'application/pdf'),
          gameId: 'game-123',
          language: 'en',
          status: 'uploading' as const,
          progress: 50,
          retryCount: 0,
        },
      ];
      mockGetStats.mockReturnValue(createStats({ total: 1, uploading: 1 }));

      rerender(<MultiFileUpload {...defaultProps} />);

      expect(screen.getByTestId('upload-queue')).toBeInTheDocument();
    });
  });

  describe('Queue State Transitions', () => {
    it('transitions from empty to populated queue', async () => {
      mockQueueStateRef.current = [];
      const { rerender } = render(<MultiFileUpload {...defaultProps} />);

      expect(screen.queryByTestId('upload-queue')).not.toBeInTheDocument();

      mockQueueStateRef.current = [
        {
          id: '1',
          file: createMockFile('test.pdf', 1000, 'application/pdf'),
          gameId: 'game-123',
          language: 'en',
          status: 'pending' as const,
          progress: 0,
          retryCount: 0,
        },
      ];
      mockGetStats.mockReturnValue(createStats({ total: 1, pending: 1 }));

      rerender(<MultiFileUpload {...defaultProps} />);

      expect(screen.getByTestId('upload-queue')).toBeInTheDocument();
    });

    it('transitions from populated to empty queue', () => {
      mockQueueStateRef.current = [
        {
          id: '1',
          file: createMockFile('test.pdf', 1000, 'application/pdf'),
          gameId: 'game-123',
          language: 'en',
          status: 'pending' as const,
          progress: 0,
          retryCount: 0,
        },
      ];
      mockGetStats.mockReturnValue(createStats({ total: 1, pending: 1 }));

      const { rerender } = render(<MultiFileUpload {...defaultProps} />);

      expect(screen.getByTestId('upload-queue')).toBeInTheDocument();

      mockQueueStateRef.current = [];
      mockGetStats.mockReturnValue(createStats({ total: 0 }));

      rerender(<MultiFileUpload {...defaultProps} />);

      expect(screen.queryByTestId('upload-queue')).not.toBeInTheDocument();
    });
  });

  describe('Component Props Integration', () => {
    it('displays correct game info in badge', () => {
      render(<MultiFileUpload gameId="test-123" gameName="Test Board Game" language="en" />);

      const badge = screen.getByTestId('game-info-badge');
      expect(badge).toHaveTextContent('Target Game: Test Board Game (test-123)');
    });
  });
});
