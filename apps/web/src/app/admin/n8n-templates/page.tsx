'use client';

import { useState, useEffect, useCallback } from 'react';
import { api } from '@/lib/api';
import { ErrorDisplay } from '@/components/errors';
import { categorizeError } from '@/lib/errorUtils';
import { LoadingButton } from '@/components/loading/LoadingButton';
import { getErrorMessage } from '@/lib/utils/errorHandler';

interface TemplateParameter {
  name: string;
  type: string;
  label: string;
  description: string;
  required: boolean;
  default: string | null;
  options: string[] | null;
  sensitive: boolean;
}

interface WorkflowTemplate {
  id: string;
  name: string;
  version: string;
  description: string;
  category: string;
  author: string;
  tags: string[];
  icon: string;
  screenshot: string | null;
  documentation: string | null;
  parameters: TemplateParameter[];
}

interface WorkflowTemplateDetail extends WorkflowTemplate {
  workflow: object;
}

const categories = [
  { value: '', label: 'All Categories' },
  { value: 'integration', label: 'Integration' },
  { value: 'automation', label: 'Automation' },
  { value: 'monitoring', label: 'Monitoring' },
  { value: 'data-processing', label: 'Data Processing' }
];

const N8nTemplatesPage = () => {
  const [templates, setTemplates] = useState<WorkflowTemplate[]>([]);
  const [selectedTemplate, setSelectedTemplate] = useState<WorkflowTemplateDetail | null>(null);
  const [category, setCategory] = useState<string>('');
  const [loading, setLoading] = useState(true);
  const [importing, setImporting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const loadTemplates = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const url = category
        ? `/api/v1/n8n/templates?category=${category}`
        : '/api/v1/n8n/templates';
      const data = await api.get<WorkflowTemplate[]>(url);

      if (data === null) {
        setError('Unauthorized. Please log in.');
        setTemplates([]);
        return;
      }

      setTemplates(data);
    } catch (err) {
      console.error('Failed to load templates:', err);
      setError('Failed to load templates. Please try again.');
    } finally {
      setLoading(false);
    }
  }, [category]);

  useEffect(() => {
    loadTemplates();
  }, [loadTemplates]);

  const handleSelectTemplate = async (templateId: string) => {
    try {
      const data = await api.get<WorkflowTemplateDetail>(`/api/v1/n8n/templates/${templateId}`);

      if (data === null) {
        setError('Unauthorized. Please log in.');
        return;
      }

      setSelectedTemplate(data);
    } catch (err) {
      console.error('Failed to load template details:', err);
      setError('Failed to load template details. Please try again.');
    }
  };

  const handleImport = async (templateId: string, parameters: Record<string, string>) => {
    setImporting(true);
    setError(null);
    setSuccessMessage(null);

    try {
      const result = await api.post<{ workflowId: string; message: string }>(
        `/api/v1/n8n/templates/${templateId}/import`,
        { parameters }
      );

      setSuccessMessage(`${result.message} (Workflow ID: ${result.workflowId})`);
      setSelectedTemplate(null);

      // Auto-dismiss success message after 5 seconds
      setTimeout(() => setSuccessMessage(null), 5000);
    } catch (err) {
      console.error('Failed to import template:', err);
      const errorMsg = err?.response?.data?.error || getErrorMessage(err, 'Failed to import template');
      setError(`Import failed: ${errorMsg}`);
    } finally {
      setImporting(false);
    }
  };

  return (
    <div className="min-h-dvh bg-gray-50 py-8">
      <div className="container mx-auto px-4">
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-4xl font-bold text-gray-900 mb-2">
            n8n Workflow Templates
          </h1>
          <p className="text-lg text-gray-600">
            Browse and import pre-built automation workflows for common tasks
          </p>
        </div>

        {/* Success Message */}
        {successMessage && (
          <div className="mb-6 p-4 bg-green-50 border-l-4 border-green-500 text-green-700">
            <div className="flex items-center">
              <span className="text-2xl mr-3">✅</span>
              <span>{successMessage}</span>
            </div>
          </div>
        )}

        {/* Error Message */}
        {error && (
          <div className="mb-6">
            <ErrorDisplay
              error={categorizeError(new Error(error))}
              onRetry={loadTemplates}
              onDismiss={() => setError(null)}
              showTechnicalDetails={process.env.NODE_ENV === 'development'}
            />
          </div>
        )}

        {/* Category Filter */}
        <div className="mb-6">
          <div className="flex flex-wrap gap-2">
            {categories.map((cat) => (
              <button
                key={cat.value}
                onClick={() => setCategory(cat.value)}
                className={`px-4 py-2 rounded-lg font-medium transition-colors ${
                  category === cat.value
                    ? 'bg-blue-600 text-white'
                    : 'bg-white text-gray-700 border border-gray-300 hover:bg-gray-50'
                }`}
              >
                {cat.label}
              </button>
            ))}
          </div>
        </div>

        {/* Template Grid */}
        {loading ? (
          <div className="text-center py-12">
            <div className="inline-block animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
            <p className="mt-4 text-gray-600">Loading templates...</p>
          </div>
        ) : templates.length === 0 ? (
          <div className="text-center py-12 bg-white rounded-lg shadow">
            <p className="text-2xl mb-2">📋</p>
            <p className="text-gray-600">No templates found for this category.</p>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {templates.map((template) => (
              <TemplateCard
                key={template.id}
                template={template}
                onSelect={handleSelectTemplate}
              />
            ))}
          </div>
        )}

        {/* Import Modal */}
        {selectedTemplate && (
          <TemplateImportModal
            template={selectedTemplate}
            onImport={handleImport}
            onClose={() => setSelectedTemplate(null)}
            importing={importing}
          />
        )}
      </div>
    </div>
  );
};

