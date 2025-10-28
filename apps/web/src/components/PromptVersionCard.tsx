import React from 'react';
import Link from 'next/link';

interface PromptVersionCardProps {
  version: {
    id: string;
    templateId: string;
    versionNumber: number;
    content: string;
    metadata?: Record<string, any>;
    isActive: boolean;
    createdById: string;
    createdByEmail: string;
    createdAt: string;
  };
  onActivate?: () => void;
  onCompare?: () => void;
  showActions?: boolean;
}

/**
 * Component to display prompt version metadata with action buttons
 */
export default function PromptVersionCard({
  version,
  onActivate,
  onCompare,
  showActions = true,
}: PromptVersionCardProps) {
  const truncateContent = (content: string, maxLength: number = 100) => {
    if (content.length <= maxLength) return content;
    return content.substring(0, maxLength) + '...';
  };

  return (
    <div className="border border-gray-200 rounded-lg p-4 hover:shadow-md transition-shadow">
      <div className="flex items-start justify-between mb-3">
        <div className="flex items-center gap-2">
          <h3 className="text-lg font-semibold text-gray-900">
            Version {version.versionNumber}
          </h3>
          {version.isActive && (
            <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-green-100 text-green-800">
              Active
            </span>
          )}
        </div>
        {showActions && (
          <div className="flex gap-2">
            {!version.isActive && onActivate && (
              <button
                onClick={onActivate}
                className="px-3 py-1 text-sm bg-blue-600 text-white rounded-md hover:bg-blue-700 transition-colors"
              >
                Activate
              </button>
            )}
            {onCompare && (
              <button
                onClick={onCompare}
                className="px-3 py-1 text-sm bg-gray-200 text-gray-700 rounded-md hover:bg-gray-300 transition-colors"
              >
                Compare
              </button>
            )}
            <Link href={`/admin/prompts/${version.templateId}/versions/${version.id}`}>
              <button className="px-3 py-1 text-sm bg-gray-100 text-gray-700 rounded-md hover:bg-gray-200 transition-colors">
                View
              </button>
            </Link>
          </div>
        )}
      </div>

      <div className="space-y-2 text-sm text-gray-600">
        <div className="flex items-center gap-2">
          <span className="font-medium">Created by:</span>
          <span>{version.createdByEmail}</span>
        </div>
        <div className="flex items-center gap-2">
          <span className="font-medium">Created at:</span>
          <span>{new Date(version.createdAt).toLocaleString()}</span>
        </div>
        {version.metadata && Object.keys(version.metadata).length > 0 && (
          <div className="flex items-start gap-2">
            <span className="font-medium">Metadata:</span>
            <span className="text-xs bg-gray-100 px-2 py-1 rounded">
              {Object.keys(version.metadata).length} field(s)
            </span>
          </div>
        )}
      </div>

      <div className="mt-3 pt-3 border-t border-gray-200">
        <p className="text-sm text-gray-500 italic">
          {truncateContent(version.content, 150)}
        </p>
      </div>
    </div>
  );
}
