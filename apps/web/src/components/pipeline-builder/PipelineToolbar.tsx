'use client';

/**
 * Pipeline Toolbar Component
 *
 * Toolbar for pipeline operations: save, load, validate, layout controls.
 *
 * @version 1.0.0
 * @see Issue #3425 - Visual Pipeline Builder
 */

import { useState, useCallback } from 'react';

import {
  Save,
  FolderOpen,
  CheckCircle,
  AlertTriangle,
  Undo2,
  Redo2,
  Layout,
  Maximize2,
  Lock,
  Unlock,
  Grid3X3,
  Map,
  Plus,
  Trash2,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Separator } from '@/components/ui/navigation/separator';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/overlays/dialog';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { cn } from '@/lib/utils';
import { usePipelineBuilderStore } from '@/stores/pipelineBuilderStore';

// =============================================================================
// Types
// =============================================================================

interface PipelineToolbarProps {
  className?: string;
}

// =============================================================================
// Toolbar Button
// =============================================================================

interface ToolbarButtonProps {
  icon: React.ReactNode;
  label: string;
  onClick: () => void;
  disabled?: boolean;
  active?: boolean;
  variant?: 'default' | 'destructive';
}

function ToolbarButton({
  icon,
  label,
  onClick,
  disabled,
  active,
  variant = 'default',
}: ToolbarButtonProps) {
  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <Button
            variant={variant === 'destructive' ? 'destructive' : active ? 'secondary' : 'ghost'}
            size="sm"
            onClick={onClick}
            disabled={disabled}
            className={cn('h-8 w-8 p-0', active && 'bg-muted')}
          >
            {icon}
          </Button>
        </TooltipTrigger>
        <TooltipContent side="bottom">
          <p className="text-xs">{label}</p>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}

// =============================================================================
// Main Component
// =============================================================================

