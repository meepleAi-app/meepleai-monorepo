/**
 * OpenRouter Chat Proxy Route
 * Issue #4779: SSE streaming proxy for agent chat via OpenRouter
 *
 * Accepts chat messages, constructs system prompts based on game context,
 * and proxies to OpenRouter API with SSE streaming.
 * Maps OpenRouter response chunks to MeepleAI StreamingEventType format
 * compatible with useAgentChatStream hook.
 */

import { NextRequest } from 'next/server';

export const dynamic = 'force-dynamic';
export const runtime = 'nodejs';

// ============================================================================
// Types
// ============================================================================

interface ChatProxyRequest {
  message: string;
  agentId: string;
  threadId?: string;
  gameContext: {
    gameName: string;
    agentTypology: string;
    ragContext?: string;
  };
  modelId?: string;
  maxTokens?: number;
  temperature?: number;
}

// StreamingEventType enum values (matching backend Contracts.cs)
const StreamingEventType = {
  StateUpdate: 0,
  Complete: 4,
  Error: 5,
  Heartbeat: 6,
  Token: 7,
} as const;

// ============================================================================
// Configuration
// ============================================================================

const OPENROUTER_API_URL = 'https://openrouter.ai/api/v1/chat/completions';
const DEFAULT_SYSTEM_PROMPT =
  'Sei un assistente esperto del gioco {gameName}. Aiuta i giocatori con regole, strategie e consigli. Rispondi sempre in italiano.';
const DEFAULT_TEMPERATURE = 0.7;
const DEFAULT_MAX_TOKENS = 1024;
const DEFAULT_MODEL =
  process.env.OPENROUTER_DEFAULT_MODEL || 'meta-llama/llama-3.3-70b-instruct:free';
const MAX_MESSAGE_LENGTH = 2000;
const RATE_LIMIT_MAX = 10;
const RATE_LIMIT_WINDOW_MS = 60_000;
const HEARTBEAT_INTERVAL_MS = 15_000;

// ============================================================================
// Rate Limiting (in-memory, per-instance)
// ============================================================================

const rateLimitMap = new Map<string, { count: number; resetAt: number }>();

/** @internal Exported for testing only */
export function _resetRateLimitForTesting(): void {
  rateLimitMap.clear();
}

function checkRateLimit(clientId: string): boolean {
  const now = Date.now();
  const entry = rateLimitMap.get(clientId);

  if (!entry || now >= entry.resetAt) {
    rateLimitMap.set(clientId, { count: 1, resetAt: now + RATE_LIMIT_WINDOW_MS });
    return true;
  }

  if (entry.count >= RATE_LIMIT_MAX) {
    return false;
  }

  entry.count++;
  return true;
}

// Periodically clean expired entries
setInterval(() => {
  const now = Date.now();
  for (const [key, entry] of rateLimitMap) {
    if (now >= entry.resetAt) {
      rateLimitMap.delete(key);
    }
  }
}, RATE_LIMIT_WINDOW_MS);

// ============================================================================
// System Prompt Builder
// ============================================================================

function buildSystemPrompt(gameContext: ChatProxyRequest['gameContext']): string {
  let prompt = DEFAULT_SYSTEM_PROMPT.replace(/{gameName}/g, gameContext.gameName);

  if (gameContext.ragContext) {
    prompt += `\n\nContesto dalla knowledge base:\n${gameContext.ragContext}`;
  }

  return prompt;
}

// ============================================================================
// SSE Helpers
// ============================================================================

function sseEvent(type: number, data: unknown): string {
  return `data: ${JSON.stringify({ type, data, timestamp: new Date().toISOString() })}\n\n`;
}

// ============================================================================
// POST Handler
// ============================================================================