interface TemplateCardProps {
  template: WorkflowTemplate;
  onSelect: (id: string) => void;
}

const TemplateCard = ({ template, onSelect }: TemplateCardProps) => {
  const categoryColors: Record<string, string> = {
    integration: 'bg-blue-100 text-blue-800',
    automation: 'bg-green-100 text-green-800',
    monitoring: 'bg-yellow-100 text-yellow-800',
    'data-processing': 'bg-purple-100 text-purple-800'
  };

  const categoryColor = categoryColors[template.category] || 'bg-gray-100 text-gray-800';

  return (
    <div
      className="bg-white rounded-lg shadow-md hover:shadow-xl transition-shadow cursor-pointer overflow-hidden"
      onClick={() => onSelect(template.id)}
    >
      <div className="p-6">
        {/* Icon and Title */}
        <div className="flex items-start mb-3">
          <span className="text-5xl mr-4">{template.icon}</span>
          <div className="flex-1">
            <h3 className="text-xl font-semibold text-gray-900 mb-1">
              {template.name}
            </h3>
            <span className={`inline-block px-2 py-1 rounded text-xs font-medium ${categoryColor}`}>
              {template.category}
            </span>
          </div>
        </div>

        {/* Description */}
        <p className="text-sm text-gray-600 mb-4 line-clamp-3">
          {template.description}
        </p>

        {/* Tags */}
        <div className="flex flex-wrap gap-2 mb-4">
          {template.tags.slice(0, 4).map((tag) => (
            <span
              key={tag}
              className="inline-block px-2 py-1 bg-gray-100 text-gray-700 rounded text-xs"
            >
              {tag}
            </span>
          ))}
          {template.tags.length > 4 && (
            <span className="inline-block px-2 py-1 bg-gray-100 text-gray-700 rounded text-xs">
              +{template.tags.length - 4} more
            </span>
          )}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-between text-xs text-gray-500">
          <span>v{template.version}</span>
          <span>{template.parameters.length} parameters</span>
        </div>
      </div>

      {/* Hover Action */}
      <div className="bg-gray-50 px-6 py-3 border-t border-gray-200">
        <button className="text-blue-600 hover:text-blue-700 font-medium text-sm">
          View & Import →
        </button>
      </div>
    </div>
  );
};

interface TemplateImportModalProps {
  template: WorkflowTemplateDetail;
  onImport: (id: string, params: Record<string, string>) => Promise<void>;
  onClose: () => void;
  importing: boolean;
}

