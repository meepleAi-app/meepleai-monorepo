/**
 * Resource Editor - Issue #2420
 *
 * Editor for managing game resources: tokens, cards, and other resources.
 *
 * Features:
 * - Add/remove resources
 * - Edit resource type, name, and quantity
 * - Assign resources to players (optional)
 * - Validation for quantity (>= 0)
 * - Supports readonly mode
 * - Visual grouping by resource type
 */

'use client';

import { Coins, CreditCard, Plus, Package, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Card, CardContent } from '@/components/ui/data-display/card';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';

import type { PlayerState, ResourceState } from './StateEditor';

// ========== Component Props ==========

export interface ResourceEditorProps {
  /** Current resources list */
  resources: ResourceState[];
  /** Players for assignment */
  players: PlayerState[];
  /** Callback on resources change */
  onChange: (resources: ResourceState[]) => void;
  /** Read-only mode */
  readonly?: boolean;
  /** Validation errors map (path -> error message) */
  validationErrors?: Record<string, string>;
}

// ========== Component ==========

export function ResourceEditor({
  resources,
  players,
  onChange,
  readonly = false,
  validationErrors = {},
}: ResourceEditorProps) {
  /**
   * Add new resource
   */
  const handleAddResource = () => {
    const newResource: ResourceState = {
      id: crypto.randomUUID(),
      type: 'token',
      name: `Risorsa ${resources.length + 1}`,
      quantity: 1,
    };
    onChange([...resources, newResource]);
  };

  /**
   * Remove resource
   */
  const handleRemoveResource = (id: string) => {
    onChange(resources.filter(r => r.id !== id));
  };

  /**
   * Update resource field
   */
  const handleUpdateResource = (
    id: string,
    field: keyof ResourceState,
    value: string | number | undefined
  ) => {
    onChange(
      resources.map(r =>
        r.id === id
          ? {
              ...r,
              [field]: value,
            }
          : r
      )
    );
  };

  /**
   * Get validation error for specific resource field
   */
  const getFieldError = (index: number, field: string): string | undefined => {
    return validationErrors[`resources.${index}.${field}`];
  };

  /**
   * Get icon for resource type
   */
  const getResourceIcon = (type: ResourceState['type']) => {
    switch (type) {
      case 'token':
        return <Coins className="h-5 w-5" />;
      case 'card':
        return <CreditCard className="h-5 w-5" />;
      case 'resource':
        return <Package className="h-5 w-5" />;
    }
  };

  /**
   * Group resources by type
   */
  const _groupedResources = resources.reduce(
    (acc, resource) => {
      if (!acc[resource.type]) acc[resource.type] = [];
      acc[resource.type].push(resource);
      return acc;
    },
    {} as Record<ResourceState['type'], ResourceState[]>
  );

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-lg font-semibold">Risorse</h3>
          <p className="text-sm text-muted-foreground">
            Gestisci token, carte e altre risorse di gioco
          </p>
        </div>
        {!readonly && (
          <Button onClick={handleAddResource} size="sm" variant="outline">
            <Plus className="mr-2 h-4 w-4" />
            Aggiungi Risorsa
          </Button>
        )}
      </div>

      {resources.length === 0 ? (
        <Card className="border-dashed">
          <CardContent className="flex flex-col items-center justify-center py-8">
            <Package className="mb-2 h-12 w-12 text-muted-foreground" />
            <p className="text-sm text-muted-foreground">Nessuna risorsa aggiunta</p>
            {!readonly && (
              <Button onClick={handleAddResource} size="sm" variant="ghost" className="mt-2">
                Aggiungi la prima risorsa
              </Button>
            )}
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-3">
          {resources.map((resource, index) => (
            <Card key={resource.id} className="p-4">
              <div className="flex items-start gap-4">
                <div className="mt-2 text-muted-foreground">{getResourceIcon(resource.type)}</div>

                <div className="flex-1 space-y-3">
                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <Label htmlFor={`resource-type-${resource.id}`}>Tipo</Label>
                      <Select
                        value={resource.type}
                        onValueChange={value =>
                          handleUpdateResource(resource.id, 'type', value as ResourceState['type'])
                        }
                        disabled={readonly}
                      >
                        <SelectTrigger id={`resource-type-${resource.id}`}>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="token">Token</SelectItem>
                          <SelectItem value="card">Carta</SelectItem>
                          <SelectItem value="resource">Risorsa</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>

                    <div>
                      <Label htmlFor={`resource-name-${resource.id}`}>Nome</Label>
                      <Input
                        id={`resource-name-${resource.id}`}
                        value={resource.name}
                        onChange={e => handleUpdateResource(resource.id, 'name', e.target.value)}
                        disabled={readonly}
                        className={getFieldError(index, 'name') ? 'border-red-500' : ''}
                      />
                      {getFieldError(index, 'name') && (
                        <p className="mt-1 text-sm text-red-500">{getFieldError(index, 'name')}</p>
                      )}
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <Label htmlFor={`resource-quantity-${resource.id}`}>Quantità</Label>
                      <Input
                        id={`resource-quantity-${resource.id}`}
                        type="number"
                        value={resource.quantity}
                        onChange={e =>
                          handleUpdateResource(
                            resource.id,
                            'quantity',
                            parseInt(e.target.value) || 0
                          )
                        }
                        disabled={readonly}
                        min={0}
                        className={getFieldError(index, 'quantity') ? 'border-red-500' : ''}
                      />
                      {getFieldError(index, 'quantity') && (
                        <p className="mt-1 text-sm text-red-500">
                          {getFieldError(index, 'quantity')}
                        </p>
                      )}
                    </div>

                    <div>
                      <Label htmlFor={`resource-owner-${resource.id}`}>
                        Proprietario (opzionale)
                      </Label>
                      <Select
                        value={resource.ownerId || 'none'}
                        onValueChange={value =>
                          handleUpdateResource(
                            resource.id,
                            'ownerId',
                            value === 'none' ? undefined : value
                          )
                        }
                        disabled={readonly}
                      >
                        <SelectTrigger id={`resource-owner-${resource.id}`}>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="none">Nessuno</SelectItem>
                          {players.map(player => (
                            <SelectItem key={player.id} value={player.id}>
                              {player.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                  </div>
                </div>

                {!readonly && (
                  <Button
                    onClick={() => handleRemoveResource(resource.id)}
                    variant="ghost"
                    size="icon"
                    className="text-red-500 hover:bg-red-50 hover:text-red-600"
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                )}
              </div>
            </Card>
          ))}
        </div>
      )}

      <div className="rounded-md border border-blue-200 bg-blue-50 p-3 text-sm text-blue-800">
        <strong>Risorse totali:</strong> {resources.length}
        {resources.length > 0 && (
          <>
            {' '}
            • <strong>Quantità totale:</strong> {resources.reduce((sum, r) => sum + r.quantity, 0)}
          </>
        )}
      </div>
    </div>
  );
}
