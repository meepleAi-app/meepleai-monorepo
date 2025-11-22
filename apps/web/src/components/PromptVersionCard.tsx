import React from 'react';
import Link from 'next/link';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';

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
    <Card className="hover:shadow-md transition-shadow">
      <CardHeader>
        <div className="flex items-start justify-between">
          <div className="flex items-center gap-2">
            <CardTitle className="text-lg">
              Version {version.versionNumber}
            </CardTitle>
            {version.isActive && (
              <Badge variant="default" className="bg-green-100 text-green-800 hover:bg-green-100/80 border-transparent">
                Active
              </Badge>
            )}
          </div>
          {showActions && (
            <div className="flex gap-2">
              {!version.isActive && onActivate && (
                <Button
                  onClick={onActivate}
                  size="sm"
                  variant="default"
                >
                  Activate
                </Button>
              )}
              {onCompare && (
                <Button
                  onClick={onCompare}
                  size="sm"
                  variant="secondary"
                >
                  Compare
                </Button>
              )}
              <Link href={`/admin/prompts/${version.templateId}/versions/${version.id}`}>
                <Button size="sm" variant="outline">
                  View
                </Button>
              </Link>
            </div>
          )}
        </div>
      </CardHeader>

      <CardContent>
        <div className="space-y-2 text-sm text-muted-foreground">
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
              <Badge variant="secondary" className="text-xs">
                {Object.keys(version.metadata).length} field(s)
              </Badge>
            </div>
          )}
        </div>

        <div className="mt-3 pt-3 border-t border-border">
          <p className="text-sm text-muted-foreground italic">
            {truncateContent(version.content, 150)}
          </p>
        </div>
      </CardContent>
    </Card>
  );
}
