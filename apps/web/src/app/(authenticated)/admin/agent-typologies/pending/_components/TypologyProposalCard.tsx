'use client';

/**
 * Typology Proposal Card Component (Issue #3181)
 *
 * Card displaying pending typology proposal with:
 * - Name, Description, Prompt preview
 * - Proposed By, Created At
 * - Checkbox for bulk selection
 * - Approve (green), Reject (red), View Details buttons
 *
 * Part of Epic #3174 (AI Agent System).
 */

import { formatDistanceToNow } from 'date-fns';
import { CheckCircle, Eye, XCircle } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardFooter, CardHeader } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import type { Typology } from '@/lib/api/schemas/agent-typologies.schemas';

interface TypologyProposalCardProps {
  typology: Typology;
  isSelected: boolean;
  onToggleSelect: (id: string) => void;
  onApprove: (typology: Typology) => void;
  onReject: (typology: Typology) => void;
  onViewDetails: (typology: Typology) => void;
}

export function TypologyProposalCard({
  typology,
  isSelected,
  onToggleSelect,
  onApprove,
  onReject,
  onViewDetails,
}: TypologyProposalCardProps) {
  const promptPreview =
    typology.basePrompt.length > 100
      ? `${typology.basePrompt.substring(0, 100)}...`
      : typology.basePrompt;

  const createdAtDistance = formatDistanceToNow(new Date(typology.createdAt), {
    addSuffix: true,
  });

  return (
    <Card className="relative hover:shadow-md transition-shadow">
      <CardHeader className="pb-3">
        <div className="flex items-start justify-between gap-2">
          <div className="flex-1 min-w-0">
            <h3 className="text-lg font-semibold truncate">{typology.name}</h3>
            <Badge variant="outline" className="mt-1">
              {typology.status}
            </Badge>
          </div>
          <Checkbox
            checked={isSelected}
            onCheckedChange={() => onToggleSelect(typology.id)}
            aria-label={`Select ${typology.name}`}
          />
        </div>
      </CardHeader>

      <CardContent className="space-y-3">
        <div>
          <p className="text-sm text-muted-foreground line-clamp-2">
            {typology.description}
          </p>
        </div>

        <div>
          <p className="text-xs font-medium text-muted-foreground mb-1">
            Prompt Preview:
          </p>
          <p className="text-xs text-muted-foreground/80 line-clamp-2 font-mono bg-muted p-2 rounded">
            {promptPreview}
          </p>
        </div>

        <div className="flex items-center justify-between text-xs text-muted-foreground">
          <span>
            <span className="font-medium">Proposed by:</span>{' '}
            {typology.createdBy}
          </span>
          <span>{createdAtDistance}</span>
        </div>
      </CardContent>

      <CardFooter className="flex gap-2 pt-3">
        <Button
          variant="default"
          size="sm"
          onClick={() => onApprove(typology)}
          className="flex-1 bg-green-600 hover:bg-green-700"
        >
          <CheckCircle className="h-4 w-4 mr-1" />
          Approve
        </Button>
        <Button
          variant="destructive"
          size="sm"
          onClick={() => onReject(typology)}
          className="flex-1"
        >
          <XCircle className="h-4 w-4 mr-1" />
          Reject
        </Button>
        <Button
          variant="ghost"
          size="sm"
          onClick={() => onViewDetails(typology)}
        >
          <Eye className="h-4 w-4" />
        </Button>
      </CardFooter>
    </Card>
  );
}
