/**
 * AiModelsTable Component (Issue #2521)
 *
 * Admin table for AI models management with:
 * - Sortable columns (model, provider, cost, usage, status)
 * - Set Primary button
 * - Configure button
 * - Usage statistics display
 * - Status badges
 */

'use client';

import { useState } from 'react';

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Star, Settings, TrendingUp, ArrowUpDown } from 'lucide-react';

import type { AiModelDto } from '@/lib/api';

interface AiModelsTableProps {
  models: AiModelDto[];
  onSetPrimary: (modelId: string, modelName: string) => void;
  onConfigure: (modelId: string, model: AiModelDto) => void;
  isLoading?: boolean;
}

type SortField = 'name' | 'provider' | 'cost' | 'usage' | 'status';
type SortOrder = 'asc' | 'desc';

export function AiModelsTable({ models, onSetPrimary, onConfigure, isLoading }: AiModelsTableProps) {
  const [sortField, setSortField] = useState<SortField>('name');
  const [sortOrder, setSortOrder] = useState<SortOrder>('asc');

  // Sort handler
  const handleSort = (field: SortField) => {
    if (sortField === field) {
      setSortOrder(sortOrder === 'asc' ? 'desc' : 'asc');
    } else {
      setSortField(field);
      setSortOrder('asc');
    }
  };

  // Sort models
  const sortedModels = [...models].sort((a, b) => {
    let comparison = 0;

    switch (sortField) {
      case 'name':
        comparison = a.displayName.localeCompare(b.displayName);
        break;
      case 'provider':
        comparison = a.provider.localeCompare(b.provider);
        break;
      case 'cost':
        comparison = a.cost.inputCostPer1kTokens - b.cost.inputCostPer1kTokens;
        break;
      case 'usage':
        comparison =
          (a.usageStats?.totalRequests || 0) - (b.usageStats?.totalRequests || 0);
        break;
      case 'status':
        comparison = a.status.localeCompare(b.status);
        break;
    }

    return sortOrder === 'asc' ? comparison : -comparison;
  });

  // Render sort indicator
  const SortIndicator = ({ field }: { field: SortField }) => {
    if (sortField !== field) return <ArrowUpDown className="ml-2 h-4 w-4 text-muted-foreground" />;
    return sortOrder === 'asc' ? '↑' : '↓';
  };

  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="cursor-pointer" onClick={() => handleSort('name')}>
              <div className="flex items-center">
                Model
                <SortIndicator field="name" />
              </div>
            </TableHead>
            <TableHead className="cursor-pointer" onClick={() => handleSort('provider')}>
              <div className="flex items-center">
                Provider
                <SortIndicator field="provider" />
              </div>
            </TableHead>
            <TableHead className="cursor-pointer" onClick={() => handleSort('cost')}>
              <div className="flex items-center">
                Cost/1K Tokens
                <SortIndicator field="cost" />
              </div>
            </TableHead>
            <TableHead className="cursor-pointer" onClick={() => handleSort('usage')}>
              <div className="flex items-center">
                Usage
                <SortIndicator field="usage" />
              </div>
            </TableHead>
            <TableHead className="cursor-pointer" onClick={() => handleSort('status')}>
              <div className="flex items-center">
                Status
                <SortIndicator field="status" />
              </div>
            </TableHead>
            <TableHead className="text-right">Actions</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {sortedModels.length === 0 ? (
            <TableRow>
              <TableCell colSpan={6} className="text-center text-muted-foreground py-8">
                No models available
              </TableCell>
            </TableRow>
          ) : (
            sortedModels.map((model) => (
              <TableRow key={model.id} className={model.isPrimary ? 'bg-primary/5' : ''}>
                {/* Model Name */}
                <TableCell className="font-medium">
                  <div className="flex items-center gap-2">
                    {model.displayName}
                    {model.isPrimary && (
                      <Badge variant="default" className="text-xs">
                        <Star className="h-3 w-3 mr-1" />
                        Primary
                      </Badge>
                    )}
                  </div>
                  <p className="text-xs text-muted-foreground font-mono">{model.modelIdentifier}</p>
                </TableCell>

                {/* Provider */}
                <TableCell className="capitalize">{model.provider}</TableCell>

                {/* Cost */}
                <TableCell>
                  <div className="text-sm">
                    <p>In: ${model.cost.inputCostPer1kTokens.toFixed(2)}</p>
                    <p>Out: ${model.cost.outputCostPer1kTokens.toFixed(2)}</p>
                  </div>
                </TableCell>

                {/* Usage */}
                <TableCell>
                  {model.usageStats ? (
                    <div className="text-sm">
                      <p>{model.usageStats.totalRequests.toLocaleString()} req</p>
                      <p className="text-muted-foreground">
                        ${model.usageStats.estimatedCost.toFixed(2)}
                      </p>
                    </div>
                  ) : (
                    <span className="text-muted-foreground text-sm">No usage</span>
                  )}
                </TableCell>

                {/* Status */}
                <TableCell>
                  <Badge
                    variant={
                      model.status === 'active'
                        ? 'default'
                        : model.status === 'deprecated'
                          ? 'destructive'
                          : 'secondary'
                    }
                  >
                    {model.status === 'active' ? '✅' : model.status === 'deprecated' ? '⚠️' : '○'}{' '}
                    {model.status}
                  </Badge>
                </TableCell>

                {/* Actions */}
                <TableCell className="text-right">
                  <div className="flex justify-end gap-2">
                    {!model.isPrimary && model.status === 'active' && (
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => onSetPrimary(model.id, model.displayName)}
                      >
                        <Star className="h-3 w-3 mr-1" />
                        Set Primary
                      </Button>
                    )}
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => onConfigure(model.id, model)}
                    >
                      <Settings className="h-3 w-3" />
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </div>
  );
}