export async function POST(request: NextRequest) {
  const apiKey = process.env.OPENROUTER_API_KEY;
  if (!apiKey) {
    return new Response(JSON.stringify({ error: 'OpenRouter API key not configured' }), {
      status: 503,
      headers: { 'Content-Type': 'application/json' },
    });
  }

  // Rate limiting - use forwarded IP or fallback
  const clientId =
    request.headers.get('x-forwarded-for')?.split(',')[0]?.trim() ||
    request.headers.get('x-real-ip') ||
    'anonymous';

  if (!checkRateLimit(clientId)) {
    return new Response(
      JSON.stringify({ error: 'Rate limit exceeded. Max 10 requests per minute.' }),
      { status: 429, headers: { 'Content-Type': 'application/json' } }
    );
  }

  // Parse and validate request body
  let body: ChatProxyRequest;
  try {
    body = await request.json();
  } catch {
    return new Response(JSON.stringify({ error: 'Invalid JSON body' }), {
      status: 400,
      headers: { 'Content-Type': 'application/json' },
    });
  }

  if (!body.message || typeof body.message !== 'string') {
    return new Response(JSON.stringify({ error: 'message is required and must be a string' }), {
      status: 400,
      headers: { 'Content-Type': 'application/json' },
    });
  }

  if (body.message.length > MAX_MESSAGE_LENGTH) {
    return new Response(
      JSON.stringify({ error: `message must be ${MAX_MESSAGE_LENGTH} characters or less` }),
      { status: 400, headers: { 'Content-Type': 'application/json' } }
    );
  }

  if (!body.agentId || typeof body.agentId !== 'string') {
    return new Response(JSON.stringify({ error: 'agentId is required' }), {
      status: 400,
      headers: { 'Content-Type': 'application/json' },
    });
  }

  if (!body.gameContext?.gameName || !body.gameContext?.agentTypology) {
    return new Response(
      JSON.stringify({ error: 'gameContext with gameName and agentTypology is required' }),
      { status: 400, headers: { 'Content-Type': 'application/json' } }
    );
  }

  // Build OpenRouter request
  const systemPrompt = buildSystemPrompt(body.gameContext);
  const model = body.modelId || DEFAULT_MODEL;
  const maxTokens = Math.min(body.maxTokens || DEFAULT_MAX_TOKENS, 4096);
  const temperature = Math.max(0, Math.min(body.temperature ?? DEFAULT_TEMPERATURE, 2));

  const openRouterBody = {
    model,
    messages: [
      { role: 'system', content: systemPrompt },
      { role: 'user', content: body.message },
    ],
    stream: true,
    max_tokens: maxTokens,
    temperature,
  };

  // Create SSE response stream
  const encoder = new TextEncoder();
  let heartbeatTimer: ReturnType<typeof setInterval> | null = null;
  let openRouterAbortController: AbortController | null = new AbortController();

  const stream = new ReadableStream({
    async start(controller) {
      // Start heartbeat timer
      heartbeatTimer = setInterval(() => {
        try {
          controller.enqueue(encoder.encode(sseEvent(StreamingEventType.Heartbeat, null)));
        } catch {
          // Stream already closed
          if (heartbeatTimer) clearInterval(heartbeatTimer);
        }
      }, HEARTBEAT_INTERVAL_MS);

      // Send initial state update
      controller.enqueue(
        encoder.encode(
          sseEvent(StreamingEventType.StateUpdate, {
            message: 'Connecting to AI model...',
            chatThreadId: body.threadId || null,
          })
        )
      );

      let totalTokens = 0;

      try {
        const response = await fetch(OPENROUTER_API_URL, {
          method: 'POST',
          headers: {
            Authorization: `Bearer ${apiKey}`,
            'Content-Type': 'application/json',
            'HTTP-Referer': 'https://meepleai.com',
            'X-Title': 'MeepleAI Agent Chat',
          },
          body: JSON.stringify(openRouterBody),
          signal: openRouterAbortController?.signal,
        });

        if (!response.ok) {
          const errorText = await response.text().catch(() => 'Unknown error');
          controller.enqueue(
            encoder.encode(
              sseEvent(StreamingEventType.Error, {
                errorMessage: `AI model error (${response.status}): ${errorText}`,
                errorCode: 'MODEL_ERROR',
              })
            )
          );
          controller.close();
          return;
        }

        const reader = response.body?.getReader();
        if (!reader) {
          controller.enqueue(
            encoder.encode(
              sseEvent(StreamingEventType.Error, {
                errorMessage: 'No response stream from AI model',
                errorCode: 'NO_STREAM',
              })
            )
          );
          controller.close();
          return;
        }

        const decoder = new TextDecoder();
        let buffer = '';

        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          buffer += decoder.decode(value, { stream: true });

          // Process complete SSE lines
          const lines = buffer.split('\n');
          buffer = lines.pop() || '';

          for (const line of lines) {
            const trimmed = line.trim();
            if (!trimmed || trimmed.startsWith(':')) continue;

            if (trimmed === 'data: [DONE]') {
              // Stream complete
              continue;
            }

            if (trimmed.startsWith('data: ')) {
              try {
                const chunk = JSON.parse(trimmed.slice(6));
                const delta = chunk.choices?.[0]?.delta;

                if (delta?.content) {
                  totalTokens++;
                  controller.enqueue(
                    encoder.encode(
                      sseEvent(StreamingEventType.Token, {
                        token: delta.content,
                      })
                    )
                  );
                }

                // Check for usage info in final chunk
                if (chunk.usage) {
                  totalTokens = chunk.usage.total_tokens || totalTokens;
                }
              } catch {
                // Skip malformed chunks
              }
            }
          }
        }

        // Send complete event
        controller.enqueue(
          encoder.encode(
            sseEvent(StreamingEventType.Complete, {
              totalTokens,
              chatThreadId: body.threadId || null,
            })
          )
        );
      } catch (error) {
        if (error instanceof Error && error.name === 'AbortError') {
          // Client disconnected - clean close
        } else {
          const errorMsg = error instanceof Error ? error.message : 'Stream processing failed';
          try {
            controller.enqueue(
              encoder.encode(
                sseEvent(StreamingEventType.Error, {
                  errorMessage: errorMsg,
                  errorCode: 'STREAM_ERROR',
                })
              )
            );
          } catch {
            // Controller already closed
          }
        }
      } finally {
        if (heartbeatTimer) clearInterval(heartbeatTimer);
        openRouterAbortController = null;
        try {
          controller.close();
        } catch {
          // Already closed
        }
      }
    },
    cancel() {
      // Client disconnected
      if (heartbeatTimer) clearInterval(heartbeatTimer);
      openRouterAbortController?.abort();
      openRouterAbortController = null;
    },
  });

  return new Response(stream, {
    headers: {
      'Content-Type': 'text/event-stream',
      'Cache-Control': 'no-cache, no-transform',
      Connection: 'keep-alive',
      'X-Accel-Buffering': 'no',
    },
  });
}
