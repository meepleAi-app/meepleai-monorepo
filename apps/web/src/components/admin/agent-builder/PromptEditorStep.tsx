/**
 * ISSUE-3709: Agent Builder - Prompt Editor Step
 * Step 2: System prompt editing with Monaco editor
 */

'use client';

import { useState } from 'react';

import Editor from '@monaco-editor/react';
import { Plus, Trash2 } from 'lucide-react';

import { Label } from '@/components/ui/label';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import type { AgentForm, PromptTemplate } from '@/lib/schemas/agent-definition-schema';

interface PromptEditorStepProps {
  agent: AgentForm;
  onChange: (agent: AgentForm) => void;
}

export function PromptEditorStep({ agent, onChange }: PromptEditorStepProps) {
  const [activePromptIndex, setActivePromptIndex] = useState(0);

  const _activePrompt = agent.prompts[activePromptIndex];

  const updatePrompt = (index: number, updates: Partial<PromptTemplate>) => {
    const newPrompts = [...agent.prompts];
    newPrompts[index] = { ...newPrompts[index], ...updates };
    onChange({ ...agent, prompts: newPrompts });
  };

  const addPrompt = (role: 'system' | 'user' | 'assistant') => {
    const newPrompt: PromptTemplate = {
      role,
      content: '',
    };
    onChange({ ...agent, prompts: [...agent.prompts, newPrompt] });
    setActivePromptIndex(agent.prompts.length);
  };

  const removePrompt = (index: number) => {
    if (agent.prompts.length === 1) {
      return; // Keep at least one prompt
    }
    const newPrompts = agent.prompts.filter((_, i) => i !== index);
    onChange({ ...agent, prompts: newPrompts });
    setActivePromptIndex(Math.max(0, index - 1));
  };

  return (
    <div className="space-y-4">
      {/* Prompt List Header */}
      <div className="flex items-center justify-between">
        <Label>System Prompts ({agent.prompts.length}/20)</Label>
        <div className="flex gap-2">
          <Select onValueChange={(role) => addPrompt(role as 'system' | 'user' | 'assistant')}>
            <SelectTrigger className="w-[180px]">
              <SelectValue placeholder="Add prompt..." />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="system">
                <div className="flex items-center gap-2">
                  <Plus className="h-3 w-3" />
                  System Prompt
                </div>
              </SelectItem>
              <SelectItem value="user">
                <div className="flex items-center gap-2">
                  <Plus className="h-3 w-3" />
                  User Prompt
                </div>
              </SelectItem>
              <SelectItem value="assistant">
                <div className="flex items-center gap-2">
                  <Plus className="h-3 w-3" />
                  Assistant Prompt
                </div>
              </SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>

      {/* Prompt Tabs */}
      {agent.prompts.length > 0 && (
        <Tabs value={String(activePromptIndex)} onValueChange={(v) => setActivePromptIndex(Number(v))}>
          <TabsList className="w-full justify-start overflow-x-auto">
            {agent.prompts.map((prompt, index) => (
              <TabsTrigger key={index} value={String(index)} className="relative group">
                <span className="capitalize">{prompt.role}</span>
                {index > 0 && (
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      removePrompt(index);
                    }}
                    className="ml-2 opacity-0 group-hover:opacity-100 transition-opacity"
                    aria-label="Delete prompt"
                  >
                    <Trash2 className="h-3 w-3 text-destructive" />
                  </button>
                )}
              </TabsTrigger>
            ))}
          </TabsList>

          {agent.prompts.map((prompt, index) => (
            <TabsContent key={index} value={String(index)} className="space-y-3 mt-4">
              {/* Role Selector */}
              <div className="flex items-center gap-4">
                <Label htmlFor={`prompt-role-${index}`} className="w-16">
                  Role
                </Label>
                <Select
                  value={prompt.role}
                  onValueChange={(role: string) =>
                    updatePrompt(index, { role: role as 'system' | 'user' | 'assistant' })
                  }
                >
                  <SelectTrigger id={`prompt-role-${index}`} className="w-[200px]">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="system">System</SelectItem>
                    <SelectItem value="user">User</SelectItem>
                    <SelectItem value="assistant">Assistant</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              {/* Monaco Editor */}
              <div className="space-y-2">
                <Label htmlFor={`prompt-content-${index}`}>Prompt Content</Label>
                <div className="border rounded-md overflow-hidden">
                  <Editor
                    height="400px"
                    language="markdown"
                    theme="vs-dark"
                    value={prompt.content}
                    onChange={(value) => updatePrompt(index, { content: value || '' })}
                    options={{
                      minimap: { enabled: false },
                      fontSize: 14,
                      lineNumbers: 'on',
                      wordWrap: 'on',
                      scrollBeyondLastLine: false,
                      automaticLayout: true,
                      padding: { top: 12, bottom: 12 },
                    }}
                  />
                </div>
                <p className="text-xs text-muted-foreground">
                  {prompt.content.length.toLocaleString()} characters
                  {prompt.content.length > 8000 && (
                    <span className="text-yellow-600 ml-2">
                      ⚠ Large prompt ({prompt.content.length} chars)
                    </span>
                  )}
                </p>
              </div>
            </TabsContent>
          ))}
        </Tabs>
      )}
    </div>
  );
}
