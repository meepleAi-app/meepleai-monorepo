/**
 * Tests for test-utils rendering and async utilities
 */

import {
  renderWithProviders,
  mockApiResponse,
  waitForAsync,
} from './test-utils';
import React from 'react';
import { statusCodes, samplePayloads } from './test-utils.test-helpers';

describe('test-utils - Rendering and Async', () => {
  describe('renderWithProviders', () => {
    describe('Happy Path', () => {
      it('should render children correctly', () => {
        const { getByText } = renderWithProviders(<div>hello world</div>);
        expect(getByText('hello world')).toBeInTheDocument();
      });

      it('should render complex components', () => {
        const TestComponent: React.FC = () => (
          <div>
            <h1>Title</h1>
            <p>Paragraph</p>
            <button>Click me</button>
          </div>
        );

        const { getByText, getByRole } = renderWithProviders(<TestComponent />);

        expect(getByText('Title')).toBeInTheDocument();
        expect(getByText('Paragraph')).toBeInTheDocument();
        expect(getByRole('button')).toBeInTheDocument();
      });

      it('should accept custom options', () => {
        const { container } = renderWithProviders(<div>test</div>, {
          container: document.body,
        });

        expect(container).toBe(document.body);
      });
    });

    describe('Edge Cases', () => {
      it('should render null children', () => {
        const { container } = renderWithProviders(<></>);
        expect(container).toBeInTheDocument();
      });

      it('should render empty fragment', () => {
        const { container } = renderWithProviders(<></>);
        expect(container).toBeInTheDocument();
      });

      it('should render nested components', () => {
        const { getByTestId } = renderWithProviders(
          <div data-testid="parent">
            <div data-testid="child">
              <span data-testid="grandchild">Deep</span>
            </div>
          </div>
        );

        expect(getByTestId('parent')).toBeInTheDocument();
        expect(getByTestId('child')).toBeInTheDocument();
        expect(getByTestId('grandchild')).toBeInTheDocument();
      });
    });
  });

  describe('mockApiResponse', () => {
    describe('Successful Responses', () => {
      it('should create response with status 200 by default', async () => {
        const response = await mockApiResponse(statusCodes.success.ok, samplePayloads.object);

        expect(response.ok).toBe(true);
        expect(response.status).toBe(200);
      });

      it('should return JSON payload correctly', async () => {
        const response = await mockApiResponse(statusCodes.success.ok, samplePayloads.object);

        const data = await response.json();
        expect(data).toEqual(samplePayloads.object);
      });

      it('should return text payload for strings', async () => {
        const response = await mockApiResponse(statusCodes.success.ok, samplePayloads.string);

        const text = await response.text();
        expect(text).toBe(samplePayloads.string);
      });

      it('should handle 201 Created status', async () => {
        const response = await mockApiResponse(statusCodes.success.created, { id: 'new-item' });

        expect(response.status).toBe(201);
        expect(response.ok).toBe(true);
      });

      it('should handle 204 No Content', async () => {
        const response = await mockApiResponse(statusCodes.success.noContent);

        expect(response.status).toBe(204);
        expect(response.ok).toBe(true);
      });

      it('should have headers', async () => {
        const response = await mockApiResponse(statusCodes.success.ok, {});

        expect(response.headers).toBeInstanceOf(Headers);
      });
    });

    describe('Error Responses', () => {
      it('should mark 400 as not ok', async () => {
        const response = await mockApiResponse(statusCodes.error.badRequest, { error: 'Bad Request' });

        expect(response.ok).toBe(false);
        expect(response.status).toBe(400);
      });

      it('should mark 401 as not ok', async () => {
        const response = await mockApiResponse(statusCodes.error.unauthorized);

        expect(response.ok).toBe(false);
        expect(response.status).toBe(401);
      });

      it('should mark 404 as not ok', async () => {
        const response = await mockApiResponse(statusCodes.error.notFound);

        expect(response.ok).toBe(false);
        expect(response.status).toBe(404);
      });

      it('should mark 500 as not ok', async () => {
        const response = await mockApiResponse(statusCodes.error.serverError);

        expect(response.ok).toBe(false);
        expect(response.status).toBe(500);
      });
    });

    describe('Payload Handling', () => {
      it('should handle undefined payload', async () => {
        const response = await mockApiResponse(statusCodes.success.ok);

        const data = await response.json();
        expect(data).toBeUndefined();
      });

      it('should handle null payload', async () => {
        const response = await mockApiResponse(statusCodes.success.ok, samplePayloads.null);

        const data = await response.json();
        expect(data).toBeNull();
      });

      it('should handle array payload', async () => {
        const response = await mockApiResponse(statusCodes.success.ok, samplePayloads.array);

        const data = await response.json();
        expect(data).toEqual(samplePayloads.array);
      });

      it('should handle boolean payload', async () => {
        const response = await mockApiResponse(statusCodes.success.ok, samplePayloads.boolean);

        const data = await response.json();
        expect(data).toBe(true);
      });

      it('should handle number payload', async () => {
        const response = await mockApiResponse(statusCodes.success.ok, samplePayloads.number);

        const data = await response.json();
        expect(data).toBe(42);
      });

      it('should stringify non-string payloads for text()', async () => {
        const response = await mockApiResponse(statusCodes.success.ok, samplePayloads.object);

        const text = await response.text();
        expect(text).toBe(JSON.stringify(samplePayloads.object));
      });
    });

    describe('Edge Cases', () => {
      it('should handle 299 as successful', async () => {
        const response = await mockApiResponse(statusCodes.success.edge);

        expect(response.ok).toBe(true);
      });

      it('should handle 300 as not ok (redirect)', async () => {
        const response = await mockApiResponse(statusCodes.redirect.multipleChoices);

        expect(response.ok).toBe(false);
      });

      it('should handle empty string payload', async () => {
        const response = await mockApiResponse(statusCodes.success.ok, samplePayloads.emptyString);

        const text = await response.text();
        expect(text).toBe('');
      });
    });
  });

  describe('waitForAsync', () => {
    describe('Successful Assertions', () => {
      it('should resolve when assertion passes immediately', async () => {
        await expect(
          waitForAsync(() => {
            expect(true).toBe(true);
          })
        ).resolves.not.toThrow();
      });

      it('should wait for async condition to become true', async () => {
        let ready = false;

        setTimeout(() => {
          ready = true;
        }, 100);

        await waitForAsync(() => {
          expect(ready).toBe(true);
        });

        expect(ready).toBe(true);
      });

      it('should use default timeout of 1000ms', async () => {
        let ready = false;

        setTimeout(() => {
          ready = true;
        }, 500);

        await waitForAsync(() => {
          expect(ready).toBe(true);
        });

        expect(ready).toBe(true);
      });

      it('should accept custom timeout', async () => {
        let ready = false;

        setTimeout(() => {
          ready = true;
        }, 1200);

        await waitForAsync(() => {
          expect(ready).toBe(true);
        }, 2000);

        expect(ready).toBe(true);
      });
    });

    describe('Timeout Behavior', () => {
      it('should throw if assertion never passes', async () => {
        await expect(
          waitForAsync(() => {
            expect(false).toBe(true);
          }, 200)
        ).rejects.toThrow();
      });

      it('should timeout after specified milliseconds', async () => {
        const startTime = Date.now();

        try {
          await waitForAsync(() => {
            throw new Error('Never ready');
          }, 300);
        } catch (error) {
          const elapsed = Date.now() - startTime;
          expect(elapsed).toBeGreaterThanOrEqual(300);
        }
      });

      it('should throw the assertion error on timeout', async () => {
        await expect(
          waitForAsync(() => {
            throw new Error('Custom error');
          }, 100)
        ).rejects.toThrow('Custom error');
      });
    });

    describe('Retry Logic', () => {
      it('should retry assertions until success', async () => {
        let attempts = 0;

        await waitForAsync(() => {
          attempts++;
          if (attempts < 3) {
            throw new Error('Not yet');
          }
          expect(true).toBe(true);
        });

        expect(attempts).toBeGreaterThanOrEqual(3);
      });

      it('should wait 50ms between retries', async () => {
        const timestamps: number[] = [];

        let attempts = 0;
        await waitForAsync(() => {
          timestamps.push(Date.now());
          attempts++;
          if (attempts < 3) {
            throw new Error('Not yet');
          }
        });

        const gap1 = timestamps[1] - timestamps[0];
        const gap2 = timestamps[2] - timestamps[1];

        expect(gap1).toBeGreaterThanOrEqual(45); // Allow some margin
        expect(gap2).toBeGreaterThanOrEqual(45);
      });
    });

    describe('Edge Cases', () => {
      it('should handle zero timeout (immediate success if assertion passes)', async () => {
        // With zero timeout, if assertion passes immediately, it succeeds
        await expect(
          waitForAsync(() => {
            expect(true).toBe(true);
          }, 0)
        ).resolves.not.toThrow();
      });

      it('should handle very short timeout', async () => {
        await expect(
          waitForAsync(() => {
            expect(false).toBe(true);
          }, 10)
        ).rejects.toThrow();
      });

      it('should handle synchronous failure immediately', async () => {
        const startTime = Date.now();

        try {
          await waitForAsync(() => {
            expect(false).toBe(true);
          }, 100);
        } catch (error) {
          const elapsed = Date.now() - startTime;
          expect(elapsed).toBeGreaterThanOrEqual(100);
        }
      });
    });
  });
});
