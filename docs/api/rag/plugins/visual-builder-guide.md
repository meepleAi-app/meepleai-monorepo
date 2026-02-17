# Visual Pipeline Builder Guide

> **Creating RAG Pipelines with the Drag-and-Drop Interface**

The Visual Pipeline Builder provides an intuitive interface for constructing RAG pipelines without writing code. This guide covers all features of the builder UI.

## Overview

The Visual Pipeline Builder consists of four main areas:

```
┌──────────────────────────────────────────────────────────────────────────┐
│  Header: Pipeline name, save, test, export actions                       │
├─────────────┬────────────────────────────────────────────┬───────────────┤
│             │                                            │               │
│   Plugin    │              Canvas Area                   │    Node       │
│   Palette   │         (React Flow diagram)               │    Config     │
│             │                                            │    Panel      │
│  - Routing  │     ┌─────┐        ┌─────┐                │               │
│  - Cache    │     │Node1│───────▶│Node2│                │  Properties   │
│  - Retrieval│     └─────┘        └─────┘                │  Config       │
│  - etc.     │                        │                   │  Validation   │
│             │                        ▼                   │               │
│             │                   ┌─────┐                  │               │
│             │                   │Node3│                  │               │
│             │                   └─────┘                  │               │
│             │                                            │               │
├─────────────┴────────────────────────────────────────────┴───────────────┤
│  Status Bar: Validation status, node count, zoom level                    │
└──────────────────────────────────────────────────────────────────────────┘
```

## Getting Started

### Creating a New Pipeline

1. Navigate to **Admin → RAG Pipelines → Create New**
2. Enter pipeline details:
   - **Name**: Human-readable identifier
   - **ID**: Auto-generated or custom (lowercase, hyphens)
   - **Description**: Purpose and behavior
   - **Category**: rules, strategy, general, custom

### Opening an Existing Pipeline

1. Navigate to **Admin → RAG Pipelines**
2. Click on a pipeline card to open in the builder
3. Or use the **Edit** button in the pipeline list

---

## Plugin Palette

The left sidebar contains all available plugins organized by category.

### Categories

| Icon | Category | Purpose |
|------|----------|---------|
| 🚦 | Routing | Query classification and path determination |
| 💾 | Cache | Result caching and retrieval |
| 🔍 | Retrieval | Document fetching from vector stores |
| 📊 | Evaluation | Quality assessment and scoring |
| ✨ | Generation | Response creation with LLMs |
| ✅ | Validation | Output verification and guardrails |
| 🔄 | Transform | Data modification and enrichment |
| 🎯 | Filter | Document selection and removal |

### Plugin Cards

Each plugin card displays:
- **Name**: Plugin display name
- **Version**: Semantic version
- **Description**: Brief functionality summary
- **Tags**: Searchable keywords

### Searching Plugins

Use the search bar to filter plugins by:
- Name
- Description
- Tags
- Category

```
🔍 Search: "vector"
Results:
  - Vector Similarity Retrieval (retrieval-vector-v1)
  - Hybrid Vector Search (retrieval-hybrid-v1)
```

---

## Canvas Operations

### Adding Nodes

**Drag and Drop**:
1. Click and hold a plugin in the palette
2. Drag onto the canvas
3. Release to place the node

**Double-Click**:
1. Double-click a plugin in the palette
2. Node appears at canvas center

### Selecting Nodes

- **Single select**: Click on a node
- **Multi-select**: Ctrl/Cmd + Click additional nodes
- **Box select**: Click and drag on canvas background
- **Select all**: Ctrl/Cmd + A

### Moving Nodes

- **Drag**: Click and drag selected node(s)
- **Nudge**: Arrow keys move selected nodes
- **Snap to grid**: Hold Shift while dragging

### Deleting Nodes

- **Delete key**: Remove selected node(s)
- **Backspace**: Remove selected node(s)
- **Context menu**: Right-click → Delete

### Connecting Nodes

1. Hover over a node's output handle (right side)
2. Click and drag to target node's input handle (left side)
3. Release to create connection

```
┌─────────┐         ┌─────────┐
│  Node A │○───────▶●│  Node B │
│         │  drag    │         │
└─────────┘         └─────────┘
        ○ = output handle
        ● = input handle
```

### Edge Operations

**Select Edge**: Click on the edge line

**Delete Edge**:
- Select + Delete key
- Right-click → Delete

**Edit Condition**:
- Double-click edge
- Or select + use config panel

---

## Node Configuration Panel

When a node is selected, the right panel displays configuration options.

### Node Properties

**Basic Info** (read-only):
- Plugin ID
- Plugin Version
- Category

