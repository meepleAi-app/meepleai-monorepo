import type { Preview } from '@storybook/react';
import { withThemeByClassName } from '@storybook/addon-themes';
import '../src/styles/globals.css'; // Import Tailwind CSS

const preview: Preview = {
  parameters: {
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/i,
      },
      expanded: true,
    },
    backgrounds: {
      disable: true, // Use next-themes instead
    },
    layout: 'centered',
    a11y: {
      config: {
        rules: [
          {
            // Disable color-contrast rule for now (Shadcn handles this)
            id: 'color-contrast',
            enabled: false,
          },
        ],
      },
    },
  },
  decorators: [
    withThemeByClassName({
      themes: {
        light: 'light',
        dark: 'dark',
      },
      defaultTheme: 'light',
    }),
  ],
};

export default preview;
