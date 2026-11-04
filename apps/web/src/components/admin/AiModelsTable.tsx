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

import { Star, Settings, ArrowUpDown } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import { Button } from '@/components/ui/primitives/button';
import type { AiModelDto } from '@/lib/api';

interface AiModelsTableProps {
  models: AiModelDto[];
  onSetPrimary: (modelId: string, modelName: string) => void;
  onConfigure: (modelId: string, model: AiModelDto) => void;
  isLoading?: boolean;
}

type SortField = 'name' | 'provider' | 'cost' | 'usage' | 'status';
type SortOrder = 'asc' | 'desc';

export function AiModelsTable({
  models,
  onSetPrimary,
  onConfigure,
  isLoading: _isLoading,
}: AiModelsTableProps) {
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
        comparison =
          a.settings.pricing.inputPricePerMillion - b.settings.pricing.inputPricePerMillion;
        break;
      case 'usage':
        comparison = a.usage.totalRequests - b.usage.totalRequests;
        break;
      case 'status':
        comparison = Number(b.isActive) - Number(a.isActive);
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
            <TableHead>
              <button
                type="button"
                className="flex items-center cursor-pointer hover:underline focus:outline-none focus:ring-2 focus:ring-primary rounded px-1"
                onClick={() => handleSort('name')}
                aria-label={`Sort by model ${sortField === 'name' ? (sortOrder === 'asc' ? 'descending' : 'ascending') : ''}`}
                aria-sort={
                  sortField === 'name' ? (sortOrder === 'asc' ? 'ascending' : 'descending') : 'none'
                }
              >
                Model
                <SortIndicator field="name" />
              </button>
            </TableHead>
            <TableHead>
              <button
                type="button"
                className="flex items-center cursor-pointer hover:underline focus:outline-none focus:ring-2 focus:ring-primary rounded px-1"
                onClick={() => handleSort('provider')}
                aria-label={`Sort by provider ${sortField === 'provider' ? (sortOrder === 'asc' ? 'descending' : 'ascending') : ''}`}
                aria-sort={
                  sortField === 'provider'
                    ? sortOrder === 'asc'
                      ? 'ascending'
                      : 'descending'
                    : 'none'
                }
              >
                Provider
                <SortIndicator field="provider" />
              </button>
            </TableHead>
            <TableHead>
              <button
                type="button"
                className="flex items-center cursor-pointer hover:underline focus:outline-none focus:ring-2 focus:ring-primary rounded px-1"
                onClick={() => handleSort('cost')}
                aria-label={`Sort by cost ${sortField === 'cost' ? (sortOrder === 'asc' ? 'descending' : 'ascending') : ''}`}
                aria-sort={
                  sortField === 'cost' ? (sortOrder === 'asc' ? 'ascending' : 'descending') : 'none'
                }
              >
                Cost/1K Tokens
                <SortIndicator field="cost" />
              </button>
            </TableHead>
            <TableHead>
              <button
                type="button"
                className="flex items-center cursor-pointer hover:underline focus:outline-none focus:ring-2 focus:ring-primary rounded px-1"
                onClick={() => handleSort('usage')}
                aria-label={`Sort by usage ${sortField === 'usage' ? (sortOrder === 'asc' ? 'descending' : 'ascending') : ''}`}
                aria-sort={
                  sortField === 'usage'
                    ? sortOrder === 'asc'
                      ? 'ascending'
                      : 'descending'
                    : 'none'
                }
              >
                Usage
                <SortIndicator field="usage" />
              </button>
            </TableHead>
            <TableHead>
              <button
                type="button"
                className="flex items-center cursor-pointer hover:underline focus:outline-none focus:ring-2 focus:ring-primary rounded px-1"
                onClick={() => handleSort('status')}
                aria-label={`Sort by status ${sortField === 'status' ? (sortOrder === 'asc' ? 'descending' : 'ascending') : ''}`}
                aria-sort={
                  sortField === 'status'
                    ? sortOrder === 'asc'
                      ? 'ascending'
                      : 'descending'
                    : 'none'
                }
              >
                Status
                <SortIndicator field="status" />
              </button>
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
            sortedModels.map(model => (
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
                  <p className="text-xs text-muted-foreground font-mono">{model.modelId}</p>
                </TableCell>

                {/* Provider */}
                <TableCell className="capitalize">{model.provider}</TableCell>

                {/* Cost */}
                <TableCell>
                  <div className="text-sm">
                    {/* #4093: il backend espone prezzi per MILIONE di token; lo schema
                        precedente diceva per mille, quindi l'etichetta era sbagliata di 1000x. */}
                    <p>In: ${model.settings.pricing.inputPricePerMillion.toFixed(2)}/1M</p>
                    <p>Out: ${model.settings.pricing.outputPricePerMillion.toFixed(2)}/1M</p>
                  </div>
                </TableCell>

                {/* Usage */}
                <TableCell>
                  <div className="text-sm">
                    <p>{model.usage.totalRequests.toLocaleString()} req</p>
                    <p className="text-muted-foreground">${model.usage.totalCostUsd.toFixed(2)}</p>
                  </div>
                </TableCell>

                {/* Status */}
                <TableCell>
                  {/* #4093: `isActive` booleano al posto di un enum a tre valori; lo stato
                      "deprecated" non esiste nel contratto del backend. */}
                  <Badge variant={model.isActive ? 'default' : 'secondary'}>
                    {model.isActive ? '✅ attivo' : '○ inattivo'}
                  </Badge>
                </TableCell>

                {/* Actions */}
                <TableCell className="text-right">
                  <div className="flex justify-end gap-2">
                    {!model.isPrimary && model.isActive && (
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => onSetPrimary(model.id, model.displayName)}
                      >
                        <Star className="h-3 w-3 mr-1" />
                        Set Primary
                      </Button>
                    )}
                    <Button variant="ghost" size="sm" onClick={() => onConfigure(model.id, model)}>
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
