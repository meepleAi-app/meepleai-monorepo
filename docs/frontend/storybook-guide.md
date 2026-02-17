# Storybook Guide

Guide for developing, documenting, and testing UI components with Storybook.

## Quick Start

```bash
cd apps/web

# Start Storybook development server
pnpm storybook

# Build static Storybook
pnpm build-storybook

# Run visual regression tests (requires Chromatic token)
pnpm chromatic
```

## File Structure

Stories are colocated with their components:

```
src/components/
├── ui/
│   ├── primitives/
│   │   ├── button.tsx           # Component
│   │   └── button.stories.tsx   # Stories
│   ├── data-display/
│   │   ├── card.tsx
│   │   └── card.stories.tsx
│   └── overlays/
│       ├── dialog.tsx
│       └── dialog.stories.tsx
└── [feature]/
    ├── MyComponent.tsx
    └── MyComponent.stories.tsx
```

## Creating a New Story

### 1. Basic Story Template

```tsx
import type { Meta, StoryObj } from '@storybook/react';

import { MyComponent } from './MyComponent';

/**
 * Description of the component and its purpose.
 *
 * ## Features
 * - Feature 1
 * - Feature 2
 *
 * ## Accessibility
 * - ✅ Keyboard navigation
 * - ✅ Screen reader support
 */
const meta = {
  title: 'Category/MyComponent',
  component: MyComponent,
  parameters: {
    layout: 'centered', // or 'padded' | 'fullscreen'
    docs: {
      description: {
        component: 'Brief component description for docs.',
      },
    },
  },
  tags: ['autodocs'], // Enable automatic documentation
  argTypes: {
    variant: {
      control: 'select',
      options: ['primary', 'secondary'],
      description: 'Visual variant',
      table: {
        type: { summary: '"primary" | "secondary"' },
        defaultValue: { summary: 'primary' },
      },
    },
  },
} satisfies Meta<typeof MyComponent>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default story with standard props.
 */
export const Default: Story = {
  args: {
    children: 'Content',
    variant: 'primary',
  },
};

/**
 * Story with custom render function.
 */
export const CustomRender: Story = {
  render: () => (
    <div className="flex gap-4">
      <MyComponent variant="primary">Primary</MyComponent>
      <MyComponent variant="secondary">Secondary</MyComponent>
    </div>
  ),
};
```

### 2. Story Categories

Use consistent title patterns:

| Category | Title Pattern | Example |
|----------|---------------|---------|
| UI Primitives | `UI/[Component]` | `UI/Button` |
| Data Display | `UI/[Component]` | `UI/Card` |
| Overlays | `UI/[Component]` | `UI/Dialog` |
| Forms | `Forms/[Component]` | `Forms/LoginForm` |
| Admin | `Admin/[Component]` | `Admin/UserTable` |
| Layout | `Layout/[Component]` | `Layout/Header` |
| Features | `[Feature]/[Component]` | `Chat/MessageList` |

### 3. Decorators

Add context providers or wrappers:

```tsx
const meta = {
  // ...
  decorators: [
    Story => (
      <div className="p-4 bg-background">
        <Story />
      </div>
    ),
  ],
};
```

### 4. Dark Theme Story

```tsx
export const DarkTheme: Story = {
  args: { children: 'Dark Mode' },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-8 bg-background">
        <Story />
      </div>
    ),
  ],
};
```

## Available Addons

| Addon | Purpose | Usage |
|-------|---------|-------|
| **Docs** | Auto-generate documentation | Add `tags: ['autodocs']` |
| **A11y** | Accessibility testing | Automatic panel in toolbar |
| **Themes** | Light/dark theme switching | Theme button in toolbar |
| **Viewport** | Responsive testing | Viewport dropdown in toolbar |
| **Chromatic** | Visual regression testing | Run `pnpm chromatic` |
| **MSW** | API mocking | See MSW section below |

## Viewport Testing

Pre-configured viewports for responsive testing:

| Viewport | Size | Type |
|----------|------|------|
| Mobile | 375×667 | Mobile |
| Mobile Large | 414×896 | Mobile |
| Tablet | 768×1024 | Tablet |
| Laptop | 1024×768 | Desktop |
| Desktop | 1280×800 | Desktop |
| Desktop Large | 1440×900 | Desktop |

Override per-story:

```tsx
export const MobileView: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile',
    },
  },
};
```

## API Mocking with MSW

Mock API calls in stories:

```tsx
import { http, HttpResponse } from 'msw';

export const WithData: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get('/api/v1/games', () => {
          return HttpResponse.json([
            { id: 1, name: 'Chess' },
            { id: 2, name: 'Catan' },
          ]);
        }),
      ],
    },
  },
};

export const Loading: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get('/api/v1/games', async () => {
          await new Promise(r => setTimeout(r, 2000));
          return HttpResponse.json([]);
        }),
      ],
    },
  },
};

export const Error: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get('/api/v1/games', () => {
          return new HttpResponse(null, { status: 500 });
        }),
      ],
    },
  },
};
```

## Accessibility Testing

The a11y addon runs automatic accessibility checks. View results in the Accessibility panel.

For custom a11y rules per-story:

```tsx
export const AccessibleStory: Story = {
  parameters: {
    a11y: {
      config: {
        rules: [
          { id: 'color-contrast', enabled: true },
          { id: 'button-name', enabled: true },
        ],
      },
    },
  },
};
```

## Visual Testing with Chromatic

### Local Testing

```bash
# Set token
export CHROMATIC_PROJECT_TOKEN=<your-token>

# Run visual tests
pnpm chromatic
```

### CI Integration

Visual tests run automatically on PRs. See `.github/workflows/storybook-deploy.yml`.

### Best Practices

1. **Deterministic data**: Avoid `Math.random()`, `Date.now()`, or dynamic content
2. **Fixed viewports**: Set explicit dimensions for layout stories
3. **Mock external resources**: Use placeholder images and mock APIs
4. **Stable animations**: Disable or pause animations for consistent snapshots

## Common Patterns

### Interactive Component

```tsx
import { useState } from 'react';

const InteractiveWrapper = () => {
  const [value, setValue] = useState('');
  return <MyInput value={value} onChange={setValue} />;
};

export const Interactive: Story = {
  render: () => <InteractiveWrapper />,
};
```

### Form Example

```tsx
export const FormExample: Story = {
  render: () => (
    <form className="space-y-4">
      <div className="grid gap-2">
        <Label htmlFor="email">Email</Label>
        <Input id="email" type="email" placeholder="email@example.com" />
      </div>
      <Button type="submit">Submit</Button>
    </form>
  ),
};
```

### Grid/Comparison

```tsx
export const AllVariants: Story = {
  render: () => (
    <div className="grid grid-cols-3 gap-4">
      <MyComponent variant="primary">Primary</MyComponent>
      <MyComponent variant="secondary">Secondary</MyComponent>
      <MyComponent variant="outline">Outline</MyComponent>
    </div>
  ),
};
```

## Troubleshooting

### Story not appearing

- Ensure file ends with `.stories.tsx`
- Check `title` matches expected pattern
- Restart Storybook: `pnpm storybook`

### Styling issues

- Verify `globals.css` is imported in preview.ts
- Check Tailwind classes are valid
- Inspect with browser devtools

### TypeScript errors

- Use `satisfies Meta<typeof Component>` for type safety
- Define explicit `Story` type: `type Story = StoryObj<typeof meta>`

### Auth context errors

- MockAuthProvider is configured globally in preview.ts
- Override per-story with custom decorators if needed

## Resources

- [Storybook Docs](https://storybook.js.org/docs)
- [shadcn/ui Components](https://ui.shadcn.com)
- [Chromatic Docs](https://www.chromatic.com/docs/)
- [MSW Storybook Addon](https://github.com/mswjs/msw-storybook-addon)

---

**Last Updated**: 2026-02-01
