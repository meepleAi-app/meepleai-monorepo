import type { Meta, StoryObj } from '@storybook/react';
import { DiffCodeBlock } from './DiffCodeBlock';
import { DiffLine } from '@/lib/diffProcessor';

/**
 * DiffCodeBlock - Individual line in diff view with syntax highlighting.
 * Memoized to prevent unnecessary re-renders on large diffs.
 * Shows added, removed, or unchanged lines with syntax highlighting.
 */
const meta = {
  title: 'Diff/DiffCodeBlock',
  component: DiffCodeBlock,
  parameters: {
    layout: 'padded',
  },
  tags: ['autodocs'],
  argTypes: {
    isHighlighted: {
      control: 'boolean',
      description: 'Whether this line is highlighted (search match)',
    },
    searchQuery: {
      control: 'text',
      description: 'Search query for highlighting',
    },
  },
} satisfies Meta<typeof DiffCodeBlock>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Unchanged line
 */
export const UnchangedLine: Story = {
  args: {
    line: {
      type: 'unchanged',
      content: '  "name": "meepleai-web",',
      lineNumber: 3,
    },
    isHighlighted: false,
    searchQuery: '',
  },
};

/**
 * Added line (new content)
 */
export const AddedLine: Story = {
  args: {
    line: {
      type: 'added',
      content: '  "version": "2.0.0",',
      lineNumber: 4,
    },
    isHighlighted: false,
    searchQuery: '',
  },
};

/**
 * Removed line (deleted content)
 */
export const RemovedLine: Story = {
  args: {
    line: {
      type: 'removed',
      content: '  "version": "1.0.0",',
      lineNumber: 4,
    },
    isHighlighted: false,
    searchQuery: '',
  },
};

/**
 * Highlighted line (search match)
 */
export const HighlightedLine: Story = {
  args: {
    line: {
      type: 'added',
      content: '  "react": "^19.0.0",',
      lineNumber: 10,
    },
    isHighlighted: true,
    searchQuery: 'react',
  },
};

/**
 * Empty placeholder line
 */
export const EmptyPlaceholder: Story = {
  args: {
    line: {
      type: 'unchanged',
      content: '',
      lineNumber: null,
    },
    isHighlighted: false,
    searchQuery: '',
  },
};

/**
 * Long line with code
 */
export const LongLine: Story = {
  args: {
    line: {
      type: 'added',
      content: '  "dependencies": { "react": "^19.0.0", "react-dom": "^19.0.0", "next": "^16.0.0", "typescript": "^5.3.0" }',
      lineNumber: 15,
    },
    isHighlighted: false,
    searchQuery: '',
  },
};

/**
 * Complete diff example (multiple lines)
 */
export const DiffExample: Story = {
  render: () => {
    const lines: DiffLine[] = [
      { type: 'unchanged', content: '{', lineNumber: 1 },
      { type: 'unchanged', content: '  "name": "meepleai-web",', lineNumber: 2 },
      { type: 'removed', content: '  "version": "1.0.0",', lineNumber: 3 },
      { type: 'added', content: '  "version": "2.0.0",', lineNumber: 3 },
      { type: 'unchanged', content: '  "dependencies": {', lineNumber: 4 },
      { type: 'removed', content: '    "react": "^18.0.0",', lineNumber: 5 },
      { type: 'added', content: '    "react": "^19.0.0",', lineNumber: 5 },
      { type: 'unchanged', content: '  }', lineNumber: 6 },
      { type: 'unchanged', content: '}', lineNumber: 7 },
    ];

    return (
      <div className="border rounded">
        {lines.map((line, idx) => (
          <DiffCodeBlock
            key={idx}
            line={line}
            isHighlighted={false}
            searchQuery=""
          />
        ))}
      </div>
    );
  },
};

/**
 * Search highlighting example
 */
export const SearchHighlight: Story = {
  render: () => {
    const lines: DiffLine[] = [
      { type: 'unchanged', content: '  "react": "^18.0.0",', lineNumber: 1 },
      { type: 'added', content: '  "react-dom": "^19.0.0",', lineNumber: 2 },
      { type: 'removed', content: '  "typescript": "^4.9.0",', lineNumber: 3 },
      { type: 'added', content: '  "typescript": "^5.3.0",', lineNumber: 3 },
    ];

    return (
      <div className="space-y-4">
        <div className="text-sm font-medium">Searching for: "react"</div>
        <div className="border rounded">
          {lines.map((line, idx) => (
            <DiffCodeBlock
              key={idx}
              line={line}
              isHighlighted={line.content.includes('react')}
              searchQuery="react"
            />
          ))}
        </div>
      </div>
    );
  },
};

/**
 * Side-by-side comparison
 */
export const SideBySide: Story = {
  render: () => {
    const oldLines: DiffLine[] = [
      { type: 'removed', content: '  "version": "1.0.0",', lineNumber: 1 },
      { type: 'removed', content: '  "react": "^18.0.0",', lineNumber: 2 },
    ];

    const newLines: DiffLine[] = [
      { type: 'added', content: '  "version": "2.0.0",', lineNumber: 1 },
      { type: 'added', content: '  "react": "^19.0.0",', lineNumber: 2 },
    ];

    return (
      <div className="grid grid-cols-2 gap-4">
        <div>
          <div className="text-sm font-medium mb-2 text-red-600">Removed</div>
          <div className="border rounded">
            {oldLines.map((line, idx) => (
              <DiffCodeBlock
                key={idx}
                line={line}
                isHighlighted={false}
                searchQuery=""
              />
            ))}
          </div>
        </div>
        <div>
          <div className="text-sm font-medium mb-2 text-green-600">Added</div>
          <div className="border rounded">
            {newLines.map((line, idx) => (
              <DiffCodeBlock
                key={idx}
                line={line}
                isHighlighted={false}
                searchQuery=""
              />
            ))}
          </div>
        </div>
      </div>
    );
  },
};

/**
 * Dark mode
 */
export const DarkMode: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <div className="dark">
        <Story />
      </div>
    ),
  ],
  args: {
    line: {
      type: 'added',
      content: '  "react": "^19.0.0",',
      lineNumber: 10,
    },
    isHighlighted: true,
    searchQuery: 'react',
  },
};