export function PipelineToolbar({ className }: PipelineToolbarProps) {
  const [newPipelineName, setNewPipelineName] = useState('');
  const [isNewDialogOpen, setIsNewDialogOpen] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  const {
    pipeline,
    isDirty,
    showMiniMap,
    showGrid,
    isLocked,
    createPipeline,
    savePipeline,
    clearPipeline,
    validatePipeline,
    autoLayout,
    fitView,
    toggleMiniMap,
    toggleGrid,
    toggleLock,
    undo,
    redo,
    canUndo,
    canRedo,
  } = usePipelineBuilderStore();

  const handleNew = useCallback(() => {
    if (newPipelineName.trim()) {
      createPipeline(newPipelineName.trim());
      setNewPipelineName('');
      setIsNewDialogOpen(false);
    }
  }, [newPipelineName, createPipeline]);

  const handleSave = useCallback(async () => {
    setIsSaving(true);
    await savePipeline();
    setIsSaving(false);
  }, [savePipeline]);

  const handleValidate = useCallback(() => {
    validatePipeline();
  }, [validatePipeline]);

  const handleClear = useCallback(() => {
    if (confirm('Are you sure you want to clear the pipeline? This cannot be undone.')) {
      clearPipeline();
    }
  }, [clearPipeline]);

  const isValid = pipeline?.isValid ?? true;
  const nodeCount = pipeline?.nodes.length ?? 0;
  const edgeCount = pipeline?.edges.length ?? 0;

  return (
    <div
      className={cn(
        'flex items-center gap-1 px-2 py-1.5 bg-card border-b',
        className
      )}
    >
      {/* File Operations */}
      <div className="flex items-center gap-1">
        {/* New Pipeline */}
        <Dialog open={isNewDialogOpen} onOpenChange={setIsNewDialogOpen}>
          <DialogTrigger asChild>
            <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
              <Plus className="h-4 w-4" />
            </Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>New Pipeline</DialogTitle>
              <DialogDescription>
                Create a new RAG pipeline. Enter a name for your pipeline.
              </DialogDescription>
            </DialogHeader>
            <Input
              value={newPipelineName}
              onChange={(e) => setNewPipelineName(e.target.value)}
              placeholder="Pipeline name..."
              onKeyDown={(e) => e.key === 'Enter' && handleNew()}
            />
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsNewDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleNew} disabled={!newPipelineName.trim()}>
                Create
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        <ToolbarButton
          icon={<FolderOpen className="h-4 w-4" />}
          label="Load Pipeline"
          onClick={() => {}}
          disabled
        />

        <ToolbarButton
          icon={isSaving ? <span className="animate-spin">⏳</span> : <Save className="h-4 w-4" />}
          label={isDirty ? 'Save (unsaved changes)' : 'Save'}
          onClick={handleSave}
          disabled={!pipeline || isSaving}
        />
      </div>

      <Separator orientation="vertical" className="h-6 mx-1" />

      {/* Edit Operations */}
      <div className="flex items-center gap-1">
        <ToolbarButton
          icon={<Undo2 className="h-4 w-4" />}
          label="Undo"
          onClick={undo}
          disabled={!canUndo()}
        />
        <ToolbarButton
          icon={<Redo2 className="h-4 w-4" />}
          label="Redo"
          onClick={redo}
          disabled={!canRedo()}
        />
      </div>

      <Separator orientation="vertical" className="h-6 mx-1" />

      {/* Validation */}
      <div className="flex items-center gap-1">
        <ToolbarButton
          icon={
            isValid ? (
              <CheckCircle className="h-4 w-4 text-green-500" />
            ) : (
              <AlertTriangle className="h-4 w-4 text-amber-500" />
            )
          }
          label={isValid ? 'Pipeline is valid' : 'Validate Pipeline'}
          onClick={handleValidate}
          disabled={!pipeline}
        />
      </div>

      <Separator orientation="vertical" className="h-6 mx-1" />

      {/* Layout Controls */}
      <div className="flex items-center gap-1">
        <ToolbarButton
          icon={<Layout className="h-4 w-4" />}
          label="Auto Layout"
          onClick={autoLayout}
          disabled={!pipeline || nodeCount === 0}
        />
        <ToolbarButton
          icon={<Maximize2 className="h-4 w-4" />}
          label="Fit View"
          onClick={fitView}
          disabled={!pipeline || nodeCount === 0}
        />
      </div>

      <Separator orientation="vertical" className="h-6 mx-1" />

      {/* View Controls */}
      <div className="flex items-center gap-1">
        <ToolbarButton
          icon={<Grid3X3 className="h-4 w-4" />}
          label={showGrid ? 'Hide Grid' : 'Show Grid'}
          onClick={toggleGrid}
          active={showGrid}
        />
        <ToolbarButton
          icon={<Map className="h-4 w-4" />}
          label={showMiniMap ? 'Hide Mini Map' : 'Show Mini Map'}
          onClick={toggleMiniMap}
          active={showMiniMap}
        />
        <ToolbarButton
          icon={isLocked ? <Lock className="h-4 w-4" /> : <Unlock className="h-4 w-4" />}
          label={isLocked ? 'Unlock Canvas' : 'Lock Canvas'}
          onClick={toggleLock}
          active={isLocked}
        />
      </div>

      {/* Spacer */}
      <div className="flex-1" />

      {/* Pipeline Info */}
      {pipeline && (
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <span className="font-medium truncate max-w-[150px]">{pipeline.name}</span>
          <Badge variant="secondary" className="text-[10px]">
            {nodeCount} node{nodeCount !== 1 ? 's' : ''}
          </Badge>
          <Badge variant="secondary" className="text-[10px]">
            {edgeCount} edge{edgeCount !== 1 ? 's' : ''}
          </Badge>
          {isDirty && (
            <Badge variant="outline" className="text-[10px] text-amber-500 border-amber-500/50">
              Unsaved
            </Badge>
          )}
          {!isValid && (
            <Badge variant="destructive" className="text-[10px]">
              Invalid
            </Badge>
          )}
        </div>
      )}

      <Separator orientation="vertical" className="h-6 mx-1" />

      {/* Danger Zone */}
      <ToolbarButton
        icon={<Trash2 className="h-4 w-4" />}
        label="Clear Pipeline"
        onClick={handleClear}
        disabled={!pipeline}
        variant="destructive"
      />
    </div>
  );
}