const TemplateImportModal = ({ template, onImport, onClose, importing }: TemplateImportModalProps) => {
  const [parameters, setParameters] = useState<Record<string, string>>({});
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    // Initialize with default values
    const defaults: Record<string, string> = {};
    template.parameters.forEach((param) => {
      if (param.default) {
        defaults[param.name] = param.default;
      }
    });
    setParameters(defaults);
  }, [template]);

  const handleParameterChange = (name: string, value: string) => {
    setParameters({ ...parameters, [name]: value });
    // Clear validation error for this field
    if (validationErrors[name]) {
      const newErrors = { ...validationErrors };
      delete newErrors[name];
      setValidationErrors(newErrors);
    }
  };

  const validateForm = (): boolean => {
    const errors: Record<string, string> = {};

    template.parameters.forEach((param) => {
      if (param.required && !parameters[param.name]?.trim()) {
        errors[param.name] = `${param.label} is required`;
      }

      // Type validation
      if (parameters[param.name]) {
        const value = parameters[param.name];
        if (param.type === 'number' && isNaN(Number(value))) {
          errors[param.name] = `${param.label} must be a number`;
        }
      }
    });

    setValidationErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!validateForm()) {
      return;
    }

    await onImport(template.id, parameters);
  };

  return (
    <div
      className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4"
      onClick={onClose}
    >
      <div
        className="bg-white rounded-lg shadow-xl max-w-3xl w-full max-h-[90vh] overflow-y-auto"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Modal Header */}
        <div className="sticky top-0 bg-white border-b border-gray-200 px-6 py-4 flex items-start justify-between">
          <div className="flex items-start">
            <span className="text-4xl mr-4">{template.icon}</span>
            <div>
              <h2 className="text-2xl font-bold text-gray-900">
                {template.name}
              </h2>
              <p className="text-sm text-gray-600 mt-1">
                {template.description}
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            disabled={importing}
            className="text-gray-400 hover:text-gray-600 text-3xl leading-none disabled:opacity-50"
          >
            ×
          </button>
        </div>

        {/* Modal Body */}
        <form onSubmit={handleSubmit} className="p-6">
          <div className="space-y-6">
            {template.parameters.length === 0 ? (
              <p className="text-gray-600 text-center py-4">
                This template has no configurable parameters.
              </p>
            ) : (
              template.parameters.map((param) => (
                <div key={param.name}>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    {param.label}
                    {param.required && <span className="text-red-500 ml-1">*</span>}
                  </label>

                  <p className="text-sm text-gray-500 mb-2">
                    {param.description}
                  </p>

                  {param.type === 'select' && param.options ? (
                    <select
                      className={`w-full border rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 focus:border-transparent ${
                        validationErrors[param.name] ? 'border-red-500' : 'border-gray-300'
                      }`}
                      value={parameters[param.name] || ''}
                      onChange={(e) => handleParameterChange(param.name, e.target.value)}
                      required={param.required}
                    >
                      <option value="">Select...</option>
                      {param.options.map((opt) => (
                        <option key={opt} value={opt}>
                          {opt}
                        </option>
                      ))}
                    </select>
                  ) : param.type === 'boolean' ? (
                    <div className="flex items-center">
                      <input
                        type="checkbox"
                        className="h-4 w-4 text-blue-600 focus:ring-blue-500 border-gray-300 rounded"
                        checked={parameters[param.name] === 'true'}
                        onChange={(e) => handleParameterChange(param.name, e.target.checked ? 'true' : 'false')}
                      />
                      <span className="ml-2 text-sm text-gray-700">Enable</span>
                    </div>
                  ) : (
                    <input
                      type={param.sensitive ? 'password' : param.type === 'number' ? 'number' : 'text'}
                      className={`w-full border rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 focus:border-transparent ${
                        validationErrors[param.name] ? 'border-red-500' : 'border-gray-300'
                      }`}
                      value={parameters[param.name] || ''}
                      onChange={(e) => handleParameterChange(param.name, e.target.value)}
                      required={param.required}
                      placeholder={param.default || ''}
                    />
                  )}

                  {validationErrors[param.name] && (
                    <p className="mt-1 text-sm text-red-600">
                      {validationErrors[param.name]}
                    </p>
                  )}
                </div>
              ))
            )}
          </div>

          {/* Modal Footer */}
          <div className="flex gap-3 mt-8 pt-6 border-t border-gray-200">
            <LoadingButton
              type="submit"
              isLoading={importing}
              loadingText="Importing..."
              className="flex-1 px-6 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 font-medium"
            >
              Import Template
            </LoadingButton>
            <LoadingButton
              type="button"
              onClick={onClose}
              isLoading={importing}
              variant="outline"
              className="px-6 py-3 bg-gray-100 text-gray-700 rounded-lg hover:bg-gray-200 font-medium"
            >
              Cancel
            </LoadingButton>
          </div>
        </form>
      </div>
    </div>
  );
};

export default N8nTemplatesPage;
