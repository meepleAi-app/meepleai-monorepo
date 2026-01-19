import { useState } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import { useForm, Controller } from 'react-hook-form';
import { toast } from 'sonner';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Textarea } from '@/components/ui/primitives/textarea';
import { alertRulesApi } from '@/lib/api/alert-rules.api';
import {
  createAlertRuleSchema,
  type CreateAlertRule,
  type AlertRule,
} from '@/lib/api/schemas/alert-rules.schemas';

// Severity options with visual indicators
const SEVERITY_OPTIONS = [
  { value: 'Info', label: 'Info', colorClass: 'text-blue-600' },
  { value: 'Warning', label: 'Warning', colorClass: 'text-yellow-600' },
  { value: 'Error', label: 'Error', colorClass: 'text-orange-600' },
  { value: 'Critical', label: 'Critical', colorClass: 'text-red-600' },
] as const;

interface AlertRuleFormProps {
  rule: AlertRule | null;
  initialData?: Partial<CreateAlertRule>;
  onSubmit: () => void;
  onCancel: () => void;
}

export function AlertRuleForm({ rule, initialData, onSubmit, onCancel }: AlertRuleFormProps) {
  const [isSubmitting, setIsSubmitting] = useState(false);

  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<CreateAlertRule>({
    resolver: zodResolver(createAlertRuleSchema),
    defaultValues: initialData
      ? {
          name: initialData.name || '',
          alertType: initialData.alertType || '',
          severity: initialData.severity || 'Warning',
          thresholdValue: initialData.thresholdValue || 0,
          thresholdUnit: initialData.thresholdUnit || '%',
          durationMinutes: initialData.durationMinutes || 5,
          description: initialData.description || '',
        }
      : rule
        ? {
            name: rule.name,
            alertType: rule.alertType,
            severity: rule.severity,
            thresholdValue: rule.thresholdValue,
            thresholdUnit: rule.thresholdUnit,
            durationMinutes: rule.durationMinutes,
            description: rule.description || '',
          }
        : {
            name: '',
            alertType: '',
            severity: 'Warning',
            thresholdValue: 0,
            thresholdUnit: '%',
            durationMinutes: 5,
            description: '',
          },
  });

  const onSubmitForm = async (data: CreateAlertRule) => {
    setIsSubmitting(true);
    try {
      if (rule?.id) {
        await alertRulesApi.update(rule.id, data);
        toast.success('Alert rule updated successfully');
      } else {
        await alertRulesApi.create(data);
        toast.success('Alert rule created successfully');
      }
      onSubmit();
    } catch (error) {
      console.error('Failed to save rule:', error);
      toast.error('Failed to save alert rule', {
        description: error instanceof Error ? error.message : 'Unknown error',
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit(onSubmitForm)} className="space-y-4">
      <div>
        <Label htmlFor="name">Name *</Label>
        <Input id="name" {...register('name')} placeholder="High Error Rate" />
        {errors.name && <p className="text-sm text-destructive mt-1">{errors.name.message}</p>}
      </div>

      <div>
        <Label htmlFor="alertType">Alert Type *</Label>
        <Input
          id="alertType"
          {...register('alertType')}
          placeholder="HighErrorRate, HighLatency, ServiceDown..."
        />
        {errors.alertType && (
          <p className="text-sm text-destructive mt-1">{errors.alertType.message}</p>
        )}
      </div>

      <div>
        <Label htmlFor="severity">Severity *</Label>
        <Controller
          name="severity"
          control={control}
          render={({ field }) => (
            <Select onValueChange={field.onChange} value={field.value}>
              <SelectTrigger>
                <SelectValue placeholder="Select severity" />
              </SelectTrigger>
              <SelectContent>
                {SEVERITY_OPTIONS.map(option => (
                  <SelectItem key={option.value} value={option.value}>
                    <span className="flex items-center gap-2">
                      <span className={option.colorClass}>●</span>
                      {option.label}
                    </span>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
        />
        {errors.severity && (
          <p className="text-sm text-destructive mt-1">{errors.severity.message}</p>
        )}
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div>
          <Label htmlFor="thresholdValue">Threshold Value *</Label>
          <Input
            id="thresholdValue"
            type="number"
            step="0.01"
            {...register('thresholdValue', { valueAsNumber: true })}
            placeholder="5.0"
          />
          {errors.thresholdValue && (
            <p className="text-sm text-destructive mt-1">{errors.thresholdValue.message}</p>
          )}
        </div>
        <div>
          <Label htmlFor="thresholdUnit">Unit *</Label>
          <Input
            id="thresholdUnit"
            {...register('thresholdUnit')}
            placeholder="%, ms, count, req/s"
          />
          {errors.thresholdUnit && (
            <p className="text-sm text-destructive mt-1">{errors.thresholdUnit.message}</p>
          )}
        </div>
      </div>

      <div>
        <Label htmlFor="durationMinutes">Duration (minutes) *</Label>
        <Input
          id="durationMinutes"
          type="number"
          {...register('durationMinutes', { valueAsNumber: true })}
          placeholder="5"
        />
        {errors.durationMinutes && (
          <p className="text-sm text-destructive mt-1">{errors.durationMinutes.message}</p>
        )}
        <p className="text-sm text-muted-foreground mt-1">
          Alert triggers only if condition persists for this duration
        </p>
      </div>

      <div>
        <Label htmlFor="description">Description</Label>
        <Textarea
          id="description"
          {...register('description')}
          placeholder="Describe when this alert should trigger and what action to take..."
          rows={3}
        />
        {errors.description && (
          <p className="text-sm text-destructive mt-1">{errors.description.message}</p>
        )}
      </div>

      <div className="flex justify-end gap-2 pt-4">
        <Button type="button" variant="outline" onClick={onCancel} disabled={isSubmitting}>
          Cancel
        </Button>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Saving...' : rule ? 'Update Rule' : 'Create Rule'}
        </Button>
      </div>
    </form>
  );
}
