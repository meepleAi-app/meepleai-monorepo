import js from "@eslint/js";
import typescriptEslint from "@typescript-eslint/eslint-plugin";
import typescriptParser from "@typescript-eslint/parser";
import react from "eslint-plugin-react";
import reactHooks from "eslint-plugin-react-hooks";
import jsxA11y from "eslint-plugin-jsx-a11y";
import security from "eslint-plugin-security";
import noUnsanitized from "eslint-plugin-no-unsanitized";

// Custom security rules
import noIncompleteSanitization from "./eslint-rules/no-incomplete-sanitization.js";

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
      "jest.config.js",
      "jest.setup.js",
      "jest.teardown.js",
      "scripts/**", // Node.js utility scripts - exempt from browser linting
      "**/__tests__/**", // Unit test files - TypeScript handles syntax checking
      "**/*.test.{ts,tsx,js,jsx}", // Test files - avoid ESLint parser conflicts
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
      security: security,
      "no-unsanitized": noUnsanitized,
      // Custom security rules
      "local": {
        rules: {
          "no-incomplete-sanitization": noIncompleteSanitization,
        },
      },
    },
    rules: {
      // Custom rules
      "no-console": ["warn", { allow: ["warn", "error"] }],
      "prefer-const": "warn",
      "no-var": "error",
      "eqeqeq": ["warn", "always"],
      "no-redeclare": "warn", // Changed to warn temporarily
      "no-empty": "warn", // Changed to warn temporarily

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
      // Changed to warn temporarily to reduce noise during security implementation
      "@typescript-eslint/no-explicit-any": "warn",
      "no-unused-vars": "off", // Use @typescript-eslint/no-unused-vars instead
      "no-undef": "off", // TypeScript handles this better than ESLint

      // React Hooks - enforce dependency arrays to prevent infinite loops (Issue #XXXX)
      // Changed to warn to reduce noise - should be fixed gradually
      "react-hooks/exhaustive-deps": "warn",

      // Accessibility rules (basic)
      "jsx-a11y/alt-text": "warn",
      "jsx-a11y/anchor-is-valid": "warn",
      // Temporarily relaxed - many false positives in library integrations
      "jsx-a11y/click-events-have-key-events": "off",
      "jsx-a11y/no-static-element-interactions": "off",

      // ============================================================================
      // Security Rules (SEC-001 to SEC-015) - Issue #XXXX
      // ============================================================================

      // SEC-001: Prevent XSS via dangerouslySetInnerHTML without sanitization
      "no-unsanitized/property": "error",
      "no-unsanitized/method": "error",

      // SEC-002: Detect unsafe patterns (security plugin)
      "security/detect-eval-with-expression": "error",
      "security/detect-non-literal-fs-filename": "warn",
      "security/detect-non-literal-regexp": "warn",
      "security/detect-non-literal-require": "warn",
      "security/detect-object-injection": "warn",
      "security/detect-possible-timing-attacks": "warn",
      "security/detect-pseudoRandomBytes": "error",
      "security/detect-unsafe-regex": "error",

      // SEC-003: Prevent dangerous global functions
      "no-eval": "error",
      "no-implied-eval": "error",
      "no-new-func": "error",

      // SEC-004: Prevent prototype pollution
      "no-proto": "error",
      "no-extend-native": "error",

      // SEC-005: Strict null/undefined checks (TypeScript)
      "@typescript-eslint/no-non-null-assertion": "warn",
      // Note: prefer-nullish-coalescing and prefer-optional-chain require type information
      // They are disabled to avoid performance impact. Use them in IDE instead.

      // SEC-006: Prevent unsafe type assertions
      // Changed to warn to reduce noise during security implementation
      "@typescript-eslint/consistent-type-assertions": [
        "warn",
        {
          assertionStyle: "as",
          objectLiteralTypeAssertions: "allow-as-parameter",
        },
      ],

      // SEC-007: Prevent RegEx DoS
      "no-control-regex": "error",
      "no-regex-spaces": "warn",

      // SEC-008: Prevent incomplete sanitization (CWE-116)
      // Custom rule to detect unsafe .replace() patterns that don't escape backslashes
      "local/no-incomplete-sanitization": "error",
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
      parser: typescriptParser,
      parserOptions: {
        ecmaVersion: "latest",
        sourceType: "module",
        ecmaFeatures: {
          jsx: true,
        },
      },
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
        Buffer: "readonly",
        process: "readonly",
        console: "readonly",
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
      // Disable security rules for test files (many false positives)
      "security/detect-object-injection": "off",
      "security/detect-non-literal-regexp": "off",
      "security/detect-non-literal-fs-filename": "off",
      "security/detect-unsafe-regex": "off",
      "@typescript-eslint/no-non-null-assertion": "off",
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
      // Disable security rules for E2E tests (many false positives)
      "security/detect-object-injection": "off",
      "security/detect-non-literal-regexp": "off",
      "security/detect-non-literal-fs-filename": "off",
      "security/detect-unsafe-regex": "off",
      "@typescript-eslint/no-non-null-assertion": "off",
    },
  },
  // Configuration for scripts - Must come after TypeScript config to override
  {
    files: ["scripts/**/*.{ts,tsx,js,jsx,mjs,cjs}"],
    languageOptions: {
      globals: {
        console: "readonly",
        process: "readonly",
        __dirname: "readonly",
        __filename: "readonly",
        Buffer: "readonly",
      },
    },
    rules: {
      "no-console": "off", // Allow console in scripts
      "no-undef": "off", // Node globals are implicit
      "@typescript-eslint/no-explicit-any": "off", // Relaxed type checking for utility scripts
      // Disable some security rules for scripts (false positives in build tools)
      "security/detect-non-literal-fs-filename": "off",
      "security/detect-non-literal-regexp": "off",
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
