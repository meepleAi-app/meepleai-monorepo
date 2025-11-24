/**
 * Streaming SSE Schemas (Issue #1007)
 *
 * Type-safe schemas for Server-Sent Events (SSE) streaming responses.
 * Matches backend RagStreamingEvent structure from Issue #1186.
 *
 * Backend contract:
 * - Content-Type: text/event-stream
 * - Format: "data: {json}\n\n"
 * - Event types: StateUpdate, Citations, Token, Complete, Error, FollowUpQuestions, SetupStep
 *
 * @see apps/api/src/Api/Models/Contracts.cs (RagStreamingEvent)
 * @see apps/api/src/Api/Routing/AiEndpoints.cs (SSE endpoints)
 */

import { z } from 'zod';

/**
 * Streaming event types matching backend StreamingEventType enum
 */
export enum StreamingEventType {
  StateUpdate = 'stateUpdate',
  Citations = 'citations',
  Token = 'token',
  Complete = 'complete',
  Error = 'error',
  FollowUpQuestions = 'followUpQuestions',
  SetupStep = 'setupStep',
}

/**
 * State update event data
 * Sent during RAG processing to show progress
 */
export const StreamingStateUpdateSchema = z.object({
  state: z.string(), // e.g., "Generating embeddings...", "Searching vector database..."
});

export type StreamingStateUpdate = z.infer<typeof StreamingStateUpdateSchema>;

/**
 * Citation/source reference
 * Matches backend Citation structure
 */
export const CitationSchema = z.object({
  documentId: z.string().optional().nullable(),
  source: z.string(), // e.g., "rules.pdf", "game_manual.pdf"
  page: z.number().optional().nullable(),
  pageNumber: z.number().optional().nullable(), // Alternative field name
  line: z.number().optional().nullable(),
  text: z.string().optional().nullable(),
  snippet: z.string().optional().nullable(), // Alternative field name
  score: z.number().optional().nullable(),
  relevanceScore: z.number().optional().nullable(), // Alternative field name
});

export type Citation = z.infer<typeof CitationSchema>;

/**
 * Citations event data
 * Sent after vector search with relevant document snippets
 */
export const StreamingCitationsSchema = z.object({
  citations: z.array(CitationSchema).optional().default([]),
  snippets: z.array(CitationSchema).optional().default([]), // Alternative field name
});

export type StreamingCitations = z.infer<typeof StreamingCitationsSchema>;

/**
 * Token event data
 * Sent for each token during LLM streaming (progressive text display)
 */
export const StreamingTokenSchema = z.object({
  token: z.string(), // Single token or word chunk
});

export type StreamingToken = z.infer<typeof StreamingTokenSchema>;

/**
 * Complete event data
 * Sent when streaming finishes successfully
 */
export const StreamingCompleteSchema = z.object({
  totalTokens: z.number(),
  promptTokens: z.number().optional().nullable(),
  completionTokens: z.number().optional().nullable(),
  confidence: z.number().optional().nullable(), // 0.0 - 1.0
  estimatedReadingTimeMinutes: z.number().optional().nullable(),
  snippets: z.array(CitationSchema).optional().default([]), // Final citations
});

export type StreamingComplete = z.infer<typeof StreamingCompleteSchema>;

/**
 * Error event data
 * Sent when an error occurs during streaming
 */
export const StreamingErrorSchema = z.object({
  message: z.string(),
  code: z.string(), // e.g., "INTERNAL_ERROR", "TIMEOUT", "UNAUTHORIZED"
});

export type StreamingError = z.infer<typeof StreamingErrorSchema>;

/**
 * Follow-up questions event data
 * Sent after Q&A completion with suggested next questions
 */
export const StreamingFollowUpQuestionsSchema = z.object({
  questions: z.array(z.string()),
});

export type StreamingFollowUpQuestions = z.infer<typeof StreamingFollowUpQuestionsSchema>;

/**
 * Setup guide step
 * Individual step in setup guide streaming
 */
export const SetupGuideStepSchema = z.object({
  title: z.string(),
  description: z.string(),
  estimatedTimeMinutes: z.number().optional().nullable(),
  order: z.number().optional().nullable(),
});

export type SetupGuideStep = z.infer<typeof SetupGuideStepSchema>;

/**
 * Setup step event data
 * Sent for each setup step during setup guide streaming
 */
export const StreamingSetupStepSchema = z.object({
  step: SetupGuideStepSchema,
});

export type StreamingSetupStep = z.infer<typeof StreamingSetupStepSchema>;

/**
 * Main RAG streaming event
 * Union type for all possible event payloads
 */
export const RagStreamingEventSchema = z.object({
  type: z.nativeEnum(StreamingEventType),
  data: z.unknown(), // Validated based on type in parser
  timestamp: z.string().datetime(),
});

export type RagStreamingEvent = z.infer<typeof RagStreamingEventSchema>;

/**
 * Typed streaming event with validated data
 * Helper type for type-safe event handling
 */
export type TypedStreamingEvent<T extends StreamingEventType> = {
  type: T;
  data: T extends StreamingEventType.StateUpdate
    ? StreamingStateUpdate
    : T extends StreamingEventType.Citations
      ? StreamingCitations
      : T extends StreamingEventType.Token
        ? StreamingToken
        : T extends StreamingEventType.Complete
          ? StreamingComplete
          : T extends StreamingEventType.Error
            ? StreamingError
            : T extends StreamingEventType.FollowUpQuestions
              ? StreamingFollowUpQuestions
              : T extends StreamingEventType.SetupStep
                ? StreamingSetupStep
                : never;
  timestamp: string;
};

/**
 * Validate and parse event data based on event type
 * Provides type-safe validation for each event type
 */
export function parseEventData(
  event: RagStreamingEvent
): TypedStreamingEvent<StreamingEventType> {
  switch (event.type) {
    case StreamingEventType.StateUpdate:
      return {
        ...event,
        data: StreamingStateUpdateSchema.parse(event.data),
      } as TypedStreamingEvent<StreamingEventType.StateUpdate>;

    case StreamingEventType.Citations:
      return {
        ...event,
        data: StreamingCitationsSchema.parse(event.data),
      } as TypedStreamingEvent<StreamingEventType.Citations>;

    case StreamingEventType.Token:
      return {
        ...event,
        data: StreamingTokenSchema.parse(event.data),
      } as TypedStreamingEvent<StreamingEventType.Token>;

    case StreamingEventType.Complete:
      return {
        ...event,
        data: StreamingCompleteSchema.parse(event.data),
      } as TypedStreamingEvent<StreamingEventType.Complete>;

    case StreamingEventType.Error:
      return {
        ...event,
        data: StreamingErrorSchema.parse(event.data),
      } as TypedStreamingEvent<StreamingEventType.Error>;

    case StreamingEventType.FollowUpQuestions:
      return {
        ...event,
        data: StreamingFollowUpQuestionsSchema.parse(event.data),
      } as TypedStreamingEvent<StreamingEventType.FollowUpQuestions>;

    case StreamingEventType.SetupStep:
      return {
        ...event,
        data: StreamingSetupStepSchema.parse(event.data),
      } as TypedStreamingEvent<StreamingEventType.SetupStep>;

    default:
      throw new Error(`Unknown event type: ${event.type}`);
  }
}
