'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { Plus, Trash2 } from 'lucide-react';
import { useFieldArray, useForm } from 'react-hook-form';
import { z } from 'zod';

import {
  Button,
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
  Input,
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
  Slider,
  Textarea,
} from '@/components/ui';
import {
  createAgentDefinitionSchema,
  type CreateAgentDefinition,
} from '@/lib/api/schemas/agent-definitions.schemas';

interface AgentBuilderFormProps {
  defaultValues?: Partial<CreateAgentDefinition>;
  onSubmit: (data: z.output<typeof createAgentDefinitionSchema>) => void;
  isLoading?: boolean;
  disabled?: boolean;
}

const CHAT_LANGUAGES = [
  { value: 'auto', label: 'Auto (from PDF)' },
  { value: 'en', label: 'English' },
  { value: 'it', label: 'Italiano' },
  { value: 'de', label: 'Deutsch' },
  { value: 'fr', label: 'Français' },
  { value: 'es', label: 'Español' },
  { value: 'pt', label: 'Português' },
  { value: 'nl', label: 'Nederlands' },
  { value: 'pl', label: 'Polski' },
  { value: 'ru', label: 'Русский' },
  { value: 'ja', label: '日本語' },
  { value: 'zh', label: '中文' },
  { value: 'ko', label: '한국어' },
];

const MODELS = [
  { value: 'gpt-4', label: 'GPT-4' },
  { value: 'gpt-4-turbo', label: 'GPT-4 Turbo' },
  { value: 'claude-3-opus', label: 'Claude 3 Opus' },
  { value: 'claude-3-sonnet', label: 'Claude 3 Sonnet' },
  { value: 'deepseek-chat', label: 'DeepSeek Chat' },
];

