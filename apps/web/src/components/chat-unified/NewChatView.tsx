/**
 * NewChatView — Thin wrapper around ChatEntryOrchestrator
 *
 * The original 847-line God Component has been decomposed into:
 * - GameSelector, AgentSelector, QuickStartSuggestions, ThreadCreator, ChatEntryOrchestrator
 * See: components/chat/entry/
 */

'use client';

import { ChatEntryOrchestrator } from '@/components/chat/entry';

export function NewChatView() {
  return <ChatEntryOrchestrator />;
}
