'use client';

/**
 * Pipeline Builder Component
 *
 * Main integrated component for visual RAG pipeline building.
 * Combines canvas, palette, config panels, and preview in a unified layout.
 *
 * @version 1.0.0
 * @see Issue #3425 - Visual Pipeline Builder
 */

import { useEffect, useState, useRef } from 'react';

import { motion } from 'framer-motion';
import { PanelLeft, PanelRight, Settings2, Zap } from 'lucide-react';


import { Tabs, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { Button } from '@/components/ui/primitives/button';
import {
  ResizableHandle,
  ResizablePanel,
  ResizablePanelGroup,
} from '@/components/ui/primitives/resizable';
import { cn } from '@/lib/utils';
import { usePipelineBuilderStore } from '@/stores/pipelineBuilderStore';

import { EdgeConfigPanel } from './EdgeConfigPanel';
import { NodeConfigPanel } from './NodeConfigPanel';
import { PipelineCanvas } from './PipelineCanvas';
import { PipelinePreview } from './PipelinePreview';
import { PipelineToolbar } from './PipelineToolbar';
import { PluginPalette } from './PluginPalette';


import type { PanelImperativeHandle } from 'react-resizable-panels';

// =============================================================================
// Types
// =============================================================================

interface PipelineBuilderProps {
  className?: string;
}

// =============================================================================
// Main Component
// =============================================================================

export function PipelineBuilder({ className }: PipelineBuilderProps) {
  const [leftPanelCollapsed, setLeftPanelCollapsed] = useState(false);
  const [rightPanelCollapsed, setRightPanelCollapsed] = useState(false);
  const [rightPanelTab, setRightPanelTab] = useState<'config' | 'test'>('config');

  const leftPanelRef = useRef<PanelImperativeHandle>(null);
  const rightPanelRef = useRef<PanelImperativeHandle>(null);

  const {
    pipeline,
    selectedNodeId,
    selectedEdgeId,
    createPipeline,
  } = usePipelineBuilderStore();

  // Create default pipeline if none exists
  useEffect(() => {
    if (!pipeline) {
      createPipeline('New Pipeline', 'A new RAG pipeline');
    }
  }, [pipeline, createPipeline]);

  // Switch to config tab when node/edge selected
  useEffect(() => {
    if (selectedNodeId || selectedEdgeId) {
      setRightPanelTab('config');
      if (rightPanelCollapsed && rightPanelRef.current) {
        rightPanelRef.current.expand();
        setRightPanelCollapsed(false);
      }
    }
  }, [selectedNodeId, selectedEdgeId, rightPanelCollapsed]);

  // Panel collapse handlers
  const handleLeftPanelCollapse = () => {
    if (leftPanelRef.current) {
      leftPanelRef.current.collapse();
      setLeftPanelCollapsed(true);
    }
  };

  const handleLeftPanelExpand = () => {
    if (leftPanelRef.current) {
      leftPanelRef.current.expand();
      setLeftPanelCollapsed(false);
    }
  };

  const handleRightPanelCollapse = () => {
    if (rightPanelRef.current) {
      rightPanelRef.current.collapse();
      setRightPanelCollapsed(true);
    }
  };

  const handleRightPanelExpand = () => {
    if (rightPanelRef.current) {
      rightPanelRef.current.expand();
      setRightPanelCollapsed(false);
    }
  };

  return (
    <div className={cn('flex flex-col h-full bg-background', className)}>
      {/* Toolbar */}
      <PipelineToolbar />

      {/* Main Content */}
      <div className="flex-1 overflow-hidden">
        <ResizablePanelGroup orientation="horizontal" className="h-full">
          {/* Left Panel - Plugin Palette */}
          <ResizablePanel
            panelRef={leftPanelRef}
            defaultSize={20}
            minSize={15}
            maxSize={30}
            collapsible
            collapsedSize={0}
            onResize={(size) => setLeftPanelCollapsed(size.asPercentage === 0)}
            className={cn(
              'border-r bg-card',
              leftPanelCollapsed && 'min-w-0'
            )}
          >
            {!leftPanelCollapsed && (
              <motion.div
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                className="h-full flex flex-col"
              >
                <div className="p-2 border-b flex items-center justify-between">
                  <h3 className="font-semibold text-sm">Plugins</h3>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-6 w-6 p-0"
                    onClick={handleLeftPanelCollapse}
                  >
                    <PanelLeft className="h-4 w-4" />
                  </Button>
                </div>
                <PluginPalette className="flex-1 overflow-hidden" />
              </motion.div>
            )}
          </ResizablePanel>

          {/* Left Collapse Button */}
          {leftPanelCollapsed && (
            <div className="flex flex-col items-center py-2 px-1 border-r bg-card">
              <Button
                variant="ghost"
                size="sm"
                className="h-8 w-8 p-0"
                onClick={handleLeftPanelExpand}
              >
                <PanelLeft className="h-4 w-4" />
              </Button>
            </div>
          )}

          <ResizableHandle withHandle />

          {/* Center - Canvas */}
          <ResizablePanel defaultSize={55} minSize={40}>
            <PipelineCanvas className="h-full" />
          </ResizablePanel>

          <ResizableHandle withHandle />

          {/* Right Collapse Button */}
          {rightPanelCollapsed && (
            <div className="flex flex-col items-center py-2 px-1 border-l bg-card">
              <Button
                variant="ghost"
                size="sm"
                className="h-8 w-8 p-0"
                onClick={handleRightPanelExpand}
              >
                <PanelRight className="h-4 w-4" />
              </Button>
            </div>
          )}

          {/* Right Panel - Config & Test */}
          <ResizablePanel
            panelRef={rightPanelRef}
            defaultSize={25}
            minSize={20}
            maxSize={35}
            collapsible
            collapsedSize={0}
            onResize={(size) => setRightPanelCollapsed(size.asPercentage === 0)}
            className={cn(
              'border-l bg-card',
              rightPanelCollapsed && 'min-w-0'
            )}
          >
            {!rightPanelCollapsed && (
              <motion.div
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                className="h-full flex flex-col"
              >
                {/* Panel Header with Tabs */}
                <div className="border-b">
                  <div className="flex items-center justify-between px-2 pt-2">
                    <Tabs value={rightPanelTab} onValueChange={(v) => setRightPanelTab(v as 'config' | 'test')}>
                      <TabsList className="h-8">
                        <TabsTrigger value="config" className="text-xs h-7 gap-1.5">
                          <Settings2 className="h-3.5 w-3.5" />
                          Config
                        </TabsTrigger>
                        <TabsTrigger value="test" className="text-xs h-7 gap-1.5">
                          <Zap className="h-3.5 w-3.5" />
                          Test
                        </TabsTrigger>
                      </TabsList>
                    </Tabs>
                    <Button
                      variant="ghost"
                      size="sm"
                      className="h-6 w-6 p-0"
                      onClick={handleRightPanelCollapse}
                    >
                      <PanelRight className="h-4 w-4" />
                    </Button>
                  </div>
                </div>

                {/* Panel Content */}
                <div className="flex-1 overflow-hidden">
                  {rightPanelTab === 'config' && (
                    <>
                      {selectedNodeId ? (
                        <NodeConfigPanel className="h-full" />
                      ) : selectedEdgeId ? (
                        <EdgeConfigPanel className="h-full" />
                      ) : (
                        <div className="flex items-center justify-center h-full">
                          <div className="text-center p-6">
                            <Settings2 className="h-10 w-10 mx-auto text-muted-foreground/50 mb-3" />
                            <p className="text-sm text-muted-foreground">
                              Select a node or edge to configure
                            </p>
                          </div>
                        </div>
                      )}
                    </>
                  )}
                  {rightPanelTab === 'test' && (
                    <PipelinePreview className="h-full" />
                  )}
                </div>
              </motion.div>
            )}
          </ResizablePanel>
        </ResizablePanelGroup>
      </div>
    </div>
  );
}

// =============================================================================
// Default Export
// =============================================================================

export default PipelineBuilder;
