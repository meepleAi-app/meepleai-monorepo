'use client';

/**
 * FirstAgentStep Component
 * Issue #132 - Create AI agent for added game during onboarding
 *
 * Conditional step: only shown if user added a game in the previous step.
 * Allows creating an AI assistant for the game.
 */

import { FormEvent, useState } from 'react';

import { toast } from 'sonner';

import { AccessibleButton, AccessibleFormInput } from '@/components/accessible';
import { api } from '@/lib/api';
import { getErrorMessage } from '@/lib/utils/errorHandler';

export interface FirstAgentStepProps {
  gameId: string;
  gameName: string;
  onComplete: () => void;
  onSkip: () => void;
}

export function FirstAgentStep({ gameId, gameName, onComplete, onSkip }: FirstAgentStepProps) {
  const [agentName, setAgentName] = useState(`${gameName} Assistant`);
  const [isCreating, setIsCreating] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setErrorMessage('');

    if (!agentName.trim()) {
      setErrorMessage('Please enter a name for your agent.');
      return;
    }

    setIsCreating(true);
    try {
      await api.agents.createUserAgent({
        gameId,
        agentType: 'RuleExpert',
        name: agentName.trim(),
      });
      toast.success('AI Agent created!');
      onComplete();
    } catch (err) {
      setErrorMessage(getErrorMessage(err, 'Failed to create agent.'));
    } finally {
      setIsCreating(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold text-slate-900">
          Create Your AI Assistant
        </h2>
        <p className="mt-1 text-sm text-slate-600">
          Set up an AI agent for <strong>{gameName}</strong> to help you with rules and strategies.
        </p>
      </div>

      {errorMessage && (
        <div
          role="alert"
          aria-live="polite"
          className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
        >
          {errorMessage}
        </div>
      )}

      <form noValidate onSubmit={handleSubmit} className="space-y-4">
        <AccessibleFormInput
          label="Agent Name"
          type="text"
          value={agentName}
          onChange={e => setAgentName(e.target.value)}
          placeholder="e.g., Catan Helper"
          hint="Give your AI assistant a friendly name"
        />

        <div className="rounded-lg bg-amber-50 border border-amber-200 p-4">
          <h3 className="text-sm font-medium text-amber-900">What can your agent do?</h3>
          <ul className="mt-2 space-y-1 text-sm text-amber-800">
            <li>Answer rules questions about {gameName}</li>
            <li>Help with strategy tips and suggestions</li>
            <li>Track game state during sessions</li>
          </ul>
        </div>

        <div className="flex items-center gap-3 pt-2">
          <AccessibleButton
            type="submit"
            variant="primary"
            className="flex-1"
            isLoading={isCreating}
            loadingText="Creating..."
          >
            Create Agent
          </AccessibleButton>
          <button
            type="button"
            onClick={onSkip}
            className="text-sm text-slate-500 hover:text-slate-700"
            data-testid="agent-skip"
          >
            Skip for now
          </button>
        </div>
      </form>
    </div>
  );
}