export function AgentBuilderForm({
  defaultValues,
  onSubmit,
  isLoading,
  disabled,
}: AgentBuilderFormProps) {
  const form = useForm<z.input<typeof createAgentDefinitionSchema>>({
    resolver: zodResolver(createAgentDefinitionSchema),
    defaultValues: defaultValues || {
      name: '',
      description: '',
      chatLanguage: 'auto',
      model: 'gpt-4',
      maxTokens: 2048,
      temperature: 0.7,
      prompts: [],
      tools: [],
    },
  });

  const {
    fields: promptFields,
    append: appendPrompt,
    remove: removePrompt,
  } = useFieldArray({
    control: form.control,
    name: 'prompts',
  });

  const {
    fields: toolFields,
    append: appendTool,
    remove: removeTool,
  } = useFieldArray({
    control: form.control,
    name: 'tools',
  });

  const handleFormSubmit = (data: z.input<typeof createAgentDefinitionSchema>) => {
    onSubmit(data as z.output<typeof createAgentDefinitionSchema>);
  };

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(handleFormSubmit)} className="space-y-8">
        {/* Basic Info */}
        <div className="space-y-4">
          <FormField
            control={form.control}
            name="name"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Agent Name</FormLabel>
                <FormControl>
                  <Input placeholder="My Custom Agent" {...field} />
                </FormControl>
                <FormDescription>Unique name for your agent (3-100 characters)</FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="description"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Description</FormLabel>
                <FormControl>
                  <Textarea placeholder="Describe what this agent does..." {...field} />
                </FormControl>
                <FormDescription>Optional description (max 1000 characters)</FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="chatLanguage"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Chat Language</FormLabel>
                <Select onValueChange={field.onChange} defaultValue={field.value || 'auto'}>
                  <FormControl>
                    <SelectTrigger>
                      <SelectValue placeholder="Select language" />
                    </SelectTrigger>
                  </FormControl>
                  <SelectContent>
                    {CHAT_LANGUAGES.map(lang => (
                      <SelectItem key={lang.value} value={lang.value}>
                        {lang.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <FormDescription>
                  Language for agent responses. &quot;Auto&quot; detects from PDF content.
                </FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />
        </div>

        {/* Model Configuration */}
        <div className="space-y-4">
          <h3 className="text-lg font-medium">Model Configuration</h3>

          <FormField
            control={form.control}
            name="model"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Model</FormLabel>
                <Select onValueChange={field.onChange} defaultValue={field.value}>
                  <FormControl>
                    <SelectTrigger>
                      <SelectValue placeholder="Select a model" />
                    </SelectTrigger>
                  </FormControl>
                  <SelectContent>
                    {MODELS.map(model => (
                      <SelectItem key={model.value} value={model.value}>
                        {model.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="maxTokens"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Max Tokens: {field.value}</FormLabel>
                <FormControl>
                  <Slider
                    min={100}
                    max={32000}
                    step={100}
                    value={[field.value]}
                    onValueChange={([value]) => field.onChange(value)}
                  />
                </FormControl>
                <FormDescription>Maximum tokens for generation (100-32000)</FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="temperature"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Temperature: {field.value.toFixed(2)}</FormLabel>
                <FormControl>
                  <Slider
                    min={0}
                    max={2}
                    step={0.1}
                    value={[field.value]}
                    onValueChange={([value]) => field.onChange(value)}
                  />
                </FormControl>
                <FormDescription>Randomness in responses (0.0-2.0)</FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />
        </div>

        {/* Prompt Templates */}
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <h3 className="text-lg font-medium">Prompt Templates</h3>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => appendPrompt({ role: 'system', content: '' })}
            >
              <Plus className="h-4 w-4 mr-2" />
              Add Prompt
            </Button>
          </div>

          {promptFields.map((field, index) => (
            <div key={field.id} className="flex gap-4 items-start p-4 border rounded-lg">
              <div className="flex-1 space-y-4">
                <FormField
                  control={form.control}
                  name={`prompts.${index}.role`}
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Role</FormLabel>
                      <Select onValueChange={field.onChange} defaultValue={field.value}>
                        <FormControl>
                          <SelectTrigger>
                            <SelectValue />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          <SelectItem value="system">System</SelectItem>
                          <SelectItem value="user">User</SelectItem>
                          <SelectItem value="assistant">Assistant</SelectItem>
                          <SelectItem value="function">Function</SelectItem>
                        </SelectContent>
                      </Select>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <FormField
                  control={form.control}
                  name={`prompts.${index}.content`}
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Content</FormLabel>
                      <FormControl>
                        <Textarea placeholder="Enter prompt content..." {...field} rows={4} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>

              <Button
                type="button"
                variant="ghost"
                size="icon"
                onClick={() => removePrompt(index)}
                className="mt-8"
              >
                <Trash2 className="h-4 w-4" />
              </Button>
            </div>
          ))}

          {promptFields.length === 0 && (
            <p className="text-sm text-muted-foreground text-center py-8">
              No prompts added. Click "Add Prompt" to create one.
            </p>
          )}
        </div>

        {/* Tool Configuration */}
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <h3 className="text-lg font-medium">Tool Configuration</h3>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => appendTool({ name: '', settings: {} })}
            >
              <Plus className="h-4 w-4 mr-2" />
              Add Tool
            </Button>
          </div>

          {toolFields.map((field, index) => (
            <div key={field.id} className="flex gap-4 items-start p-4 border rounded-lg">
              <div className="flex-1">
                <FormField
                  control={form.control}
                  name={`tools.${index}.name`}
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Tool Name</FormLabel>
                      <FormControl>
                        <Input placeholder="web_search, calculator, etc." {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>

              <Button
                type="button"
                variant="ghost"
                size="icon"
                onClick={() => removeTool(index)}
                className="mt-8"
              >
                <Trash2 className="h-4 w-4" />
              </Button>
            </div>
          ))}

          {toolFields.length === 0 && (
            <p className="text-sm text-muted-foreground text-center py-8">
              No tools configured. Click "Add Tool" to add one.
            </p>
          )}
        </div>

        {/* Submit */}
        <div className="flex justify-end gap-4">
          <Button type="submit" disabled={isLoading || disabled}>
            {isLoading ? 'Saving...' : 'Save Agent'}
          </Button>
        </div>
      </form>
    </Form>
  );
}
