/**
 * Pipeline Builder Components
 *
 * Visual RAG pipeline builder with drag-drop canvas, plugin management,
 * configuration panels, and execution preview.
 *
 * @version 1.0.0
 * @module pipeline-builder
 */

// Main components
export { PipelineBuilder, default } from './PipelineBuilder';
export { PipelineCanvas } from './PipelineCanvas';
export { PipelineToolbar } from './PipelineToolbar';
export { PipelinePreview } from './PipelinePreview';

// Node and Edge components
export { PluginNodeComponent } from './PluginNode';
export { PipelineEdgeComponent } from './PipelineEdge';

// Panel components
export { PluginPalette } from './PluginPalette';
export { NodeConfigPanel } from './NodeConfigPanel';
export { EdgeConfigPanel } from './EdgeConfigPanel';

// Types
export type {
  PluginCategory,
  PluginDefinition,
  JsonSchema,
  JsonSchemaProperty,
  PluginNodeData,
  PluginNode,
  PipelineEdgeData,
  PipelineEdge,
  ConditionPreset,
  PipelineDefinition,
  ExecutionResult,
  ExecutionTrace,
  ExecutionStep,
  ExecutionMetrics,
  PipelineBuilderState,
  PipelineBuilderActions,
  QueryPreset,
} from './types';

// Constants
export {
  PLUGIN_CATEGORY_COLORS,
  PLUGIN_CATEGORY_ICONS,
  CONDITION_PRESETS,
  BUILT_IN_PLUGINS,
  QUERY_PRESETS,
} from './types';
