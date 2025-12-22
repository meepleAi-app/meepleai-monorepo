/**
 * ApiKeyCreationModal Component (Issue #909)
 *
 * Advanced modal for creating API keys with metadata support.
 * Integrates with backend CreateApiKeyManagementCommand (CQRS).
 *
 * Features:
 * - WCAG 2.1 AA compliant accessibility
 * - Form with validation (KeyName, Scopes, ExpiresAt, Metadata)
 * - JSON metadata editor with validation
 * - Scopes preview with descriptions
 * - One-time plaintext key display with copy-to-clipboard
 * - Real-time validation
 * - Keyboard accessible (ESC to close, Tab navigation)
 * - Loading and error states
 *
 * @example
 * ```tsx
 * <ApiKeyCreationModal
 *   isOpen={isModalOpen}
 *   onClose={() => setIsModalOpen(false)}
 *   onApiKeyCreated={(apiKey) => {
 *     toast.success('API key created!');
 *     console.log('Plaintext key (shown once):', apiKey.plaintextKey);
 *   }}
 * />
 * ```
 */

import { useState, useEffect } from 'react';

import { Copy, Check, AlertCircle, Info, Calendar, Key } from 'lucide-react';

import { LoadingButton } from '@/components/loading/LoadingButton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Textarea } from '@/components/ui/textarea';
import { api, CreateApiKeyResponse } from '@/lib/api';
import { createErrorContext } from '@/lib/errors';
import { logger } from '@/lib/logger';

export interface ApiKeyCreationModalProps {
  /** Whether the modal is open */
  isOpen: boolean;

  /** Callback when modal should close */
  onClose: () => void;

  /** Callback when API key is successfully created */
  onApiKeyCreated?: (apiKey: CreateApiKeyResponse) => void;
}

// Available scopes with descriptions
const AVAILABLE_SCOPES = [
  { value: 'read', label: 'Read', description: 'Read-only access to resources', color: 'blue' },
  { value: 'write', label: 'Write', description: 'Create and update resources', color: 'green' },
  { value: 'admin', label: 'Admin', description: 'Full administrative access', color: 'red' },
] as const;

interface FormData {
  keyName: string;
  scopes: string[];
  expiresAt: string;
  metadata: string;
}

interface ValidationErrors {
  keyName?: string;
  scopes?: string;
  expiresAt?: string;
  metadata?: string;
  general?: string;
}

/**
 * ApiKeyCreationModal component
 */