**Editable**:
- **Display Name**: Custom label
- **Enabled**: Toggle node active state
- **Timeout**: Override default timeout

### Plugin Configuration

Configuration fields are generated from the plugin's ConfigSchema:

```
┌─────────────────────────────────────┐
│ Vector Retrieval Configuration      │
├─────────────────────────────────────┤
│ Top K Results         [10    ] ▼    │
│ Similarity Threshold  [0.7   ]      │
│ Include Metadata      [✓]           │
│ Namespace            [rules  ] ▼    │
│                                     │
│ Advanced ▼                          │
│ ┌─────────────────────────────────┐ │
│ │ Embedding Model   [ada-002  ] ▼│ │
│ │ Score Boost       [1.0      ]  │ │
│ └─────────────────────────────────┘ │
└─────────────────────────────────────┘
```

### Field Types

| Schema Type | UI Component |
|-------------|--------------|
| `string` | Text input |
| `string` + `enum` | Dropdown select |
| `number` / `integer` | Number input |
| `boolean` | Toggle switch |
| `array` | Multi-select or list editor |
| `object` | Nested form group |

### Validation

Real-time validation against ConfigSchema:
- ✅ Green border: Valid
- ⚠️ Yellow border: Warning
- ❌ Red border: Error with message

---

## Edge Configuration Panel

When an edge is selected, configure routing conditions.

### Condition Editor

```
┌─────────────────────────────────────┐
│ Edge Configuration                  │
├─────────────────────────────────────┤
│ Label: [Rules Path          ]       │
│                                     │
│ Condition:                          │
│ ┌─────────────────────────────────┐ │
│ │output.result.queryType === 'rules│ │
│ │'                                 │ │
│ └─────────────────────────────────┘ │
│                                     │
│ Quick Conditions:                   │
│ [By Query Type ▼] [Add]             │
│                                     │
│ Data Transform: (optional)          │
│ ┌─────────────────────────────────┐ │
│ │                                 │ │
│ └─────────────────────────────────┘ │
└─────────────────────────────────────┘
```

### Quick Condition Templates

Pre-built condition templates:

| Template | Condition |
|----------|-----------|
| By Query Type | `output.result.queryType === '${value}'` |
| By Confidence | `output.confidence >= ${threshold}` |
| Cache Hit | `output.result.cacheHit === true` |
| Has Documents | `output.result.documents.length > 0` |
| Relevance Score | `output.result.relevanceScore >= ${threshold}` |

### Condition Syntax Help

Available variables in conditions:

```javascript
output: {
  success: boolean,
  result: { /* plugin output */ },
  confidence: number,
  errorCode?: string
}

input: {
  query: string,
  gameId?: string,
  userId?: string
}

context: {
  variables: { /* pipeline variables */ }
}
```

### Condition Testing

Test conditions with sample data:

1. Click "Test Condition"
2. Enter sample output JSON
3. See evaluation result (true/false)

---

## Canvas Controls

### Zoom and Pan

| Action | Mouse | Keyboard |
|--------|-------|----------|
| Zoom in | Scroll up | Ctrl/Cmd + Plus |
| Zoom out | Scroll down | Ctrl/Cmd + Minus |
| Pan | Middle-click drag | Space + drag |
| Fit to view | - | Ctrl/Cmd + 0 |
| Reset zoom | - | Ctrl/Cmd + 1 |

### Mini-Map

Toggle mini-map with the map icon in the toolbar:
- Shows entire pipeline overview
- Click to navigate to area
- Drag viewport rectangle

### Grid and Snapping

Toggle options in View menu:
- **Show Grid**: Display alignment grid
- **Snap to Grid**: Align nodes to grid
- **Show Edge Labels**: Display condition labels

---

## Pipeline Actions

### Toolbar

```
┌────────────────────────────────────────────────────────────┐
│ [💾 Save] [▶️ Test] [📋 Validate] [📤 Export] [⚙️ Settings] │
└────────────────────────────────────────────────────────────┘
```

### Save Pipeline

- **Ctrl/Cmd + S**: Quick save
- Validates before saving
- Shows save confirmation

### Validate Pipeline

Runs all validation checks:
- ✅ No cycles (acyclic graph)
- ✅ All nodes connected
- ✅ Valid plugin references
- ✅ Valid conditions
- ✅ Configuration validity

### Test Pipeline

Opens the Pipeline Preview/Test panel:

1. **Input Tab**: Enter test query
2. **Execute**: Run pipeline
3. **Results Tab**: View node-by-node execution
4. **Metrics Tab**: See timing and performance

