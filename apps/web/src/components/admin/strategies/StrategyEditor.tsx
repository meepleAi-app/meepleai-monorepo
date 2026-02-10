'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { Plus, Trash2 } from 'lucide-react';
import { useFieldArray, useForm } from 'react-hook-form';
import { z } from 'zod';

import { Button, Form, FormControl, FormField, FormItem, FormLabel, FormMessage, Input, Select, SelectContent, SelectItem, SelectTrigger, SelectValue, Textarea } from '@/components/ui';
import { createStrategySchema, STRATEGY_TEMPLATES, type CreateStrategy } from '@/lib/api/schemas/strategies.schemas';

interface StrategyEditorProps {
  defaultValues?: Partial<CreateStrategy>;
  onSubmit: (data: z.output<typeof createStrategySchema>) => void;
  isLoading?: boolean;
}

const STEP_TYPES = [
  { value: 'retrieval', label: 'Retrieval', description: 'Fetch relevant documents' },
  { value: 'reranking', label: 'Reranking', description: 'Re-score and prioritize results' },
  { value: 'filter', label: 'Filter', description: 'Filter by confidence score' },
  { value: 'analysis', label: 'Analysis', description: 'Analyze retrieved content' },
  { value: 'generation', label: 'Generation', description: 'Generate final response' },
];

export function StrategyEditor({ defaultValues, onSubmit, isLoading }: StrategyEditorProps) {
  const form = useForm<z.input<typeof createStrategySchema>>({
    resolver: zodResolver(createStrategySchema),
    defaultValues: defaultValues || {
      name: '',
      description: '',
      steps: [],
    },
  });

  const { fields, append, remove } = useFieldArray({
    control: form.control,
    name: 'steps',
  });

  const loadTemplate = (templateKey: keyof typeof STRATEGY_TEMPLATES) => {
    const template = STRATEGY_TEMPLATES[templateKey];
    form.setValue('name', template.name);
    form.setValue('description', template.description);
    form.setValue('steps', [...template.steps]);
  };

  const handleFormSubmit = (data: z.input<typeof createStrategySchema>) => {
    onSubmit(data as z.output<typeof createStrategySchema>);
  };

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(handleFormSubmit)} className="space-y-8">
        {/* Template Library */}
        <div className="flex gap-2">
          <span className="text-sm font-medium">Quick Templates:</span>
          {Object.keys(STRATEGY_TEMPLATES).map((key) => (
            <Button
              key={key}
              type="button"
              variant="outline"
              size="sm"
              onClick={() => loadTemplate(key as keyof typeof STRATEGY_TEMPLATES)}
            >
              {key}
            </Button>
          ))}
        </div>

        {/* Basic Info */}
        <FormField
          control={form.control}
          name="name"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Strategy Name</FormLabel>
              <FormControl>
                <Input placeholder="My Custom Strategy" {...field} />
              </FormControl>
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
                <Textarea placeholder="Describe this strategy..." {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        {/* Steps Configuration */}
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <h3 className="text-lg font-medium">Pipeline Steps</h3>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() =>
                append({ type: 'retrieval', config: {}, order: fields.length })
              }
            >
              <Plus className="h-4 w-4 mr-2" />
              Add Step
            </Button>
          </div>

          {fields.map((field, index) => (
            <div key={field.id} className="flex gap-4 items-start p-4 border rounded-lg">
              <div className="flex-1 grid grid-cols-2 gap-4">
                <FormField
                  control={form.control}
                  name={`steps.${index}.type`}
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Step Type</FormLabel>
                      <Select onValueChange={field.onChange} value={field.value}>
                        <FormControl>
                          <SelectTrigger>
                            <SelectValue />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          {STEP_TYPES.map((type) => (
                            <SelectItem key={type.value} value={type.value}>
                              {type.label}
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
                  name={`steps.${index}.order`}
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Order</FormLabel>
                      <FormControl>
                        <Input type="number" {...field} onChange={(e) => field.onChange(parseInt(e.target.value))} />
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
                onClick={() => remove(index)}
                className="mt-8"
              >
                <Trash2 className="h-4 w-4" />
              </Button>
            </div>
          ))}

          {fields.length === 0 && (
            <p className="text-sm text-muted-foreground text-center py-8">
              No steps added. Click "Add Step" or load a template.
            </p>
          )}
        </div>

        {/* Preview */}
        {fields.length > 0 && (
          <div className="bg-muted p-4 rounded-lg">
            <h4 className="text-sm font-medium mb-2">Pipeline Preview</h4>
            <div className="flex items-center gap-2">
              {fields
                .sort((a, b) => a.order - b.order)
                .map((step, i) => (
                  <div key={step.id} className="flex items-center gap-2">
                    <div className="bg-background px-3 py-1 rounded text-sm">
                      {STEP_TYPES.find((t) => t.value === step.type)?.label}
                    </div>
                    {i < fields.length - 1 && <span>→</span>}
                  </div>
                ))}
            </div>
          </div>
        )}

        {/* Submit */}
        <Button type="submit" disabled={isLoading}>
          {isLoading ? 'Saving...' : 'Save Strategy'}
        </Button>
      </form>
    </Form>
  );
}
