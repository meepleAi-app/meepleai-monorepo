'use client';

import { useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Edit2, Plus, Trash2, Play } from 'lucide-react';
import { toast } from 'sonner';

import {
  Button, Card, CardContent, CardHeader, CardTitle, Input, Textarea,
  ScrollArea, Badge,
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from '@/components/ui';
import { playgroundScenariosApi } from '@/lib/api/playground-scenarios.api';
import type {
  PlaygroundTestScenarioDto,
  CreatePlaygroundTestScenario,
  UpdatePlaygroundTestScenario,
} from '@/lib/api/schemas/playground-scenarios.schemas';

interface ScenarioManagerProps {
  agentDefinitionId: string;
  onRunScenario: (scenario: PlaygroundTestScenarioDto) => void;
}

interface ScenarioFormData {
  name: string;
  description: string;
  userMessage: string;
  systemMessage: string;
  expectedTopics: string;
  tags: string;
}

const emptyForm: ScenarioFormData = {
  name: '',
  description: '',
  userMessage: '',
  systemMessage: '',
  expectedTopics: '',
  tags: '',
};

function formToCreate(form: ScenarioFormData, agentDefinitionId: string): CreatePlaygroundTestScenario {
  return {
    name: form.name.trim(),
    description: form.description.trim(),
    agentDefinitionId,
    userMessage: form.userMessage.trim(),
    systemMessage: form.systemMessage.trim() || undefined,
    expectedTopics: form.expectedTopics.trim()
      ? form.expectedTopics.split(',').map((s) => s.trim()).filter(Boolean)
      : undefined,
    tags: form.tags.trim()
      ? form.tags.split(',').map((s) => s.trim()).filter(Boolean)
      : undefined,
  };
}

function formToUpdate(form: ScenarioFormData): UpdatePlaygroundTestScenario {
  return {
    name: form.name.trim(),
    description: form.description.trim(),
    userMessage: form.userMessage.trim(),
    systemMessage: form.systemMessage.trim() || undefined,
    expectedTopics: form.expectedTopics.trim()
      ? form.expectedTopics.split(',').map((s) => s.trim()).filter(Boolean)
      : undefined,
    tags: form.tags.trim()
      ? form.tags.split(',').map((s) => s.trim()).filter(Boolean)
      : undefined,
  };
}

function scenarioToForm(scenario: PlaygroundTestScenarioDto): ScenarioFormData {
  return {
    name: scenario.name,
    description: scenario.description,
    userMessage: scenario.userMessage,
    systemMessage: scenario.systemMessage ?? '',
    expectedTopics: scenario.expectedTopics.join(', '),
    tags: scenario.tags.join(', '),
  };
}

export function ScenarioManager({ agentDefinitionId, onRunScenario }: ScenarioManagerProps) {
  const queryClient = useQueryClient();
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<ScenarioFormData>(emptyForm);

  const { data: scenarios = [], isLoading } = useQuery({
    queryKey: ['playground-scenarios', agentDefinitionId],
    queryFn: () => playgroundScenariosApi.getAll({ agentDefinitionId }),
    enabled: !!agentDefinitionId,
  });

  const createMutation = useMutation({
    mutationFn: (data: CreatePlaygroundTestScenario) => playgroundScenariosApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['playground-scenarios'] });
      toast.success('Scenario created');
      closeDialog();
    },
    onError: () => toast.error('Failed to create scenario'),
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdatePlaygroundTestScenario }) =>
      playgroundScenariosApi.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['playground-scenarios'] });
      toast.success('Scenario updated');
      closeDialog();
    },
    onError: () => toast.error('Failed to update scenario'),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => playgroundScenariosApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['playground-scenarios'] });
      toast.success('Scenario deleted');
    },
    onError: () => toast.error('Failed to delete scenario'),
  });

  const closeDialog = () => {
    setIsDialogOpen(false);
    setEditingId(null);
    setForm(emptyForm);
  };

  const openCreate = () => {
    setEditingId(null);
    setForm(emptyForm);
    setIsDialogOpen(true);
  };

  const openEdit = (scenario: PlaygroundTestScenarioDto) => {
    setEditingId(scenario.id);
    setForm(scenarioToForm(scenario));
    setIsDialogOpen(true);
  };

  const handleSave = () => {
    if (!form.name.trim() || !form.userMessage.trim()) {
      toast.error('Name and user message are required');
      return;
    }

    if (editingId) {
      updateMutation.mutate({ id: editingId, data: formToUpdate(form) });
    } else {
      createMutation.mutate(formToCreate(form, agentDefinitionId));
    }
  };

  const isSaving = createMutation.isPending || updateMutation.isPending;

  if (!agentDefinitionId) {
    return (
      <div className="text-sm text-muted-foreground text-center py-8">
        Select an agent to manage test scenarios
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-lg font-semibold">Test Scenarios</h3>
        <Button size="sm" onClick={openCreate}>
          <Plus className="h-4 w-4 mr-1" />
          New
        </Button>
      </div>

      {isLoading && (
        <div className="text-sm text-muted-foreground text-center py-4">Loading scenarios...</div>
      )}

      {!isLoading && scenarios.length === 0 && (
        <div className="text-sm text-muted-foreground text-center py-8">
          No scenarios yet. Create one to get started.
        </div>
      )}

      <ScrollArea className="max-h-[500px]">
        <div className="space-y-3">
          {scenarios.map((scenario) => (
            <Card key={scenario.id}>
              <CardHeader className="pb-2">
                <div className="flex items-center justify-between">
                  <CardTitle className="text-sm">{scenario.name}</CardTitle>
                  <div className="flex gap-1">
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-7 w-7"
                      onClick={() => onRunScenario(scenario)}
                      title="Run scenario"
                    >
                      <Play className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-7 w-7"
                      onClick={() => openEdit(scenario)}
                      title="Edit scenario"
                    >
                      <Edit2 className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-7 w-7 text-destructive"
                      onClick={() => deleteMutation.mutate(scenario.id)}
                      title="Delete scenario"
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="pt-0">
                <p className="text-xs text-muted-foreground mb-2">{scenario.description}</p>
                <div className="text-xs bg-muted rounded p-2 mb-2 line-clamp-2">
                  {scenario.userMessage}
                </div>
                {scenario.tags.length > 0 && (
                  <div className="flex flex-wrap gap-1">
                    {scenario.tags.map((tag) => (
                      <Badge key={tag} variant="outline" className="text-xs py-0 px-1.5">
                        {tag}
                      </Badge>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>
          ))}
        </div>
      </ScrollArea>

      {/* Create/Edit Dialog */}
      <Dialog open={isDialogOpen} onOpenChange={(open) => !open && closeDialog()}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{editingId ? 'Edit Scenario' : 'New Scenario'}</DialogTitle>
          </DialogHeader>

          <div className="space-y-4">
            <div>
              <label className="text-sm font-medium mb-1 block">Name *</label>
              <Input
                value={form.name}
                onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                placeholder="e.g. Catan setup rules"
              />
            </div>

            <div>
              <label className="text-sm font-medium mb-1 block">Description</label>
              <Input
                value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
                placeholder="Brief description of what this tests"
              />
            </div>

            <div>
              <label className="text-sm font-medium mb-1 block">User Message *</label>
              <Textarea
                value={form.userMessage}
                onChange={(e) => setForm((f) => ({ ...f, userMessage: e.target.value }))}
                placeholder="The message to send to the agent"
                rows={3}
              />
            </div>

            <div>
              <label className="text-sm font-medium mb-1 block">System Message Override</label>
              <Textarea
                value={form.systemMessage}
                onChange={(e) => setForm((f) => ({ ...f, systemMessage: e.target.value }))}
                placeholder="Optional system message override"
                rows={2}
              />
            </div>

            <div>
              <label className="text-sm font-medium mb-1 block">Expected Topics</label>
              <Input
                value={form.expectedTopics}
                onChange={(e) => setForm((f) => ({ ...f, expectedTopics: e.target.value }))}
                placeholder="Comma-separated: setup, board, pieces"
              />
            </div>

            <div>
              <label className="text-sm font-medium mb-1 block">Tags</label>
              <Input
                value={form.tags}
                onChange={(e) => setForm((f) => ({ ...f, tags: e.target.value }))}
                placeholder="Comma-separated: rules, beginner, catan"
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={closeDialog}>
              Cancel
            </Button>
            <Button onClick={handleSave} disabled={isSaving}>
              {isSaving ? 'Saving...' : editingId ? 'Update' : 'Create'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
