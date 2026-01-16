/**
 * AgentConfigPanel Component - Issue #2414 (Sub-Issue 2398.2)
 *
 * Dynamic configuration panel for agent mode parameters.
 * Adapts form fields based on selected mode (Chat, Player, Ledger).
 *
 * Features:
 * - Dynamic form based on selected mode
 * - React Hook Form + Zod validation
 * - Confidence threshold slider (0.0-1.0)
 * - Advanced options toggle
 * - Local storage persistence
 * - WCAG 2.1 AA compliant
 *
 * @example
 * ```tsx
 * <AgentConfigPanel
 *   mode="Chat"
 *   config={config}
 *   onConfigChange={setConfig}
 * />
 * ```
 */

'use client';

import { useState, useEffect } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import { Settings2, ChevronDown, ChevronUp, Info } from 'lucide-react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { Slider } from '@/components/ui/slider';
import { Switch } from '@/components/ui/switch';
import { cn } from '@/lib/utils';

// ============================================================================
// Types & Schemas
// ============================================================================

/**
 * Agent mode type
 */
export type AgentMode = 'Chat' | 'Player' | 'Ledger';

/**
 * Base configuration shared by all modes
 */
const baseConfigSchema = z.object({
  confidenceThreshold: z
    .number()
    .min(0.0, 'Confidence must be at least 0.0')
    .max(1.0, 'Confidence must be at most 1.0')
    .default(0.7),
  temperature: z
    .number()
    .min(0.0, 'Temperature must be at least 0.0')
    .max(2.0, 'Temperature must be at most 2.0')
    .default(0.7),
  maxTokens: z
    .number()
    .int('Max tokens must be an integer')
    .min(100, 'Max tokens must be at least 100')
    .max(8192, 'Max tokens must be at most 8192')
    .default(2048),
  useContextWindow: z.boolean().default(true),
});

/**
 * Chat mode specific configuration
 */
const chatConfigSchema = baseConfigSchema.extend({
  mode: z.literal('Chat'),
  enableRuleLookup: z.boolean().default(true),
  maxHistoryMessages: z.number().int().min(1).max(50).default(10),
});

/**
 * Player mode specific configuration
 */
const playerConfigSchema = baseConfigSchema.extend({
  mode: z.literal('Player'),
  suggestionDepth: z.number().int().min(1).max(10).default(3),
  evaluateRisks: z.boolean().default(true),
  showAlternatives: z.boolean().default(false),
});

/**
 * Ledger mode specific configuration
 */
const ledgerConfigSchema = baseConfigSchema.extend({
  mode: z.literal('Ledger'),
  trackPlayerActions: z.boolean().default(true),
  trackResourceChanges: z.boolean().default(true),
  enableTimeTravel: z.boolean().default(false),
});

/**
 * Discriminated union of all config types
 */
export const agentConfigSchema = z.discriminatedUnion('mode', [
  chatConfigSchema,
  playerConfigSchema,
  ledgerConfigSchema,
]);

export type AgentConfig = z.infer<typeof agentConfigSchema>;

/**
 * Extract base config type (common fields)
 */
export type BaseConfig = z.infer<typeof baseConfigSchema>;

// ============================================================================
// Local Storage Hook
// ============================================================================

const STORAGE_KEY_PREFIX = 'meepleai_agent_config_';

function useAgentConfigStorage(mode: AgentMode) {
  const storageKey = `${STORAGE_KEY_PREFIX}${mode.toLowerCase()}`;

  const loadConfig = (): AgentConfig | null => {
    if (typeof window === 'undefined') return null;

    try {
      const stored = localStorage.getItem(storageKey);
      if (!stored) return null;

      const parsed = JSON.parse(stored);
      const validated = agentConfigSchema.parse(parsed);
      return validated;
    } catch (error) {
      console.error(`Failed to load config for ${mode}:`, error);
      return null;
    }
  };

  const saveConfig = (config: AgentConfig) => {
    if (typeof window === 'undefined') return;

    try {
      localStorage.setItem(storageKey, JSON.stringify(config));
    } catch (error) {
      console.error(`Failed to save config for ${mode}:`, error);
    }
  };

  return { loadConfig, saveConfig };
}

// ============================================================================
// Default Configurations
// ============================================================================

const DEFAULT_CONFIGS: Record<AgentMode, AgentConfig> = {
  Chat: {
    mode: 'Chat',
    confidenceThreshold: 0.7,
    temperature: 0.7,
    maxTokens: 2048,
    useContextWindow: true,
    enableRuleLookup: true,
    maxHistoryMessages: 10,
  },
  Player: {
    mode: 'Player',
    confidenceThreshold: 0.8,
    temperature: 0.5,
    maxTokens: 1024,
    useContextWindow: true,
    suggestionDepth: 3,
    evaluateRisks: true,
    showAlternatives: false,
  },
  Ledger: {
    mode: 'Ledger',
    confidenceThreshold: 0.9,
    temperature: 0.3,
    maxTokens: 4096,
    useContextWindow: true,
    trackPlayerActions: true,
    trackResourceChanges: true,
    enableTimeTravel: false,
  },
};

