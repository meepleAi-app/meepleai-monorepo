'use client';

/**
 * Node Configuration Panel
 *
 * Dynamic configuration panel that generates forms from plugin JSON schemas.
 * Supports all JSON Schema types with validation and conditional fields.
 *
 * @version 1.0.0
 * @see Issue #3427 - Node Configuration Panel
 */

import { useState, useMemo, useCallback } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import {
  Settings2,
  RotateCcw,
  Copy,
  Check,
  Code2,
  ChevronDown,
  ChevronRight,
  AlertCircle,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/data-display/collapsible';
import { Switch } from '@/components/ui/forms/switch';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { Slider } from '@/components/ui/primitives/slider';
import { Textarea } from '@/components/ui/primitives/textarea';
import { cn } from '@/lib/utils';
import { usePipelineBuilderStore, selectSelectedNode } from '@/stores/pipelineBuilderStore';

import { PLUGIN_CATEGORY_COLORS, PLUGIN_CATEGORY_ICONS } from './types';

import type { JsonSchemaProperty } from './types';

// =============================================================================
// Types
// =============================================================================

interface NodeConfigPanelProps {
  className?: string;
}

interface FieldProps {
  name: string;
  schema: JsonSchemaProperty;
  value: unknown;
  onChange: (name: string, value: unknown) => void;
  error?: string;
  path?: string;
}

// =============================================================================
// Form Field Components
// =============================================================================

function StringField({ name, schema, value, onChange, error }: FieldProps) {
  if (schema.enum) {
    return (
      <Select
        value={String(value || '')}
        onValueChange={(v) => onChange(name, v)}
      >
        <SelectTrigger className={cn(error && 'border-destructive')}>
          <SelectValue placeholder={`Select ${schema.title || name}...`} />
        </SelectTrigger>
        <SelectContent>
          {schema.enum.map((option) => (
            <SelectItem key={String(option)} value={String(option)}>
              {String(option)}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    );
  }

  if (schema.format === 'textarea') {
    return (
      <Textarea
        value={String(value || '')}
        onChange={(e) => onChange(name, e.target.value)}
        placeholder={schema.description}
        className={cn('min-h-[80px]', error && 'border-destructive')}
      />
    );
  }

  return (
    <Input
      type={schema.format === 'password' ? 'password' : 'text'}
      value={String(value || '')}
      onChange={(e) => onChange(name, e.target.value)}
      placeholder={schema.description}
      className={cn(error && 'border-destructive')}
    />
  );
}

function NumberField({ name, schema, value, onChange, error }: FieldProps) {
  const numValue = typeof value === 'number' ? value : (schema.default as number) || 0;
  const hasRange = schema.minimum !== undefined && schema.maximum !== undefined;

  if (hasRange) {
    return (
      <div className="space-y-2">
        <div className="flex items-center justify-between text-xs">
          <span className="text-muted-foreground">{schema.minimum}</span>
          <span className="font-mono text-foreground">{numValue}</span>
          <span className="text-muted-foreground">{schema.maximum}</span>
        </div>
        <Slider
          value={[numValue]}
          min={schema.minimum}
          max={schema.maximum}
          step={schema.type === 'integer' ? 1 : 0.01}
          onValueChange={([v]) => onChange(name, v)}
          className={cn(error && 'border-destructive')}
        />
      </div>
    );
  }

  return (
    <Input
      type="number"
      value={numValue}
      onChange={(e) =>
        onChange(name, schema.type === 'integer' ? parseInt(e.target.value) : parseFloat(e.target.value))
      }
      min={schema.minimum}
      max={schema.maximum}
      step={schema.type === 'integer' ? 1 : 'any'}
      className={cn(error && 'border-destructive')}
    />
  );
}

function BooleanField({ name, value, onChange }: FieldProps) {
  return (
    <Switch
      checked={Boolean(value)}
      onCheckedChange={(checked) => onChange(name, checked)}
    />
  );
}

function ArrayField({ name, schema, value, onChange, error }: FieldProps) {
  const arrayValue = Array.isArray(value) ? value : [];

  // Simple string array with enum (multi-select)
  if (schema.items?.enum) {
    const options = schema.items.enum as string[];
    const selected = new Set(arrayValue as string[]);

    const toggleOption = (option: string) => {
      const newSelected = new Set(selected);
      if (newSelected.has(option)) {
        newSelected.delete(option);
      } else {
        newSelected.add(option);
      }
      onChange(name, Array.from(newSelected));
    };

    return (
      <div className="flex flex-wrap gap-1.5">
        {options.map((option) => (
          <Badge
            key={option}
            variant={selected.has(option) ? 'default' : 'outline'}
            className="cursor-pointer"
            onClick={() => toggleOption(option)}
          >
            {option}
          </Badge>
        ))}
      </div>
    );
  }

  // Generic array display
  return (
    <div className="space-y-2">
      <div className="text-xs text-muted-foreground">
        {arrayValue.length} item{arrayValue.length !== 1 ? 's' : ''}
      </div>
      <Textarea
        value={JSON.stringify(arrayValue, null, 2)}
        onChange={(e) => {
          try {
            const parsed = JSON.parse(e.target.value);
            if (Array.isArray(parsed)) {
              onChange(name, parsed);
            }
          } catch {
            // Invalid JSON, ignore
          }
        }}
        placeholder="JSON array..."
        className={cn('font-mono text-xs min-h-[60px]', error && 'border-destructive')}
      />
    </div>
  );
}

function ObjectField({ name, schema, value, onChange, path = '' }: FieldProps) {
  const [isOpen, setIsOpen] = useState(true);
  const objValue = typeof value === 'object' && value !== null ? value : {};

  if (!schema.properties) {
    return (
      <Textarea
        value={JSON.stringify(objValue, null, 2)}
        onChange={(e) => {
          try {
            onChange(name, JSON.parse(e.target.value));
          } catch {
            // Invalid JSON
          }
        }}
        placeholder="JSON object..."
        className="font-mono text-xs min-h-[60px]"
      />
    );
  }

  const handleFieldChange = (fieldName: string, fieldValue: unknown) => {
    onChange(name, { ...objValue, [fieldName]: fieldValue });
  };

  return (
    <Collapsible open={isOpen} onOpenChange={setIsOpen}>
      <CollapsibleTrigger asChild>
        <button className="flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground">
          {isOpen ? <ChevronDown className="h-3 w-3" /> : <ChevronRight className="h-3 w-3" />}
          {Object.keys(schema.properties).length} properties
        </button>
      </CollapsibleTrigger>
      <CollapsibleContent>
        <div className="pl-3 border-l-2 border-muted mt-2 space-y-3">
          {Object.entries(schema.properties).map(([fieldName, fieldSchema]) => {
            // eslint-disable-next-line security/detect-object-injection -- fieldName from Object.entries
            const fieldValue = (objValue as Record<string, unknown>)[fieldName];
            return (
              <FormField
                key={fieldName}
                name={fieldName}
                schema={fieldSchema}
                value={fieldValue}
                onChange={handleFieldChange}
                path={path ? `${path}.${fieldName}` : fieldName}
              />
            );
          })}
        </div>
      </CollapsibleContent>
    </Collapsible>
  );
}

// =============================================================================
// Main Form Field
// =============================================================================

function FormField({ name, schema, value, onChange, error, path = '' }: FieldProps) {
  const FieldComponent = useMemo(() => {
    switch (schema.type) {
      case 'string':
        return StringField;
      case 'number':
      case 'integer':
        return NumberField;
      case 'boolean':
        return BooleanField;
      case 'array':
        return ArrayField;
      case 'object':
        return ObjectField;
      default:
        return StringField;
    }
  }, [schema.type]);

  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between">
        <Label htmlFor={name} className="text-sm font-medium">
          {schema.title || name}
        </Label>
        {schema.type === 'boolean' && (
          <FieldComponent
            name={name}
            schema={schema}
            value={value}
            onChange={onChange}
            error={error}
            path={path}
          />
        )}
      </div>
      {schema.type !== 'boolean' && (
        <FieldComponent
          name={name}
          schema={schema}
          value={value}
          onChange={onChange}
          error={error}
          path={path}
        />
      )}
      {schema.description && (
        <p className="text-[11px] text-muted-foreground">{schema.description}</p>
      )}
      {error && (
        <p className="text-[11px] text-destructive flex items-center gap-1">
          <AlertCircle className="h-3 w-3" />
          {error}
        </p>
      )}
    </div>
  );
}

// =============================================================================
// Main Component
// =============================================================================

export function NodeConfigPanel({ className }: NodeConfigPanelProps) {
  const [showJson, setShowJson] = useState(false);
  const [copied, setCopied] = useState(false);

  const selectedNode = usePipelineBuilderStore(selectSelectedNode);
  const { updateNodeConfig } = usePipelineBuilderStore();

  const handleConfigChange = useCallback(
    (name: string, value: unknown) => {
      if (!selectedNode) return;
      updateNodeConfig(selectedNode.id, { [name]: value });
    },
    [selectedNode, updateNodeConfig]
  );

  const handleReset = useCallback(() => {
    if (!selectedNode) return;
    const defaultConfig: Record<string, unknown> = {};
    const properties = selectedNode.data.configSchema.properties;
    if (properties) {
      Object.entries(properties).forEach(([key, prop]) => {
        if ('default' in prop && prop.default !== undefined) {
          // eslint-disable-next-line security/detect-object-injection -- key from Object.entries
          defaultConfig[key] = prop.default;
        }
      });
    }
    updateNodeConfig(selectedNode.id, defaultConfig);
  }, [selectedNode, updateNodeConfig]);

  const handleCopyJson = useCallback(() => {
    if (!selectedNode) return;
    navigator.clipboard.writeText(JSON.stringify(selectedNode.data.config, null, 2));
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }, [selectedNode]);

  if (!selectedNode) {
    return (
      <div className={cn('flex items-center justify-center h-full', className)}>
        <div className="text-center p-6">
          <Settings2 className="h-10 w-10 mx-auto text-muted-foreground/50 mb-3" />
          <p className="text-sm text-muted-foreground">Select a node to configure</p>
        </div>
      </div>
    );
  }

  const { data } = selectedNode;
  const categoryColor = PLUGIN_CATEGORY_COLORS[data.category];
  const categoryIcon = PLUGIN_CATEGORY_ICONS[data.category];
  const properties = data.configSchema.properties || {};
  const required = new Set(data.configSchema.required || []);

  return (
    <div className={cn('flex flex-col h-full', className)}>
      {/* Header */}
      <div className="p-3 border-b" style={{ borderBottomColor: `${categoryColor}40` }}>
        <div className="flex items-center gap-2 mb-2">
          <span className="text-xl">{categoryIcon}</span>
          <div className="flex-1 min-w-0">
            <h3 className="font-semibold text-sm truncate">{data.pluginName}</h3>
            <Badge
              variant="secondary"
              className="text-[10px] capitalize mt-0.5"
              style={{ backgroundColor: `${categoryColor}20`, color: categoryColor }}
            >
              {data.category}
            </Badge>
          </div>
          {!data.isValid && (
            <Badge variant="destructive" className="text-[10px]">
              Invalid
            </Badge>
          )}
        </div>
      </div>

      {/* Form */}
      <ScrollArea className="flex-1">
        <AnimatePresence mode="wait">
          {showJson ? (
            <motion.div
              key="json"
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="p-3"
            >
              <pre className="text-xs font-mono bg-muted p-3 rounded-md overflow-auto">
                {JSON.stringify(data.config, null, 2)}
              </pre>
            </motion.div>
          ) : (
            <motion.div
              key="form"
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="p-3 space-y-4"
            >
              {/* eslint-disable security/detect-object-injection -- name from Object.entries */}
              {Object.entries(properties).map(([name, schema]) => {
                const configValue = data.config[name];
                return (
                  <FormField
                    key={name}
                    name={name}
                    schema={schema}
                    value={configValue}
                    onChange={handleConfigChange}
                    error={
                      required.has(name) &&
                      (configValue === undefined ||
                        configValue === null ||
                        configValue === '')
                        ? 'This field is required'
                        : undefined
                    }
                  />
                );
              })}
              {/* eslint-enable security/detect-object-injection */}
              {Object.keys(properties).length === 0 && (
                <p className="text-sm text-muted-foreground text-center py-4">
                  This plugin has no configurable options
                </p>
              )}
            </motion.div>
          )}
        </AnimatePresence>
      </ScrollArea>

      {/* Footer Actions */}
      <div className="p-3 border-t flex items-center gap-2">
        <Button variant="outline" size="sm" onClick={handleReset} className="flex-1">
          <RotateCcw className="h-3.5 w-3.5 mr-1.5" />
          Reset
        </Button>
        <Button
          variant="outline"
          size="sm"
          onClick={handleCopyJson}
          className="flex-1"
        >
          {copied ? (
            <Check className="h-3.5 w-3.5 mr-1.5 text-green-500" />
          ) : (
            <Copy className="h-3.5 w-3.5 mr-1.5" />
          )}
          {copied ? 'Copied!' : 'Copy'}
        </Button>
        <Button
          variant="ghost"
          size="sm"
          onClick={() => setShowJson(!showJson)}
          className={cn(showJson && 'bg-muted')}
        >
          <Code2 className="h-3.5 w-3.5" />
        </Button>
      </div>
    </div>
  );
}
