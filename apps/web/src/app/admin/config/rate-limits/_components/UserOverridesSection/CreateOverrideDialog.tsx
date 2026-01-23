'use client';

import { useState } from 'react';
import { useApiClient } from '@/lib/api/context';
import { type CreateOverrideRequest } from '@/types';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Spinner } from '@/components/loading/Spinner';
import { toast } from '@/components/layout/Toast';

interface CreateOverrideDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function CreateOverrideDialog({ open, onClose, onSuccess }: CreateOverrideDialogProps) {
  const { rateLimits } = useApiClient();
  const [loading, setLoading] = useState(false);
  const [formData, setFormData] = useState<CreateOverrideRequest>({
    userId: '',
    reason: '',
  });
  const [errors, setErrors] = useState<Record<string, string>>({});

  const resetForm = () => {
    setFormData({
      userId: '',
      reason: '',
    });
    setErrors({});
  };

  const validate = (): boolean => {
    const newErrors: Record<string, string> = {};

    // UUID validation regex
    const uuidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    if (!formData.userId.trim()) {
      newErrors.userId = 'User ID is required';
    } else if (!uuidRegex.test(formData.userId)) {
      newErrors.userId = 'Must be a valid UUID';
    }

    if (!formData.reason.trim()) {
      newErrors.reason = 'Reason is required';
    } else if (formData.reason.length < 10) {
      newErrors.reason = 'Reason must be at least 10 characters';
    }

    // At least one override field must be provided
    if (
      formData.maxPendingRequests === undefined &&
      formData.maxRequestsPerMonth === undefined &&
      formData.cooldownDays === undefined
    ) {
      newErrors.overrides = 'At least one limit override is required';
    }

    // Validate individual override fields if provided
    if (formData.maxPendingRequests !== undefined && formData.maxPendingRequests <= 0) {
      newErrors.maxPendingRequests = 'Must be greater than 0';
    }

    if (formData.maxRequestsPerMonth !== undefined && formData.maxRequestsPerMonth <= 0) {
      newErrors.maxRequestsPerMonth = 'Must be greater than 0';
    }

    if (formData.cooldownDays !== undefined && formData.cooldownDays < 0) {
      newErrors.cooldownDays = 'Cannot be negative';
    }

    // Validate expiration date if provided
    if (formData.expiresAt) {
      const expirationDate = new Date(formData.expiresAt);
      if (expirationDate <= new Date()) {
        newErrors.expiresAt = 'Expiration must be in the future';
      }
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async () => {
    if (!validate()) return;

    setLoading(true);
    try {
      await rateLimits.createOverride(formData);
      toast.success('User override created successfully');
      resetForm();
      onSuccess();
    } catch (err: unknown) {
      const error = err as Error;
      toast.error(`Failed to create override: ${error.message}`);
    } finally {
      setLoading(false);
    }
  };

  const handleClose = () => {
    if (loading) {
      if (!confirm('Creation in progress. Are you sure you want to close?')) {
        return;
      }
    }
    resetForm();
    onClose();
  };

  return (
    <Dialog open={open} onOpenChange={(open) => !open && handleClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Create User Override</DialogTitle>
          <DialogDescription>
            Set custom rate limits for a specific user. At least one override field is required.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div>
            <label htmlFor="userId" className="block text-sm font-medium mb-1">
              User ID (UUID) *
            </label>
            <Input
              id="userId"
              type="text"
              placeholder="e.g., 123e4567-e89b-12d3-a456-426614174000"
              value={formData.userId}
              onChange={(e) => setFormData((prev) => ({ ...prev, userId: e.target.value }))}
              disabled={loading}
            />
            {errors.userId && <p className="text-sm text-red-600 mt-1">{errors.userId}</p>}
          </div>

          <div className="border-t pt-4">
            <p className="text-sm font-medium mb-2">Override Limits (at least one required)</p>

            <div className="space-y-3">
              <div>
                <label htmlFor="maxPending" className="block text-sm mb-1">
                  Max Pending Requests
                </label>
                <Input
                  id="maxPending"
                  type="number"
                  placeholder="Leave empty for default"
                  value={formData.maxPendingRequests ?? ''}
                  onChange={(e) =>
                    setFormData((prev) => ({
                      ...prev,
                      maxPendingRequests: e.target.value ? parseInt(e.target.value) : undefined,
                    }))
                  }
                  min="1"
                  disabled={loading}
                />
                {errors.maxPendingRequests && (
                  <p className="text-sm text-red-600 mt-1">{errors.maxPendingRequests}</p>
                )}
              </div>

              <div>
                <label htmlFor="maxMonthly" className="block text-sm mb-1">
                  Max Requests Per Month
                </label>
                <Input
                  id="maxMonthly"
                  type="number"
                  placeholder="Leave empty for default"
                  value={formData.maxRequestsPerMonth ?? ''}
                  onChange={(e) =>
                    setFormData((prev) => ({
                      ...prev,
                      maxRequestsPerMonth: e.target.value ? parseInt(e.target.value) : undefined,
                    }))
                  }
                  min="1"
                  disabled={loading}
                />
                {errors.maxRequestsPerMonth && (
                  <p className="text-sm text-red-600 mt-1">{errors.maxRequestsPerMonth}</p>
                )}
              </div>

              <div>
                <label htmlFor="cooldown" className="block text-sm mb-1">
                  Cooldown After Rejection (days)
                </label>
                <Input
                  id="cooldown"
                  type="number"
                  placeholder="Leave empty for default"
                  value={formData.cooldownDays ?? ''}
                  onChange={(e) =>
                    setFormData((prev) => ({
                      ...prev,
                      cooldownDays: e.target.value ? parseInt(e.target.value) : undefined,
                    }))
                  }
                  min="0"
                  disabled={loading}
                />
                {errors.cooldownDays && (
                  <p className="text-sm text-red-600 mt-1">{errors.cooldownDays}</p>
                )}
              </div>

              {errors.overrides && (
                <p className="text-sm text-red-600">{errors.overrides}</p>
              )}
            </div>
          </div>

          <div>
            <label htmlFor="expiresAt" className="block text-sm font-medium mb-1">
              Expiration Date (optional)
            </label>
            <Input
              id="expiresAt"
              type="datetime-local"
              value={formData.expiresAt ?? ''}
              onChange={(e) =>
                setFormData((prev) => ({
                  ...prev,
                  expiresAt: e.target.value || undefined,
                }))
              }
              disabled={loading}
            />
            {errors.expiresAt && <p className="text-sm text-red-600 mt-1">{errors.expiresAt}</p>}
            <p className="text-xs text-gray-600 mt-1">Leave empty for no expiration</p>
          </div>

          <div>
            <label htmlFor="reason" className="block text-sm font-medium mb-1">
              Reason *
            </label>
            <textarea
              id="reason"
              className="w-full border rounded px-3 py-2 text-sm min-h-[80px]"
              placeholder="Explain why this override is needed (min 10 characters)"
              value={formData.reason}
              onChange={(e) => setFormData((prev) => ({ ...prev, reason: e.target.value }))}
              disabled={loading}
            />
            {errors.reason && <p className="text-sm text-red-600 mt-1">{errors.reason}</p>}
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleClose} disabled={loading}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={loading}>
            {loading && <Spinner className="mr-2" />}
            Create Override
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