See [Testing Pipelines](#testing-pipelines) for details.

### Export Pipeline

Export options:
- **JSON**: Pipeline definition file
- **Copy to Clipboard**: JSON for sharing
- **Template**: Save as reusable template

---

## Testing Pipelines

### Preview Panel

The preview panel shows real-time pipeline testing:

```
┌─────────────────────────────────────────────────────────────┐
│ Pipeline Preview                                    [Close] │
├─────────────────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ Query: How do I set up Catan?                          │ │
│ │ Game ID: [dropdown or input]                           │ │
│ │                                          [▶️ Execute]   │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                             │
│ Execution Flow:                                             │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ ✅ router (45ms)                                        │ │
│ │    └─ queryType: "rules", confidence: 0.92             │ │
│ │                                                         │ │
│ │ ✅ rules-retrieval (234ms)                              │ │
│ │    └─ 8 documents retrieved, avg score: 0.84           │ │
│ │                                                         │ │
│ │ ✅ evaluation (67ms)                                    │ │
│ │    └─ relevance: 0.87, all documents relevant          │ │
│ │                                                         │ │
│ │ ✅ generation (1,234ms)                                 │ │
│ │    └─ 342 tokens generated                             │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                             │
│ Final Output:                                               │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ "To set up Catan, first place the hexagonal tiles..."  │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                             │
│ Total Time: 1,580ms | Nodes: 4/4 | Confidence: 0.89        │
└─────────────────────────────────────────────────────────────┘
```

### Execution Visualization

During execution, the canvas shows:
- **Pulsing nodes**: Currently executing
- **Green nodes**: Completed successfully
- **Red nodes**: Failed
- **Gray nodes**: Skipped (condition false)

### Debugging Failed Nodes

Click on a failed node to see:
- Error message
- Error code
- Stack trace (if available)
- Input received
- Configuration used

---

## Keyboard Shortcuts

### General

| Action | Shortcut |
|--------|----------|
| Save | Ctrl/Cmd + S |
| Undo | Ctrl/Cmd + Z |
| Redo | Ctrl/Cmd + Shift + Z |
| Select All | Ctrl/Cmd + A |
| Deselect | Escape |
| Delete Selected | Delete / Backspace |

### Navigation

| Action | Shortcut |
|--------|----------|
| Zoom In | Ctrl/Cmd + Plus |
| Zoom Out | Ctrl/Cmd + Minus |
| Fit to View | Ctrl/Cmd + 0 |
| Reset Zoom | Ctrl/Cmd + 1 |
| Pan | Space + Drag |

### Canvas

| Action | Shortcut |
|--------|----------|
| Copy Node | Ctrl/Cmd + C |
| Paste Node | Ctrl/Cmd + V |
| Duplicate | Ctrl/Cmd + D |
| Toggle Grid | G |
| Toggle Snap | Shift + G |

---

## Best Practices

### Pipeline Design

1. **Start with entry point**: Begin with routing or cache node
2. **Flow left to right**: Organize for readability
3. **Use meaningful names**: Rename nodes for clarity
4. **Document conditions**: Add labels to conditional edges
5. **Test incrementally**: Add nodes one at a time, test each

### Performance

1. **Add caching early**: Cache nodes before expensive operations
2. **Limit branching**: Too many parallel paths increases complexity
3. **Set appropriate timeouts**: Consider cumulative execution time
4. **Monitor execution times**: Use preview panel to identify bottlenecks

### Maintainability

1. **Use consistent spacing**: Align nodes in a grid
2. **Group related nodes**: Keep similar functionality together
3. **Comment with labels**: Edge labels explain routing logic
4. **Version pipelines**: Use semantic versioning for changes

---

## Troubleshooting

### Common Issues

**Nodes won't connect**:
- Check that you're dragging from output (right) to input (left)
- Verify the connection creates a valid DAG (no cycles)

**Validation fails**:
- Check for orphan nodes (not connected to pipeline)
- Verify all conditional edges have an "else" path
- Ensure plugin IDs are valid

**Test execution fails**:
- Check node configurations for missing required fields
- Verify external services are available (vector store, LLM)
- Review error messages in the execution panel

**Canvas is slow**:
- Try reducing zoom level
- Hide mini-map for large pipelines
- Close preview panel when not in use

### Getting Help

- **Hover tooltips**: Hover over icons for descriptions
- **Field help**: Click (?) next to configuration fields
- **Documentation**: Link in Settings menu
- **Support**: Contact team via support channel

---

## Related Documentation

- [Pipeline Definition Schema](pipeline-definition.md) - JSON structure reference
- [Plugin Development Guide](plugin-development-guide.md) - Creating custom plugins
- [Plugin Contract](plugin-contract.md) - Plugin interface specification
- [Testing Guide](testing-guide.md) - Testing plugins and pipelines
