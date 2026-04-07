'use client';

import { useCallback, useEffect, useState } from 'react';

import { Loader2 } from 'lucide-react';
import { toast } from 'sonner';

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
import { useServiceConfig, useUpdateServiceConfig } from '@/hooks/admin/use-infrastructure';
import type { ServiceConfigParam } from '@/lib/api/clients/infrastructureClient';

interface ServiceConfigFormProps {
  serviceName: string;
  isSuperAdmin: boolean;
}

export function ServiceConfigForm({ serviceName, isSuperAdmin }: ServiceConfigFormProps) {
  const { data: configData, isLoading } = useServiceConfig(serviceName);
  const updateConfig = useUpdateServiceConfig();

  const [values, setValues] = useState<Record<string, string>>({});
  const [initialValues, setInitialValues] = useState<Record<string, string>>({});

  useEffect(() => {
    if (configData?.parameters) {
      const vals: Record<string, string> = {};
      configData.parameters.forEach(p => {
        vals[p.key] = p.value;
      });
      setValues(vals);
      setInitialValues(vals);
    }
  }, [configData]);

  const hasChanges = Object.keys(values).some(k => values[k] !== initialValues[k]);

  const handleChange = useCallback((key: string, val: string) => {
    setValues(prev => ({ ...prev, [key]: val }));
  }, []);

  const handleSave = useCallback(() => {
    const changed: Record<string, string> = {};
    for (const k of Object.keys(values)) {
      if (values[k] !== initialValues[k]) {
        changed[k] = values[k];
      }
    }
    if (Object.keys(changed).length === 0) return;

    updateConfig.mutate(
      { name: serviceName, params: changed },
      {
        onSuccess: () => {
          toast.success(`Configuration updated for ${serviceName}`);
          setInitialValues({ ...values });
        },
        onError: () => {
          toast.error('Failed to update configuration');
        },
      }
    );
  }, [serviceName, values, initialValues, updateConfig]);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-8">
        <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const params = configData?.parameters ?? [];

  if (params.length === 0) {
    return (
      <p className="text-sm text-muted-foreground py-4">
        No configurable parameters for this service.
      </p>
    );
  }

  return (
    <div className="space-y-4">
      {params.map((param: ServiceConfigParam) => (
        <div key={param.key} className="space-y-1.5">
          <Label htmlFor={`config-${param.key}`} className="text-xs font-medium">
            {param.displayName}
          </Label>

          {param.type === 'enum' && param.options ? (
            <Select
              value={values[param.key] ?? param.value}
              onValueChange={v => handleChange(param.key, v)}
              disabled={!isSuperAdmin}
            >
              <SelectTrigger id={`config-${param.key}`} className="h-8 text-xs">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {param.options.map(opt => (
                  <SelectItem key={opt} value={opt} className="text-xs">
                    {opt}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          ) : (
            <Input
              id={`config-${param.key}`}
              type={param.type === 'int' ? 'number' : 'text'}
              value={values[param.key] ?? param.value}
              onChange={e => handleChange(param.key, e.target.value)}
              disabled={!isSuperAdmin}
              min={param.minValue ?? undefined}
              max={param.maxValue ?? undefined}
              className="h-8 text-xs"
            />
          )}
        </div>
      ))}

      {hasChanges && isSuperAdmin && (
        <Button size="sm" onClick={handleSave} disabled={updateConfig.isPending} className="w-full">
          {updateConfig.isPending ? (
            <>
              <Loader2 className="h-3.5 w-3.5 mr-1.5 animate-spin" />
              Saving...
            </>
          ) : (
            'Save Changes'
          )}
        </Button>
      )}
    </div>
  );
}
