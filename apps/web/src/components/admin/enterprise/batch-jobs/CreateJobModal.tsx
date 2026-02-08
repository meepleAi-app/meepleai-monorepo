/**
 * CreateJobModal Component
 * Issue #3693 - Batch Job System
 *
 * Form for creating new batch jobs.
 * Features:
 * - Job type dropdown (ResourceForecast, CostAnalysis, DataCleanup, BggSync, AgentBenchmark)
 * - Parameters textarea (JSON input with validation)
 * - Create button with loading state
 */

'use client';

import { useState } from 'react';

import { PlusIcon, XIcon } from 'lucide-react';
import { toast } from 'sonner';

import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/overlays/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api } from '@/lib/api';
import type { BatchJobType } from '@/lib/api/schemas';

interface CreateJobModalProps {
  open: boolean;
  onClose: () => void;
  onCreate: () => void;
}

const JOB_TYPES: { value: BatchJobType; label: string; description: string }[] = [
  {
    value: 'ResourceForecast',
    label: 'Resource Forecast',
    description: 'Predict future resource usage and requirements',
  },
  {
    value: 'CostAnalysis',
    label: 'Cost Analysis',
    description: 'Analyze cost trends and optimization opportunities',
  },
  {
    value: 'DataCleanup',
    label: 'Data Cleanup',
    description: 'Remove old or unused data to optimize storage',
  },
  {
    value: 'BggSync',
    label: 'BGG Sync',
    description: 'Synchronize data with BoardGameGeek catalog',
  },
  {
    value: 'AgentBenchmark',
    label: 'Agent Benchmark',
    description: 'Run performance benchmarks for AI agents',
  },
];

export function CreateJobModal({ open, onClose, onCreate }: CreateJobModalProps) {
  const [jobType, setJobType] = useState<BatchJobType | null>(null);
  const [parameters, setParameters] = useState('{}');
  const [parametersError, setParametersError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const validateParameters = (value: string): boolean => {
    try {
      JSON.parse(value);
      setParametersError(null);
      return true;
    } catch (error) {
      setParametersError('Invalid JSON format');
      return false;
    }
  };

  const handleParametersChange = (value: string) => {
    setParameters(value);
    if (value.trim()) {
      validateParameters(value);
    } else {
      setParametersError(null);
    }
  };

  const handleCreate = async () => {
    if (!jobType) {
      toast.error('Please select a job type');
      return;
    }

    if (parameters.trim() && !validateParameters(parameters)) {
      return;
    }

    setLoading(true);
    try {
      const parsedParams = parameters.trim() ? JSON.parse(parameters) : undefined;
      await api.admin.createBatchJob({
        type: jobType,
        parameters: parsedParams,
      });
      toast.success('Job created successfully');
      onCreate();
      handleReset();
    } catch (error) {
      toast.error('Failed to create job');
      console.error('Create job error:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleReset = () => {
    setJobType(null);
    setParameters('{}');
    setParametersError(null);
  };

  const selectedJobType = JOB_TYPES.find(jt => jt.value === jobType);

  return (
    <Dialog open={open} onOpenChange={onClose}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Create Batch Job</DialogTitle>
        </DialogHeader>

        <div className="space-y-6">
          {/* Job Type Selection */}
          <div className="space-y-2">
            <Label htmlFor="job-type">Job Type</Label>
            <Select value={jobType ?? undefined} onValueChange={(value) => setJobType(value as BatchJobType)}>
              <SelectTrigger id="job-type">
                <SelectValue placeholder="Select job type" />
              </SelectTrigger>
              <SelectContent>
                {JOB_TYPES.map((type) => (
                  <SelectItem key={type.value} value={type.value}>
                    <div className="flex flex-col items-start">
                      <span className="font-medium">{type.label}</span>
                      <span className="text-xs text-zinc-500 dark:text-zinc-400">
                        {type.description}
                      </span>
                    </div>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {selectedJobType && (
              <p className="text-sm text-zinc-600 dark:text-zinc-400">
                {selectedJobType.description}
              </p>
            )}
          </div>

          {/* Parameters Input */}
          <div className="space-y-2">
            <Label htmlFor="parameters">
              Parameters (JSON)
              <span className="text-zinc-500 dark:text-zinc-400 font-normal ml-2">Optional</span>
            </Label>
            <Textarea
              id="parameters"
              value={parameters}
              onChange={(e) => handleParametersChange(e.target.value)}
              placeholder='{"key": "value"}'
              rows={8}
              className="font-mono text-xs"
            />
            {parametersError && (
              <p className="text-sm text-red-600 dark:text-red-400">{parametersError}</p>
            )}
            <p className="text-xs text-zinc-500 dark:text-zinc-400">
              Enter job configuration as valid JSON. Leave empty for default settings.
            </p>
          </div>

          {/* Parameter Examples */}
          <div className="bg-zinc-50 dark:bg-zinc-900 rounded-lg p-3">
            <h4 className="text-xs font-semibold text-zinc-700 dark:text-zinc-300 mb-2">
              Example Parameters
            </h4>
            <pre className="text-xs text-zinc-600 dark:text-zinc-400 overflow-x-auto">
{`{
  "daysAhead": 30,
  "includeMetrics": ["tokens", "cost"],
  "format": "detailed"
}`}
            </pre>
          </div>
        </div>

        {/* Actions */}
        <div className="flex items-center justify-end gap-2 pt-4 border-t border-zinc-200 dark:border-zinc-800">
          <Button variant="outline" onClick={onClose} disabled={loading}>
            <XIcon className="h-4 w-4 mr-2" />
            Cancel
          </Button>
          <Button onClick={handleCreate} disabled={loading || !jobType || !!parametersError}>
            <PlusIcon className="h-4 w-4 mr-2" />
            {loading ? 'Creating...' : 'Create Job'}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
