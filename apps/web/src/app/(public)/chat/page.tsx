/**
 * General Chat Page (Task #9 - Issue #11)
 *
 * General chat page that accepts agent pre-selection via query params.
 * Accessed from:
 * - /discover → "Chat with" button → /chat?agent={agentId}
 * - Direct navigation
 *
 * Features:
 * - Agent pre-selection from query params
 * - Agent selector in header
 * - General-purpose chat (no game-specific context)
 * - Works for authenticated and guest users (guest = limited)
 */

'use client';

import { useEffect, useState } from 'react';

import { useRouter, useSearchParams } from 'next/navigation';

import { AgentChat } from '@/components/agent/AgentChat';
import type { AgentType } from '@/components/agent/AgentSelector';
import { useAuth } from '@/components/auth/AuthProvider';

// ============================================================================
// Main Component
// ============================================================================

export default function GeneralChatPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { user } = useAuth();

  // Get agent from query params (Issue #11)
  const agentFromQuery = searchParams?.get('agent') as AgentType | null;
  const [selectedAgent, setSelectedAgent] = useState<AgentType>(
    agentFromQuery || 'auto'
  );

  // Redirect unauthenticated users
  useEffect(() => {
    if (!user) {
      const currentUrl = `/chat${agentFromQuery ? `?agent=${agentFromQuery}` : ''}`;
      router.push(`/login?redirect=${encodeURIComponent(currentUrl)}`);
    }
  }, [user, router, agentFromQuery]);

  // Update selected agent when query param changes
  useEffect(() => {
    if (agentFromQuery && agentFromQuery !== selectedAgent) {
      setSelectedAgent(agentFromQuery);
    }
  }, [agentFromQuery, selectedAgent]);

  // Handle agent change
  const handleAgentChange = (newAgentId: AgentType) => {
    setSelectedAgent(newAgentId);
    // Update URL without navigation
    const newUrl = `/chat?agent=${newAgentId}`;
    window.history.replaceState(null, '', newUrl);
  };

  if (!user) {
    return null; // Will redirect in useEffect
  }

  // ============================================================================
  // Main Render
  // ============================================================================

  return (
    <div className="min-h-screen bg-[#FAF8F5] py-8 px-4">
      <div className="container mx-auto max-w-4xl">
        <AgentChat
          agentId={selectedAgent}
          layout="full-page"
          agentName="MeepleAI Assistant"
          strategy="SingleModel"
          enableAgentSelector
          onAgentChange={handleAgentChange}
        />
      </div>
    </div>
  );
}
