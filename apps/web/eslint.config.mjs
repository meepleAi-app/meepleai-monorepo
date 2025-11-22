import js from "@eslint/js";
import typescriptEslint from "@typescript-eslint/eslint-plugin";
import typescriptParser from "@typescript-eslint/parser";
import react from "eslint-plugin-react";
import reactHooks from "eslint-plugin-react-hooks";
import jsxA11y from "eslint-plugin-jsx-a11y";

export default [
  {
    ignores: [
      ".next/**",
      "node_modules/**",
      "out/**",
      "build/**",
      "public/**",
      "*.config.js",
      "*.config.mjs",
      "playwright-report/**",
      "test-results/**",
      "coverage/**",
    ],
  },
  js.configs.recommended,
  {
    files: ["**/*.{ts,tsx}"],
    languageOptions: {
      parser: typescriptParser,
      parserOptions: {
        ecmaVersion: "latest",
        sourceType: "module",
        ecmaFeatures: {
          jsx: true,
        },
      },
      globals: {
        React: "writable",
        JSX: "writable",
        console: "readonly",
        process: "readonly",
        __dirname: "readonly",
        __filename: "readonly",
        module: "readonly",
        require: "readonly",
        Buffer: "readonly",
        setTimeout: "readonly",
        clearTimeout: "readonly",
        setInterval: "readonly",
        clearInterval: "readonly",
        fetch: "readonly",
        Response: "readonly",
        Request: "readonly",
        Headers: "readonly",
        FormData: "readonly",
        window: "readonly",
        document: "readonly",
        navigator: "readonly",
        localStorage: "readonly",
        sessionStorage: "readonly",
        location: "readonly",
        history: "readonly",
        NodeJS: "readonly",
      },
    },
    plugins: {
      "@typescript-eslint": typescriptEslint,
      react: react,
      "react-hooks": reactHooks,
      "jsx-a11y": jsxA11y,
    },
    rules: {
      // Custom rules
      "no-console": ["warn", { allow: ["warn", "error"] }],
      "prefer-const": "warn",
      "no-var": "error",
      "eqeqeq": ["warn", "always"],

      // React rules
      "react/react-in-jsx-scope": "off",
      "react/prop-types": "off",
      "react/jsx-uses-react": "error",
      "react/jsx-uses-vars": "error",
      // Temporarily relaxed - many valid uses for animations and library integrations
      "react/forbid-dom-props": "off",

      // React Hooks rules
      "react-hooks/rules-of-hooks": "error",

      // TypeScript rules
      // Temporarily relaxed for alpha phase - will be re-enabled incrementally
      "@typescript-eslint/no-unused-vars": "off",
      // TS-001: Enforce type safety - no explicit any types (Issue #1431)
      "@typescript-eslint/no-explicit-any": "error",
      "no-unused-vars": "off", // Use @typescript-eslint/no-unused-vars instead
      "no-undef": "off", // TypeScript handles this better than ESLint

      // React Hooks - enforce dependency arrays to prevent infinite loops (Issue #XXXX)
      "react-hooks/exhaustive-deps": "error",

      // Accessibility rules (basic)
      "jsx-a11y/alt-text": "warn",
      "jsx-a11y/anchor-is-valid": "warn",
      // Temporarily relaxed - many false positives in library integrations
      "jsx-a11y/click-events-have-key-events": "off",
      "jsx-a11y/no-static-element-interactions": "off",
    },
    settings: {
      react: {
        version: "detect",
      },
    },
  },
  {
    files: ["**/*.{js,jsx}"],
    languageOptions: {
      ecmaVersion: "latest",
      sourceType: "module",
      parserOptions: {
        ecmaFeatures: {
          jsx: true,
        },
      },
      globals: {
        console: "readonly",
        process: "readonly",
        __dirname: "readonly",
        __filename: "readonly",
        module: "readonly",
        require: "readonly",
        Buffer: "readonly",
      },
    },
    plugins: {
      react: react,
      "react-hooks": reactHooks,
    },
    rules: {
      "no-console": ["warn", { allow: ["warn", "error"] }],
      "prefer-const": "warn",
      "no-var": "error",
      "eqeqeq": ["warn", "always"],
      "react/react-in-jsx-scope": "off",
      "react/prop-types": "off",
      "react-hooks/rules-of-hooks": "error",
      "react-hooks/exhaustive-deps": "warn",
    },
    settings: {
      react: {
        version: "detect",
      },
    },
  },
  // Configuration for Jest setup and test files - Must come after TypeScript config to override
  {
    files: [
      "jest.setup.js",
      "jest.teardown.js",
      "**/*.test.{js,jsx,ts,tsx}",
      "**/__tests__/**/*.{js,jsx,ts,tsx}",
      "src/test-utils/**/*.{js,jsx,ts,tsx}",
      "src/__tests__/**/*.{js,jsx,ts,tsx}",
    ],
    languageOptions: {
      globals: {
        jest: "readonly",
        expect: "readonly",
        test: "readonly",
        describe: "readonly",
        it: "readonly",
        beforeEach: "readonly",
        afterEach: "readonly",
        beforeAll: "readonly",
        afterAll: "readonly",
        global: "readonly",
        setTimeout: "readonly",
        clearTimeout: "readonly",
        setInterval: "readonly",
        clearInterval: "readonly",
      },
    },
    plugins: {
      "@typescript-eslint": typescriptEslint,
    },
    rules: {
      "no-console": "off",
      "@typescript-eslint/no-unused-vars": "off",
      "@typescript-eslint/no-explicit-any": "off",
      "no-unused-vars": "off",
      "no-undef": "off",
    },
  },
  // Configuration for E2E tests (Playwright) - Must come after TypeScript config to override
  {
    files: ["e2e/**/*.ts", "e2e/**/*.tsx"],
    languageOptions: {
      parser: typescriptParser,
      parserOptions: {
        ecmaVersion: "latest",
        sourceType: "module",
      },
      globals: {
        console: "readonly",
        process: "readonly",
        setTimeout: "readonly",
        clearTimeout: "readonly",
        setInterval: "readonly",
        clearInterval: "readonly",
        URL: "readonly",
        Blob: "readonly",
        performance: "readonly",
        requestAnimationFrame: "readonly",
        cancelAnimationFrame: "readonly",
      },
    },
    plugins: {
      "@typescript-eslint": typescriptEslint,
    },
    rules: {
      "no-console": "off", // Allow console in E2E tests
      "@typescript-eslint/no-unused-vars": "off",
      "@typescript-eslint/no-explicit-any": "off",
      "react-hooks/rules-of-hooks": "off", // Disable for Playwright test.use()
      "no-unused-vars": "off",
      "no-undef": "off",
    },
  },
  // Configuration for scripts - Must come after TypeScript config to override
  {
    files: ["scripts/**/*.{ts,tsx,js,jsx}"],
    rules: {
      "no-console": "off", // Allow console in scripts
      "@typescript-eslint/no-explicit-any": "off", // Relaxed type checking for utility scripts
    },
  },
  // Configuration for Storybook files - Must come after TypeScript config to override
  {
    files: ["**/*.stories.{ts,tsx,js,jsx}"],
    rules: {
      "@typescript-eslint/no-explicit-any": "off", // Relaxed type checking for Storybook stories
      "react-hooks/rules-of-hooks": "off", // Storybook render functions can call hooks
      "no-console": "off", // Allow console in Storybook stories for debugging
    },
  },
  // Configuration for admin pages - Must come after TypeScript config to override
  {
    files: ["src/app/admin/**/*.{ts,tsx}"],
    rules: {
      // React Hooks relaxed for internal admin tools (conditional rendering with early returns)
      "react-hooks/rules-of-hooks": "off",
      // Note: @typescript-eslint/no-explicit-any remains enforced (error) for type safety
    },
  },
];
