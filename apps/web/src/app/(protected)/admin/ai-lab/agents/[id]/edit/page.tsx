/**
 * ISSUE-3709: Agent Editor Page
 * Edit existing agent definition (reuses builder components)
 */

'use client';

import { useState, useEffect } from 'react';

import { ArrowLeft, ArrowRight, Save, Loader2 } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { AgentBuilderHeader } from '@/components/admin/agent-builder/AgentBuilderHeader';
import { AgentBuilderSteps } from '@/components/admin/agent-builder/AgentBuilderSteps';
import { AgentPreviewPanel } from '@/components/admin/agent-builder/AgentPreviewPanel';
import { BasicInfoStep } from '@/components/admin/agent-builder/BasicInfoStep';
import { PromptEditorStep } from '@/components/admin/agent-builder/PromptEditorStep';
import { ToolsStrategyStep } from '@/components/admin/agent-builder/ToolsStrategyStep';
import { Button } from '@/components/ui/button';
import { useAgentDefinition, useUpdateAgent } from '@/hooks/admin/use-agent-definitions';
import { defaultAgentForm, type AgentForm } from '@/lib/schemas/agent-definition-schema';

interface AgentEditorPageProps {
  params: { id: string };
}

export default function AgentEditorPage({ params }: AgentEditorPageProps) {
  const router = useRouter();
  const [step, setStep] = useState(1);
  const [agent, setAgent] = useState<AgentForm>(defaultAgentForm);
  const [validationErrors, setValidationErrors] = useState<string[]>([]);

  const { data: existingAgent, isLoading } = useAgentDefinition(params.id);
  const { mutate: updateAgent, isPending } = useUpdateAgent();

  // Load existing agent data when fetched
  useEffect(() => {
    if (existingAgent) {
      setAgent({
        name: existingAgent.name,
        description: existingAgent.description || '',
        type: existingAgent.type as 'RAG' | 'Citation' | 'Confidence' | 'Custom' | 'RulesInterpreter' | 'Conversation',
        model: existingAgent.model,
        maxTokens: existingAgent.maxTokens,
        temperature: existingAgent.temperature,
        strategyName: existingAgent.strategyName,
        strategyParameters: existingAgent.strategyParameters,
        prompts: existingAgent.prompts || [],
        tools: existingAgent.tools || [],
      });
    }
  }, [existingAgent]);

  const handleNext = () => {
    setStep((prev) => Math.min(prev + 1, 3)); // Edit has only 3 steps (no review)
  };

  const handlePrevious = () => {
    setStep((prev) => Math.max(prev - 1, 1));
  };

  const handleSave = () => {
    setValidationErrors([]);

    const request = {
      name: agent.name,
      description: agent.description || '',
      type: agent.type,
      model: agent.model,
      maxTokens: agent.maxTokens,
      temperature: agent.temperature,
      strategyName: agent.strategyName,
      strategyParameters: agent.strategyParameters,
      prompts: agent.prompts,
      tools: agent.tools,
    };

    updateAgent(
      { id: params.id, data: request },
      {
        onSuccess: (data) => {
          toast.success(`Agent "${data.name}" updated successfully!`);
          router.push('/admin/ai-lab/agents');
        },
        onError: (error) => {
          setValidationErrors([error.message]);
          toast.error('Failed to update agent');
        },
      }
    );
  };

  if (isLoading) {
    return (
      <div className="container max-w-7xl py-8">
        <div className="flex items-center justify-center h-[400px]">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      </div>
    );
  }

  if (!existingAgent) {
    return (
      <div className="container max-w-7xl py-8">
        <div className="text-center">
          <p className="text-muted-foreground">Agent not found</p>
          <Button variant="outline" onClick={() => router.push('/admin/ai-lab/agents')} className="mt-4">
            Back to Agents
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="container max-w-7xl py-8">
      <AgentBuilderHeader />

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        {/* Main Content */}
        <div className="lg:col-span-2">
          <AgentBuilderSteps currentStep={step} />

          {/* Step Content */}
          <div className="bg-card border rounded-lg p-6 min-h-[500px]">
            {step === 1 && <BasicInfoStep agent={agent} onChange={setAgent} />}
            {step === 2 && <PromptEditorStep agent={agent} onChange={setAgent} />}
            {step === 3 && <ToolsStrategyStep agent={agent} onChange={setAgent} />}

            {/* Validation Errors */}
            {validationErrors.length > 0 && (
              <div className="mt-4 p-4 bg-destructive/10 border border-destructive/20 rounded-md">
                <p className="text-sm font-medium text-destructive mb-2">Errors:</p>
                <ul className="list-disc list-inside space-y-1">
                  {validationErrors.map((error, index) => (
                    <li key={index} className="text-sm text-destructive">
                      {error}
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>

          {/* Navigation Buttons */}
          <div className="flex justify-between mt-6">
            <Button
              variant="outline"
              onClick={handlePrevious}
              disabled={step === 1}
            >
              <ArrowLeft className="mr-2 h-4 w-4" />
              Previous
            </Button>

            <div className="flex gap-2">
              <Button variant="ghost" onClick={() => router.push('/admin/ai-lab/agents')}>
                Cancel
              </Button>

              {step < 3 ? (
                <Button onClick={handleNext}>
                  Next
                  <ArrowRight className="ml-2 h-4 w-4" />
                </Button>
              ) : (
                <Button onClick={handleSave} disabled={isPending}>
                  {isPending ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      Saving...
                    </>
                  ) : (
                    <>
                      <Save className="mr-2 h-4 w-4" />
                      Save Changes
                    </>
                  )}
                </Button>
              )}
            </div>
          </div>
        </div>

        {/* Preview Panel */}
        <div className="lg:col-span-1">
          <div className="sticky top-4">
            <AgentPreviewPanel agent={agent} />
          </div>
        </div>
      </div>
    </div>
  );
}