export function ApiKeyCreationModal({
  isOpen,
  onClose,
  onApiKeyCreated,
}: ApiKeyCreationModalProps) {
  // Form state
  const [formData, setFormData] = useState<FormData>({
    keyName: '',
    scopes: [],
    expiresAt: '',
    metadata: '',
  });

  // UI state
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [validationErrors, setValidationErrors] = useState<ValidationErrors>({});
  const [createdApiKey, setCreatedApiKey] = useState<CreateApiKeyResponse | null>(null);
  const [copied, setCopied] = useState(false);

  // Reset state when modal closes
  useEffect(() => {
    if (!isOpen) {
      setFormData({
        keyName: '',
        scopes: [],
        expiresAt: '',
        metadata: '',
      });
      setValidationErrors({});
      setCreatedApiKey(null);
      setCopied(false);
      setIsSubmitting(false);
    }
  }, [isOpen]);

  // Validate form
  const validateForm = (): boolean => {
    const errors: ValidationErrors = {};

    // Validate key name (3-100 chars)
    if (!formData.keyName.trim()) {
      errors.keyName = 'API key name is required';
    } else if (formData.keyName.trim().length < 3) {
      errors.keyName = 'API key name must be at least 3 characters';
    } else if (formData.keyName.trim().length > 100) {
      errors.keyName = 'API key name must be at most 100 characters';
    }

    // Validate scopes (at least one required)
    if (formData.scopes.length === 0) {
      errors.scopes = 'At least one scope is required';
    }

    // Validate expiration date (optional, but must be in future if provided)
    if (formData.expiresAt) {
      const expiryDate = new Date(formData.expiresAt);
      const now = new Date();
      if (expiryDate <= now) {
        errors.expiresAt = 'Expiration date must be in the future';
      }
    }

    // Validate metadata JSON (optional, but must be valid JSON if provided)
    if (formData.metadata.trim()) {
      try {
        JSON.parse(formData.metadata);
      } catch (_e) {
        errors.metadata = 'Metadata must be valid JSON';
      }
    }

    setValidationErrors(errors);
    return Object.keys(errors).length === 0;
  };

  // Handle scope toggle
  const toggleScope = (scope: string) => {
    setFormData(prev => {
      const scopes = prev.scopes.includes(scope)
        ? prev.scopes.filter(s => s !== scope)
        : [...prev.scopes, scope];
      return { ...prev, scopes };
    });
    // Clear scopes error when user selects a scope
    if (validationErrors.scopes) {
      setValidationErrors(prev => ({ ...prev, scopes: undefined }));
    }
  };

  // Handle form submission
  const handleSubmit = async () => {
    if (!validateForm()) {
      return;
    }

    setIsSubmitting(true);
    setValidationErrors({});

    try {
      const request = {
        keyName: formData.keyName.trim(),
        scopes: formData.scopes.join(','),
        expiresAt: formData.expiresAt ? new Date(formData.expiresAt).toISOString() : null,
        metadata: formData.metadata.trim() || null,
      };

      logger.info(
        'Creating API key',
        createErrorContext('ApiKeyCreationModal', 'handleSubmit', { keyName: request.keyName })
      );
      const response = await api.auth.createApiKey(request);

      logger.info(
        'API key created successfully',
        createErrorContext('ApiKeyCreationModal', 'handleSubmit', { keyId: response.id })
      );
      setCreatedApiKey(response);
      onApiKeyCreated?.(response);
    } catch (error: unknown) {
      const ctx = createErrorContext('ApiKeyCreationModal', 'handleSubmit');
      const errorObj = error instanceof Error ? error : new Error(String(error));
      logger.error('Failed to create API key', errorObj, ctx);

      if (error instanceof Error) {
        setValidationErrors({ general: error.message || 'Failed to create API key' });
      } else {
        setValidationErrors({ general: 'An unexpected error occurred' });
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  // Handle copy to clipboard
  const handleCopyKey = async () => {
    if (!createdApiKey) return;

    try {
      await navigator.clipboard.writeText(createdApiKey.plaintextKey);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (error: unknown) {
      const ctx = createErrorContext('ApiKeyCreationModal', 'handleCopyKey');
      const errorObj = error instanceof Error ? error : new Error(String(error));
      logger.error('Failed to copy API key', errorObj, ctx);
    }
  };

  // Success state: Show the created API key
  if (createdApiKey) {
    return (
      <Dialog open={isOpen} onOpenChange={onClose}>
        <DialogContent className="sm:max-w-[600px]" aria-describedby="api-key-success-description">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <Check className="h-5 w-5 text-green-600" />
              API Key Created Successfully
            </DialogTitle>
            <DialogDescription id="api-key-success-description">
              Save this key somewhere safe. You won't be able to see it again.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            {/* Key Info */}
            <div className="rounded-lg border border-gray-200 bg-gray-50 p-4 dark:border-gray-700 dark:bg-gray-800">
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-medium text-gray-700 dark:text-gray-300">
                    Key Name:
                  </span>
                  <span className="text-sm text-gray-900 dark:text-white">
                    {createdApiKey.keyName}
                  </span>
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-sm font-medium text-gray-700 dark:text-gray-300">
                    Key Prefix:
                  </span>
                  <code className="rounded bg-gray-200 px-2 py-1 text-xs font-mono dark:bg-gray-700">
                    {createdApiKey.keyPrefix}
                  </code>
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-sm font-medium text-gray-700 dark:text-gray-300">
                    Scopes:
                  </span>
                  <span className="text-sm text-gray-900 dark:text-white">
                    {createdApiKey.scopes}
                  </span>
                </div>
                {createdApiKey.expiresAt && (
                  <div className="flex items-center justify-between">
                    <span className="text-sm font-medium text-gray-700 dark:text-gray-300">
                      Expires:
                    </span>
                    <span className="text-sm text-gray-900 dark:text-white">
                      {new Date(createdApiKey.expiresAt).toLocaleDateString()}
                    </span>
                  </div>
                )}
              </div>
            </div>

            {/* Plaintext Key (one-time display) */}
            <Alert>
              <AlertCircle className="h-4 w-4" />
              <AlertDescription className="space-y-2">
                <p className="font-medium">Your API Key (shown once):</p>
                <div className="flex items-center gap-2">
                  <code className="flex-1 rounded bg-gray-100 px-3 py-2 text-sm font-mono dark:bg-gray-900">
                    {createdApiKey.plaintextKey}
                  </code>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={handleCopyKey}
                    disabled={copied}
                    aria-label="Copy API key to clipboard"
                  >
                    {copied ? (
                      <>
                        <Check className="h-4 w-4 mr-1" />
                        Copied
                      </>
                    ) : (
                      <>
                        <Copy className="h-4 w-4 mr-1" />
                        Copy
                      </>
                    )}
                  </Button>
                </div>
                <p className="text-xs text-gray-600 dark:text-gray-400">
                  Make sure to copy this key now. You won't be able to see it again!
                </p>
              </AlertDescription>
            </Alert>
          </div>

          <DialogFooter>
            <Button onClick={onClose}>Close</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    );
  }

  // Creation form state
  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[600px]" aria-describedby="api-key-form-description">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Key className="h-5 w-5" />
            Create New API Key
          </DialogTitle>
          <DialogDescription id="api-key-form-description">
            Generate a new API key for programmatic access. Choose permissions carefully.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {/* General error */}
          {validationErrors.general && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>{validationErrors.general}</AlertDescription>
            </Alert>
          )}

          {/* Key Name */}
          <div className="space-y-2">
            <Label htmlFor="keyName">
              API Key Name <span className="text-red-500">*</span>
            </Label>
            <Input
              id="keyName"
              value={formData.keyName}
              onChange={e => {
                setFormData({ ...formData, keyName: e.target.value });
                if (validationErrors.keyName) {
                  setValidationErrors({ ...validationErrors, keyName: undefined });
                }
              }}
              placeholder="e.g., Production API Key"
              aria-invalid={!!validationErrors.keyName}
              aria-describedby={validationErrors.keyName ? 'keyName-error' : undefined}
              disabled={isSubmitting}
            />
            {validationErrors.keyName && (
              <p id="keyName-error" className="text-sm text-red-600" role="alert">
                {validationErrors.keyName}
              </p>
            )}
            <p className="text-xs text-gray-500">3-100 characters, descriptive name for tracking</p>
          </div>

          {/* Scopes Selection */}
          <div className="space-y-2">
            <Label>
              Scopes <span className="text-red-500">*</span>
            </Label>
            <div className="space-y-2">
              {AVAILABLE_SCOPES.map(scope => (
                <div
                  key={scope.value}
                  className={`flex items-start space-x-3 rounded-lg border p-3 transition-colors ${
                    formData.scopes.includes(scope.value)
                      ? 'border-blue-500 bg-blue-50 dark:bg-blue-900/20'
                      : 'border-gray-200 hover:border-gray-300 dark:border-gray-700'
                  }`}
                >
                  <input
                    type="checkbox"
                    id={`scope-${scope.value}`}
                    checked={formData.scopes.includes(scope.value)}
                    onChange={() => toggleScope(scope.value)}
                    className="mt-1 h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-2 focus:ring-blue-500"
                    disabled={isSubmitting}
                  />
                  <div className="flex-1">
                    <label
                      htmlFor={`scope-${scope.value}`}
                      className="flex items-center gap-2 cursor-pointer"
                    >
                      <span className="font-medium text-gray-900 dark:text-white">
                        {scope.label}
                      </span>
                      <Badge
                        variant="outline"
                        className={`text-xs ${
                          scope.color === 'blue'
                            ? 'border-blue-500 text-blue-700'
                            : scope.color === 'green'
                              ? 'border-green-500 text-green-700'
                              : 'border-red-500 text-red-700'
                        }`}
                      >
                        {scope.value}
                      </Badge>
                    </label>
                    <p className="text-xs text-gray-600 dark:text-gray-400 mt-1">
                      {scope.description}
                    </p>
                  </div>
                </div>
              ))}
            </div>
            {validationErrors.scopes && (
              <p className="text-sm text-red-600" role="alert">
                {validationErrors.scopes}
              </p>
            )}
          </div>

          {/* Expiration Date */}
          <div className="space-y-2">
            <Label htmlFor="expiresAt" className="flex items-center gap-2">
              <Calendar className="h-4 w-4" />
              Expiration Date (Optional)
            </Label>
            <Input
              id="expiresAt"
              type="datetime-local"
              value={formData.expiresAt}
              onChange={e => {
                setFormData({ ...formData, expiresAt: e.target.value });
                if (validationErrors.expiresAt) {
                  setValidationErrors({ ...validationErrors, expiresAt: undefined });
                }
              }}
              aria-invalid={!!validationErrors.expiresAt}
              aria-describedby={validationErrors.expiresAt ? 'expiresAt-error' : undefined}
              disabled={isSubmitting}
            />
            {validationErrors.expiresAt && (
              <p id="expiresAt-error" className="text-sm text-red-600" role="alert">
                {validationErrors.expiresAt}
              </p>
            )}
            <p className="text-xs text-gray-500">
              Leave empty for keys that never expire (not recommended for production)
            </p>
          </div>

          {/* Metadata JSON Editor */}
          <div className="space-y-2">
            <Label htmlFor="metadata" className="flex items-center gap-2">
              <Info className="h-4 w-4" />
              Metadata (Optional JSON)
            </Label>
            <Textarea
              id="metadata"
              value={formData.metadata}
              onChange={e => {
                setFormData({ ...formData, metadata: e.target.value });
                if (validationErrors.metadata) {
                  setValidationErrors({ ...validationErrors, metadata: undefined });
                }
              }}
              placeholder='{"environment": "production", "project": "my-app"}'
              rows={4}
              className="font-mono text-sm"
              aria-invalid={!!validationErrors.metadata}
              aria-describedby={validationErrors.metadata ? 'metadata-error' : 'metadata-hint'}
              disabled={isSubmitting}
            />
            {validationErrors.metadata ? (
              <p id="metadata-error" className="text-sm text-red-600" role="alert">
                {validationErrors.metadata}
              </p>
            ) : (
              <p id="metadata-hint" className="text-xs text-gray-500">
                Optional JSON metadata for tracking purposes (e.g., environment, project name)
              </p>
            )}
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <LoadingButton
            onClick={handleSubmit}
            isLoading={isSubmitting}
            disabled={isSubmitting}
            aria-label="Create API key"
          >
            Create API Key
          </LoadingButton>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
