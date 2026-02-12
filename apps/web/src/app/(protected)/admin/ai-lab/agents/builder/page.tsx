/**
 * ISSUE-3709: Agent Builder Page
 * Multi-step wizard for creating AI agent definitions
 */

'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { ArrowLeft, ArrowRight } from 'lucide-react';

import { AgentBuilderHeader } from '@/components/admin/agent-builder/AgentBuilderHeader';
import { AgentBuilderSteps } from '@/components/admin/agent-builder/AgentBuilderSteps';
import { BasicInfoStep } from '@/components/admin/agent-builder/BasicInfoStep';
import { PromptEditorStep } from '@/components/admin/agent-builder/PromptEditorStep';
import { ToolsStrategyStep } from '@/components/admin/agent-builder/ToolsStrategyStep';
import { ReviewStep } from '@/components/admin/agent-builder/ReviewStep';
import { AgentPreviewPanel } from '@/components/admin/agent-builder/AgentPreviewPanel';
import { Button } from '@/components/ui/button';
import { useCreateAgent } from '@/hooks/admin/use-agent-definitions';
import { defaultAgentForm, AgentFormSchema, type AgentForm } from '@/lib/schemas/agent-definition-schema';
import { toast } from 'sonner';

export default function AgentBuilderPage() {
  const router = useRouter();
  const [step, setStep] = useState(1);
  const [agent, setAgent] = useState<AgentForm>(defaultAgentForm);
  const [validationErrors, setValidationErrors] = useState<string[]>([]);

  const { mutate: createAgent, isPending } = useCreateAgent();

  // Validate current step
  const validateStep = (stepNumber: number): boolean => {
    setValidationErrors([]);

    try {
      if (stepNumber === 1) {
        // Basic info validation
        if (!agent.name.trim()) {
          setValidationErrors(['Name is required']);
          return false;
        }
        if (agent.name.length > 100) {
          setValidationErrors(['Name must be 100 characters or less']);
          return false;
        }
      } else if (stepNumber === 2) {
        // Prompt validation
        if (agent.prompts.length === 0) {
          setValidationErrors(['At least one prompt is required']);
          return false;
        }
        for (const prompt of agent.prompts) {
          if (!prompt.content.trim()) {
            setValidationErrors([`${prompt.role} prompt content cannot be empty`]);
            return false;
          }
        }
      } else if (stepNumber === 3) {
        // Tools & strategy validation (optional, so always valid)
        return true;
      }

      return true;
    } catch (error) {
      setValidationErrors([error instanceof Error ? error.message : 'Validation failed']);
      return false;
    }
  };

  const handleNext = () => {
    if (validateStep(step)) {
      setStep((prev) => Math.min(prev + 1, 4));
    }
  };

  const handlePrevious = () => {
    setStep((prev) => Math.max(prev - 1, 1));
  };

  const handleSubmit = () => {
    // Final validation
    const validation = AgentFormSchema.safeParse(agent);

    if (!validation.success) {
      const errors = validation.error.issues.map((e) => `${e.path.join('.')}: ${e.message}`);
      setValidationErrors(errors);
      toast.error(`Validation failed: ${errors[0]}`);
      return;
    }

    // Transform form to API request
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

    createAgent(request, {
      onSuccess: (data) => {
        toast.success(`Agent "${data.name}" created successfully!`);
        router.push('/admin/ai-lab/agents');
      },
      onError: (error) => {
        setValidationErrors([error.message]);
      },
    });
  };

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
            {step === 4 && (
              <ReviewStep
                agent={agent}
                onSubmit={handleSubmit}
                isSubmitting={isPending}
              />
            )}

            {/* Validation Errors */}
            {validationErrors.length > 0 && step !== 4 && (
              <div className="mt-4 p-4 bg-destructive/10 border border-destructive/20 rounded-md">
                <p className="text-sm font-medium text-destructive mb-2">Validation Errors:</p>
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
          {step < 4 && (
            <div className="flex justify-between mt-6">
              <Button
                variant="outline"
                onClick={handlePrevious}
                disabled={step === 1}
              >
                <ArrowLeft className="mr-2 h-4 w-4" />
                Previous
              </Button>

              <Button onClick={handleNext}>
                Next
                <ArrowRight className="ml-2 h-4 w-4" />
              </Button>
            </div>
          )}

          {step === 4 && (
            <div className="flex justify-between mt-6">
              <Button
                variant="outline"
                onClick={handlePrevious}
              >
                <ArrowLeft className="mr-2 h-4 w-4" />
                Previous
              </Button>

              <Button variant="ghost" onClick={() => router.push('/admin/ai-lab/agents')}>
                Cancel
              </Button>
            </div>
          )}
        </div>

        {/* Preview Panel (Sidebar) */}
        <div className="lg:col-span-1">
          <div className="sticky top-4">
            <AgentPreviewPanel agent={agent} />
          </div>
        </div>
      </div>
    </div>
  );
}
