/**
 * Agent Session Launch Hook
 * Issue #3375 - Agent Session Launch API Integration
 *
 * Manages agent session launch with loading state, error handling, and navigation.
 */

'use client';

import { useState, useCallback } from 'react';

import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import {
  createAgentSessionsClient,
  type LaunchSessionAgentRequest,
} from '@/lib/api/clients/agentSessionsClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import { useAgentStore } from '@/stores/agentStore';

interface UseAgentSessionLaunchOptions {
  onSuccess?: (agentSessionId: string) => void;
  onError?: (error: Error) => void;
  redirectToChat?: boolean;
}

interface UseAgentSessionLaunchReturn {
  launch: (gameSessionId: string, overrides?: Partial<LaunchSessionAgentRequest>) => Promise<string | null>;
  isLaunching: boolean;
  error: Error | null;
  reset: () => void;
}

/**
 * Hook for launching agent sessions with full lifecycle management
 */
export function useAgentSessionLaunch(
  options: UseAgentSessionLaunchOptions = {}
): UseAgentSessionLaunchReturn {
  const { onSuccess, onError, redirectToChat = true } = options;

  const [isLaunching, setIsLaunching] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  const router = useRouter();

  const {
    selectedTypologyId,
    selectedGameId,
  } = useAgentStore();

  const httpClient = new HttpClient();
  const agentSessionsClient = createAgentSessionsClient({ httpClient });

  const reset = useCallback(() => {
    setError(null);
    setIsLaunching(false);
  }, []);

  const launch = useCallback(
    async (
      gameSessionId: string,
      overrides?: Partial<LaunchSessionAgentRequest>
    ): Promise<string | null> => {
      // Validate required fields
      const typologyId = overrides?.typologyId || selectedTypologyId;
      const gameId = overrides?.gameId || selectedGameId;

      if (!typologyId) {
        const err = new Error('Please select an agent template');
        setError(err);
        toast.error('Configuration Required', {
          description: 'Please select an agent template before launching.',
        });
        onError?.(err);
        return null;
      }

      if (!gameId) {
        const err = new Error('Please select a game');
        setError(err);
        toast.error('Configuration Required', {
          description: 'Please select a game before launching.',
        });
        onError?.(err);
        return null;
      }

      setIsLaunching(true);
      setError(null);

      try {
        const request: LaunchSessionAgentRequest = {
          typologyId,
          agentId: typologyId, // Using typologyId as agentId for now
          gameId,
          initialGameStateJson: overrides?.initialGameStateJson || '{}',
        };

        const response = await agentSessionsClient.launch(gameSessionId, request);

        toast.success('Agent Launched', {
          description: 'Your AI assistant is ready to help!',
        });

        onSuccess?.(response.agentSessionId);

        if (redirectToChat) {
          // Navigate to agent chat page
          router.push(`/games/${gameId}/chat?session=${response.agentSessionId}`);
        }

        return response.agentSessionId;
      } catch (err) {
        const error = err instanceof Error ? err : new Error('Failed to launch agent');

        setError(error);

        // Handle specific error types
        if (error.message.includes('quota')) {
          toast.error('Quota Exceeded', {
            description: 'You have reached your agent usage limit. Please upgrade your plan.',
          });
        } else if (error.message.includes('invalid')) {
          toast.error('Invalid Configuration', {
            description: 'Please check your agent settings and try again.',
          });
        } else {
          toast.error('Launch Failed', {
            description: error.message || 'Unable to start agent session. Please try again.',
          });
        }

        onError?.(error);
        return null;
      } finally {
        setIsLaunching(false);
      }
    },
    [
      selectedTypologyId,
      selectedGameId,
      agentSessionsClient,
      router,
      onSuccess,
      onError,
      redirectToChat,
    ]
  );

  return {
    launch,
    isLaunching,
    error,
    reset,
  };
}