// ============================================================================
// Component Props
// ============================================================================

export interface AgentConfigPanelProps {
  /**
   * Current agent mode (determines available config options)
   */
  mode: AgentMode;

  /**
   * Current configuration
   */
  config?: AgentConfig;

  /**
   * Callback when configuration changes
   */
  onConfigChange: (config: AgentConfig) => void;

  /**
   * Whether the form is disabled
   */
  disabled?: boolean;

  /**
   * Optional CSS classes
   */
  className?: string;
}

// ============================================================================
// Main Component
// ============================================================================

export function AgentConfigPanel({
  mode,
  config,
  onConfigChange,
  disabled = false,
  className = '',
}: AgentConfigPanelProps) {
  const [isAdvancedOpen, setIsAdvancedOpen] = useState(false);
  const { loadConfig, saveConfig } = useAgentConfigStorage(mode);

  // Get schema for current mode
  const getModeSchema = () => {
    switch (mode) {
      case 'Chat':
        return chatConfigSchema;
      case 'Player':
        return playerConfigSchema;
      case 'Ledger':
        return ledgerConfigSchema;
    }
  };

  // Initialize form with default or provided config
  const form = useForm<AgentConfig>({
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    resolver: zodResolver(getModeSchema()) as any, // Type assertion for discriminated union
    defaultValues: config || DEFAULT_CONFIGS[mode],
  });

  // Load config from localStorage on mount or mode change
  useEffect(() => {
    const storedConfig = loadConfig();
    if (storedConfig && storedConfig.mode === mode) {
      form.reset(storedConfig);
      onConfigChange(storedConfig);
    } else if (config && config.mode === mode) {
      // Use provided config if it matches the mode
      form.reset(config);
      onConfigChange(config);
    } else {
      // Fallback to default config
      const defaultConfig = DEFAULT_CONFIGS[mode];
      form.reset(defaultConfig);
      onConfigChange(defaultConfig);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- Intentionally only re-run when mode changes to avoid infinite loops
  }, [mode]);

  // Watch form changes and propagate
  useEffect(() => {
    const subscription = form.watch(values => {
      const validated = getModeSchema().safeParse(values);
      if (validated.success) {
        onConfigChange(validated.data);
        saveConfig(validated.data);
      }
    });

    return () => subscription.unsubscribe();
    // eslint-disable-next-line react-hooks/exhaustive-deps -- Functions are stable, only re-run on form/mode change
  }, [form, mode]);

  // Render mode-specific fields
  const renderModeSpecificFields = () => {
    switch (mode) {
      case 'Chat':
        return (
          <>
            <FormField
              control={form.control}
              name="enableRuleLookup"
              render={({ field }) => (
                <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                  <div className="space-y-0.5">
                    <FormLabel>Enable Rule Lookup</FormLabel>
                    <FormDescription className="text-xs">
                      Search game rules database for accurate answers
                    </FormDescription>
                  </div>
                  <FormControl>
                    <Switch
                      checked={field.value}
                      onCheckedChange={field.onChange}
                      disabled={disabled}
                    />
                  </FormControl>
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="maxHistoryMessages"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Max History Messages</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      {...field}
                      onChange={e => field.onChange(parseInt(e.target.value, 10))}
                      min={1}
                      max={50}
                      disabled={disabled}
                      className="h-9"
                    />
                  </FormControl>
                  <FormDescription className="text-xs">
                    Number of previous messages to include in context (1-50)
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </>
        );

      case 'Player':
        return (
          <>
            <FormField
              control={form.control}
              name="suggestionDepth"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Suggestion Depth</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      {...field}
                      onChange={e => field.onChange(parseInt(e.target.value, 10))}
                      min={1}
                      max={10}
                      disabled={disabled}
                      className="h-9"
                    />
                  </FormControl>
                  <FormDescription className="text-xs">
                    Number of moves to analyze ahead (1-10)
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="evaluateRisks"
              render={({ field }) => (
                <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                  <div className="space-y-0.5">
                    <FormLabel>Evaluate Risks</FormLabel>
                    <FormDescription className="text-xs">
                      Include risk assessment in move suggestions
                    </FormDescription>
                  </div>
                  <FormControl>
                    <Switch
                      checked={field.value}
                      onCheckedChange={field.onChange}
                      disabled={disabled}
                    />
                  </FormControl>
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="showAlternatives"
              render={({ field }) => (
                <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                  <div className="space-y-0.5">
                    <FormLabel>Show Alternatives</FormLabel>
                    <FormDescription className="text-xs">
                      Display alternative move options with reasoning
                    </FormDescription>
                  </div>
                  <FormControl>
                    <Switch
                      checked={field.value}
                      onCheckedChange={field.onChange}
                      disabled={disabled}
                    />
                  </FormControl>
                </FormItem>
              )}
            />
          </>
        );

      case 'Ledger':
        return (
          <>
            <FormField
              control={form.control}
              name="trackPlayerActions"
              render={({ field }) => (
                <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                  <div className="space-y-0.5">
                    <FormLabel>Track Player Actions</FormLabel>
                    <FormDescription className="text-xs">
                      Record all player moves and decisions
                    </FormDescription>
                  </div>
                  <FormControl>
                    <Switch
                      checked={field.value}
                      onCheckedChange={field.onChange}
                      disabled={disabled}
                    />
                  </FormControl>
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="trackResourceChanges"
              render={({ field }) => (
                <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                  <div className="space-y-0.5">
                    <FormLabel>Track Resource Changes</FormLabel>
                    <FormDescription className="text-xs">
                      Monitor resource flow and inventory changes
                    </FormDescription>
                  </div>
                  <FormControl>
                    <Switch
                      checked={field.value}
                      onCheckedChange={field.onChange}
                      disabled={disabled}
                    />
                  </FormControl>
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="enableTimeTravel"
              render={({ field }) => (
                <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                  <div className="space-y-0.5">
                    <FormLabel>Enable Time Travel</FormLabel>
                    <FormDescription className="text-xs">
                      Allow rewinding to previous game states (experimental)
                    </FormDescription>
                  </div>
                  <FormControl>
                    <Switch
                      checked={field.value}
                      onCheckedChange={field.onChange}
                      disabled={disabled}
                    />
                  </FormControl>
                </FormItem>
              )}
            />
          </>
        );
    }
  };

  return (
    <Card className={cn('w-full', className)}>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Settings2 className="h-4 w-4" />
          Configuration
        </CardTitle>
        <CardDescription className="text-xs">Adjust parameters for {mode} mode</CardDescription>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          <form className="space-y-6">
            {/* Confidence Threshold Slider */}
            <FormField
              control={form.control}
              name="confidenceThreshold"
              render={({ field }) => (
                <FormItem>
                  <div className="flex items-center justify-between">
                    <FormLabel>Confidence Threshold</FormLabel>
                    <span className="text-sm font-medium text-muted-foreground">
                      {field.value.toFixed(2)}
                    </span>
                  </div>
                  <FormControl>
                    <Slider
                      min={0}
                      max={1}
                      step={0.05}
                      value={[field.value]}
                      onValueChange={values => field.onChange(values[0])}
                      disabled={disabled}
                      className="py-4"
                    />
                  </FormControl>
                  <FormDescription className="text-xs flex items-start gap-1">
                    <Info className="h-3 w-3 mt-0.5 flex-shrink-0" />
                    <span>
                      Minimum confidence score required for agent responses. Higher values produce
                      more conservative answers.
                    </span>
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            {/* Mode-Specific Fields */}
            <div className="space-y-4">{renderModeSpecificFields()}</div>

            {/* Advanced Options Collapsible */}
            <Collapsible open={isAdvancedOpen} onOpenChange={setIsAdvancedOpen}>
              <CollapsibleTrigger asChild>
                <Button
                  variant="ghost"
                  size="sm"
                  className="w-full flex items-center justify-between"
                  disabled={disabled}
                  type="button"
                >
                  <span className="text-xs font-medium">Advanced Options</span>
                  {isAdvancedOpen ? (
                    <ChevronUp className="h-4 w-4" />
                  ) : (
                    <ChevronDown className="h-4 w-4" />
                  )}
                </Button>
              </CollapsibleTrigger>
              <CollapsibleContent className="space-y-4 pt-4">
                {/* Temperature */}
                <FormField
                  control={form.control}
                  name="temperature"
                  render={({ field }) => (
                    <FormItem>
                      <div className="flex items-center justify-between">
                        <FormLabel>Temperature</FormLabel>
                        <span className="text-sm font-medium text-muted-foreground">
                          {field.value.toFixed(2)}
                        </span>
                      </div>
                      <FormControl>
                        <Slider
                          min={0}
                          max={2}
                          step={0.1}
                          value={[field.value]}
                          onValueChange={values => field.onChange(values[0])}
                          disabled={disabled}
                          className="py-4"
                        />
                      </FormControl>
                      <FormDescription className="text-xs">
                        Controls randomness: 0.0 = deterministic, 2.0 = very creative
                      </FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                {/* Max Tokens */}
                <FormField
                  control={form.control}
                  name="maxTokens"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Max Tokens</FormLabel>
                      <FormControl>
                        <Input
                          type="number"
                          {...field}
                          onChange={e => field.onChange(parseInt(e.target.value, 10))}
                          min={100}
                          max={8192}
                          disabled={disabled}
                          className="h-9"
                        />
                      </FormControl>
                      <FormDescription className="text-xs">
                        Maximum response length in tokens (100-8192)
                      </FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                {/* Use Context Window */}
                <FormField
                  control={form.control}
                  name="useContextWindow"
                  render={({ field }) => (
                    <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                      <div className="space-y-0.5">
                        <FormLabel>Use Context Window</FormLabel>
                        <FormDescription className="text-xs">
                          Include conversation history in agent context
                        </FormDescription>
                      </div>
                      <FormControl>
                        <Switch
                          checked={field.value}
                          onCheckedChange={field.onChange}
                          disabled={disabled}
                        />
                      </FormControl>
                    </FormItem>
                  )}
                />
              </CollapsibleContent>
            </Collapsible>
          </form>
        </Form>
      </CardContent>
    </Card>
  );
}

export default AgentConfigPanel;
