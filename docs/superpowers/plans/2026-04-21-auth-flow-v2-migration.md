# Auth Flow v2 Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate login, register, forgot/reset-password, verify-email, verification-pending pages and AuthModal to v2 design system using 5 new primitives.

**Architecture:** Four internal milestones inside single PR on branch `refactor/redesign-v2-m6`:
1. **M1** — Build 5 v2 primitives (`InputField`, `PwdInput`, `StrengthMeter`, `Divider`, `SuccessCard`) with dedicated tests
2. **M2** — Refactor `LoginForm` and `RegisterForm` to use new primitives
3. **M3** — Refactor `AuthModal` + delete legacy `OAuthButtons.tsx`
4. **M4** — Rewrite page `_content.tsx` files for `/login`, `/register`, `/verification-pending`, `/reset-password`, audit `/verify-email`

**Tech Stack:** React 19 / Next.js 16 / TypeScript strict / Vitest + React Testing Library / Tailwind 4 / react-hook-form + zod (existing forms) / HSL entity tokens `hsl(var(--c-*))`.

**Spec:** `docs/superpowers/specs/2026-04-21-auth-flow-v2-design.md`

---

## File structure

### New files (M1 — primitives)

```
apps/web/src/components/ui/v2/
├── input-field/
│   ├── input-field.tsx
│   ├── input-field.test.tsx
│   └── index.ts
├── pwd-input/
│   ├── pwd-input.tsx
│   ├── pwd-input.test.tsx
│   └── index.ts
├── strength-meter/
│   ├── strength-meter.tsx
│   ├── strength-meter.test.tsx
│   └── index.ts
├── divider/
│   ├── divider.tsx
│   ├── divider.test.tsx
│   └── index.ts
└── success-card/
    ├── success-card.tsx
    ├── success-card.test.tsx
    └── index.ts
```

### Modified files (M2–M4)

- `apps/web/src/components/auth/LoginForm.tsx` — replace AccessibleFormInput + LoadingButton
- `apps/web/src/components/auth/RegisterForm.tsx` — same + add StrengthMeter + terms checkbox
- `apps/web/src/components/auth/AuthModal.tsx` — replace shadcn Tabs with v2 segmented control
- `apps/web/src/components/auth/__tests__/LoginForm.test.tsx` — update assertions
- `apps/web/src/components/auth/__tests__/RegisterForm.test.tsx` — update + add strength
- `apps/web/src/components/auth/__tests__/AuthModal.test.tsx` — NEW
- `apps/web/src/app/(auth)/login/_content.tsx` — rewrite
- `apps/web/src/app/(auth)/register/_content.tsx` — rewrite
- `apps/web/src/app/(auth)/verification-pending/_content.tsx` — rewrite
- `apps/web/src/app/(auth)/reset-password/_content.tsx` — rewrite (dual-mode unified)
- `apps/web/src/app/(auth)/verify-email/_content.tsx` — audit only, align tokens if needed

### Deleted files (M3)

- `apps/web/src/components/auth/OAuthButtons.tsx`
- `apps/web/src/components/auth/OAuthButtons.stories.tsx`

---

## Milestone M1 — v2 Primitives

### Task 1: InputField primitive

**Files:**
- Create: `apps/web/src/components/ui/v2/input-field/input-field.tsx`
- Create: `apps/web/src/components/ui/v2/input-field/index.ts`
- Test: `apps/web/src/components/ui/v2/input-field/input-field.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/src/components/ui/v2/input-field/input-field.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { InputField } from './input-field';

describe('InputField', () => {
  it('renders label and input', () => {
    render(<InputField label="Email" value="" onChange={() => {}} />);
    expect(screen.getByLabelText('Email')).toBeInTheDocument();
  });

  it('fires onChange with new value', () => {
    const onChange = vi.fn();
    render(<InputField label="Email" value="" onChange={onChange} />);
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'a@b.c' } });
    expect(onChange).toHaveBeenCalledWith('a@b.c');
  });

  it('shows error with aria-invalid and aria-describedby', () => {
    render(<InputField label="Email" value="" onChange={() => {}} error="Invalid" />);
    const input = screen.getByLabelText('Email');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('alert')).toHaveTextContent('Invalid');
    expect(input.getAttribute('aria-describedby')).toContain('error');
  });

  it('shows hint when no error', () => {
    render(<InputField label="Email" value="" onChange={() => {}} hint="Use work email" />);
    expect(screen.getByText('Use work email')).toBeInTheDocument();
  });

  it('hides hint when error present', () => {
    render(
      <InputField label="Email" value="" onChange={() => {}} hint="Hint text" error="Bad" />
    );
    expect(screen.queryByText('Hint text')).not.toBeInTheDocument();
  });

  it('renders right slot', () => {
    render(
      <InputField
        label="Email"
        value=""
        onChange={() => {}}
        right={<span data-testid="slot">x</span>}
      />
    );
    expect(screen.getByTestId('slot')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/v2/input-field`
Expected: FAIL — "Cannot find module './input-field'"

- [ ] **Step 3: Implement primitive**

```tsx
// apps/web/src/components/ui/v2/input-field/input-field.tsx
import { forwardRef, useId, type ChangeEvent, type JSX, type ReactNode } from 'react';
import clsx from 'clsx';

export interface InputFieldProps {
  readonly label: string;
  readonly value: string;
  readonly onChange: (value: string) => void;
  readonly type?: string;
  readonly placeholder?: string;
  readonly error?: string;
  readonly hint?: string;
  readonly right?: ReactNode;
  readonly autoComplete?: string;
  readonly name?: string;
  readonly id?: string;
  readonly disabled?: boolean;
  readonly required?: boolean;
  readonly className?: string;
}

export const InputField = forwardRef<HTMLInputElement, InputFieldProps>(function InputField(
  {
    label,
    value,
    onChange,
    type = 'text',
    placeholder,
    error,
    hint,
    right,
    autoComplete,
    name,
    id,
    disabled = false,
    required = false,
    className,
  },
  ref
): JSX.Element {
  const autoId = useId();
  const inputId = id ?? `if-${autoId}`;
  const errorId = `${inputId}-error`;
  const hintId = `${inputId}-hint`;
  const describedBy = error ? errorId : hint ? hintId : undefined;

  const handleChange = (e: ChangeEvent<HTMLInputElement>): void => {
    onChange(e.target.value);
  };

  return (
    <div className={clsx('flex flex-col gap-1.5', className)}>
      <label
        htmlFor={inputId}
        className="text-sm font-medium text-foreground font-body"
      >
        {label}
      </label>
      <div className="relative">
        <input
          ref={ref}
          id={inputId}
          name={name}
          type={type}
          value={value}
          onChange={handleChange}
          placeholder={placeholder}
          autoComplete={autoComplete}
          disabled={disabled}
          required={required}
          aria-invalid={!!error}
          aria-describedby={describedBy}
          className={clsx(
            'w-full h-10 px-3 rounded-lg bg-background text-foreground',
            'border transition-colors font-body text-sm',
            'focus:outline-none focus:ring-2 focus:ring-offset-0',
            error
              ? 'border-[hsl(var(--c-danger))] focus:ring-[hsl(var(--c-danger)/0.3)]'
              : 'border-border focus:ring-[hsl(var(--c-toolkit)/0.3)] focus:border-[hsl(var(--c-toolkit))]',
            right && 'pr-10',
            disabled && 'opacity-50 cursor-not-allowed'
          )}
        />
        {right && (
          <div className="absolute inset-y-0 right-0 flex items-center pr-2">{right}</div>
        )}
      </div>
      {error ? (
        <p
          id={errorId}
          role="alert"
          aria-live="polite"
          className="text-xs text-[hsl(var(--c-danger))] font-body"
        >
          {error}
        </p>
      ) : hint ? (
        <p id={hintId} className="text-xs text-muted-foreground font-body">
          {hint}
        </p>
      ) : null}
    </div>
  );
});
```

- [ ] **Step 4: Create barrel**

```ts
// apps/web/src/components/ui/v2/input-field/index.ts
export { InputField } from './input-field';
export type { InputFieldProps } from './input-field';
```

- [ ] **Step 5: Run tests to verify PASS**

Run: `cd apps/web && pnpm vitest run src/components/ui/v2/input-field`
Expected: PASS 6/6

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/v2/input-field/
git commit -m "feat(v2): add InputField primitive with error/hint/right slot"
```

---

### Task 2: StrengthMeter primitive (built before PwdInput — PwdInput consumes it)

**Files:**
- Create: `apps/web/src/components/ui/v2/strength-meter/strength-meter.tsx`
- Create: `apps/web/src/components/ui/v2/strength-meter/index.ts`
- Test: `apps/web/src/components/ui/v2/strength-meter/strength-meter.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/src/components/ui/v2/strength-meter/strength-meter.test.tsx
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StrengthMeter } from './strength-meter';

describe('StrengthMeter', () => {
  it('renders null when password empty', () => {
    const { container } = render(<StrengthMeter password="" />);
    expect(container.firstChild).toBeNull();
  });

  it('shows 1 filled segment for very weak password', () => {
    render(<StrengthMeter password="ab" />);
    expect(screen.getByRole('status')).toHaveAttribute('aria-live', 'polite');
    expect(screen.getByText(/debole/i)).toBeInTheDocument();
  });

  it('shows higher score for long password with special chars', () => {
    render(<StrengthMeter password="LongPass123!@#" />);
    expect(screen.getByText(/buona|ottima/i)).toBeInTheDocument();
  });

  it('renders 4 segments', () => {
    const { container } = render(<StrengthMeter password="abc123" />);
    const segments = container.querySelectorAll('[data-segment]');
    expect(segments).toHaveLength(4);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/v2/strength-meter`
Expected: FAIL — module not found

- [ ] **Step 3: Implement primitive**

```tsx
// apps/web/src/components/ui/v2/strength-meter/strength-meter.tsx
import type { JSX } from 'react';
import clsx from 'clsx';

export interface StrengthMeterProps {
  readonly password: string;
}

interface Score {
  readonly filled: 0 | 1 | 2 | 3 | 4;
  readonly label: string;
  readonly color: string;
}

function computeScore(password: string): Score {
  if (password.length === 0) {
    return { filled: 0, label: '', color: 'hsl(var(--c-danger))' };
  }
  let score = 0;
  if (password.length >= 6) score++;
  if (password.length >= 10) score++;
  if (/[A-Z]/.test(password) && /[a-z]/.test(password)) score++;
  if (/[^A-Za-z0-9]/.test(password) || /\d/.test(password)) score++;
  const filled = Math.min(score, 4) as 0 | 1 | 2 | 3 | 4;
  const labels = ['Debole', 'Debole', 'Discreta', 'Buona', 'Ottima'] as const;
  const colors = [
    'hsl(var(--c-danger))',
    'hsl(var(--c-danger))',
    'hsl(var(--c-event))',
    'hsl(var(--c-game))',
    'hsl(var(--c-toolkit))',
  ] as const;
  return { filled, label: labels[filled], color: colors[filled] };
}

export function StrengthMeter({ password }: StrengthMeterProps): JSX.Element | null {
  if (password.length === 0) return null;
  const { filled, label, color } = computeScore(password);

  return (
    <div role="status" aria-live="polite" className="flex flex-col gap-1">
      <div className="flex gap-1" aria-hidden="true">
        {[0, 1, 2, 3].map((i) => (
          <div
            key={i}
            data-segment={i}
            className={clsx(
              'h-1 flex-1 rounded-full transition-colors motion-reduce:transition-none'
            )}
            style={{ background: i < filled ? color : 'hsl(var(--border))' }}
          />
        ))}
      </div>
      <span className="text-xs text-muted-foreground font-body">
        Password: <span style={{ color }}>{label}</span>
      </span>
    </div>
  );
}
```

- [ ] **Step 4: Create barrel**

```ts
// apps/web/src/components/ui/v2/strength-meter/index.ts
export { StrengthMeter } from './strength-meter';
export type { StrengthMeterProps } from './strength-meter';
```

- [ ] **Step 5: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/v2/strength-meter`
Expected: PASS 4/4

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/v2/strength-meter/
git commit -m "feat(v2): add StrengthMeter primitive with 4-segment score bar"
```

---

### Task 3: PwdInput primitive

**Files:**
- Create: `apps/web/src/components/ui/v2/pwd-input/pwd-input.tsx`
- Create: `apps/web/src/components/ui/v2/pwd-input/index.ts`
- Test: `apps/web/src/components/ui/v2/pwd-input/pwd-input.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/src/components/ui/v2/pwd-input/pwd-input.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { PwdInput } from './pwd-input';

describe('PwdInput', () => {
  it('renders as password input by default', () => {
    render(<PwdInput label="Password" value="secret" onChange={() => {}} />);
    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'password');
  });

  it('toggles visibility when eye button clicked', () => {
    render(<PwdInput label="Password" value="secret" onChange={() => {}} />);
    const toggle = screen.getByRole('button', { name: /mostra password/i });
    fireEvent.click(toggle);
    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'text');
    expect(toggle).toHaveAttribute('aria-pressed', 'true');
    fireEvent.click(toggle);
    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'password');
  });

  it('fires onChange with new value', () => {
    const onChange = vi.fn();
    render(<PwdInput label="Password" value="" onChange={onChange} />);
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'abc' } });
    expect(onChange).toHaveBeenCalledWith('abc');
  });

  it('shows strength meter when showStrength and value present', () => {
    render(
      <PwdInput label="Password" value="abc123" onChange={() => {}} showStrength />
    );
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('hides strength meter when showStrength false', () => {
    render(<PwdInput label="Password" value="abc123" onChange={() => {}} />);
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('hides strength meter when value empty', () => {
    render(<PwdInput label="Password" value="" onChange={() => {}} showStrength />);
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('shows error with aria-invalid', () => {
    render(
      <PwdInput label="Password" value="" onChange={() => {}} error="Required" />
    );
    expect(screen.getByLabelText('Password')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('alert')).toHaveTextContent('Required');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/v2/pwd-input`
Expected: FAIL — module not found

- [ ] **Step 3: Implement primitive**

```tsx
// apps/web/src/components/ui/v2/pwd-input/pwd-input.tsx
import { forwardRef, useState, type JSX } from 'react';
import { Eye, EyeOff } from 'lucide-react';
import { InputField } from '../input-field';
import { StrengthMeter } from '../strength-meter';

export interface PwdInputProps {
  readonly label: string;
  readonly value: string;
  readonly onChange: (value: string) => void;
  readonly placeholder?: string;
  readonly error?: string;
  readonly hint?: string;
  readonly autoComplete?: string;
  readonly name?: string;
  readonly id?: string;
  readonly disabled?: boolean;
  readonly required?: boolean;
  readonly showStrength?: boolean;
  readonly className?: string;
}

export const PwdInput = forwardRef<HTMLInputElement, PwdInputProps>(function PwdInput(
  {
    label,
    value,
    onChange,
    placeholder,
    error,
    hint,
    autoComplete,
    name,
    id,
    disabled = false,
    required = false,
    showStrength = false,
    className,
  },
  ref
): JSX.Element {
  const [show, setShow] = useState(false);

  const toggle = (
    <button
      type="button"
      onClick={() => setShow((s) => !s)}
      aria-label={show ? 'Nascondi password' : 'Mostra password'}
      aria-pressed={show}
      className="h-8 w-8 flex items-center justify-center rounded-md text-muted-foreground hover:text-foreground hover:bg-muted focus:outline-none focus:ring-2 focus:ring-[hsl(var(--c-toolkit)/0.3)]"
      tabIndex={-1}
    >
      {show ? <EyeOff size={16} aria-hidden="true" /> : <Eye size={16} aria-hidden="true" />}
    </button>
  );

  return (
    <div className={className}>
      <InputField
        ref={ref}
        label={label}
        value={value}
        onChange={onChange}
        type={show ? 'text' : 'password'}
        placeholder={placeholder}
        error={error}
        hint={hint}
        autoComplete={autoComplete}
        name={name}
        id={id}
        disabled={disabled}
        required={required}
        right={toggle}
      />
      {showStrength && value.length > 0 && (
        <div className="mt-2">
          <StrengthMeter password={value} />
        </div>
      )}
    </div>
  );
});
```

- [ ] **Step 4: Create barrel**

```ts
// apps/web/src/components/ui/v2/pwd-input/index.ts
export { PwdInput } from './pwd-input';
export type { PwdInputProps } from './pwd-input';
```

- [ ] **Step 5: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/v2/pwd-input`
Expected: PASS 7/7

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/v2/pwd-input/
git commit -m "feat(v2): add PwdInput primitive with show/hide toggle and optional strength"
```

---

### Task 4: Divider primitive

**Files:**
- Create: `apps/web/src/components/ui/v2/divider/divider.tsx`
- Create: `apps/web/src/components/ui/v2/divider/index.ts`
- Test: `apps/web/src/components/ui/v2/divider/divider.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/src/components/ui/v2/divider/divider.test.tsx
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Divider } from './divider';

describe('Divider', () => {
  it('renders default label "oppure"', () => {
    render(<Divider />);
    expect(screen.getByText('oppure')).toBeInTheDocument();
  });

  it('renders custom label', () => {
    render(<Divider label="or" />);
    expect(screen.getByText('or')).toBeInTheDocument();
  });

  it('has role separator', () => {
    render(<Divider />);
    expect(screen.getByRole('separator')).toHaveAttribute('aria-orientation', 'horizontal');
  });

  it('label is aria-hidden', () => {
    render(<Divider label="oppure" />);
    expect(screen.getByText('oppure')).toHaveAttribute('aria-hidden', 'true');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/v2/divider`
Expected: FAIL — module not found

- [ ] **Step 3: Implement primitive**

```tsx
// apps/web/src/components/ui/v2/divider/divider.tsx
import type { JSX } from 'react';

export interface DividerProps {
  readonly label?: string;
  readonly className?: string;
}

export function Divider({ label = 'oppure', className }: DividerProps): JSX.Element {
  return (
    <div
      role="separator"
      aria-orientation="horizontal"
      className={`flex items-center gap-3 my-4 ${className ?? ''}`}
    >
      <div className="flex-1 h-px bg-border" />
      <span
        aria-hidden="true"
        className="text-xs text-muted-foreground uppercase tracking-wider font-body"
      >
        {label}
      </span>
      <div className="flex-1 h-px bg-border" />
    </div>
  );
}
```

- [ ] **Step 4: Create barrel**

```ts
// apps/web/src/components/ui/v2/divider/index.ts
export { Divider } from './divider';
export type { DividerProps } from './divider';
```

- [ ] **Step 5: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/v2/divider`
Expected: PASS 4/4

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/v2/divider/
git commit -m "feat(v2): add Divider primitive with oppure label"
```

---

### Task 5: SuccessCard primitive

**Files:**
- Create: `apps/web/src/components/ui/v2/success-card/success-card.tsx`
- Create: `apps/web/src/components/ui/v2/success-card/index.ts`
- Test: `apps/web/src/components/ui/v2/success-card/success-card.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/src/components/ui/v2/success-card/success-card.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { SuccessCard } from './success-card';

describe('SuccessCard', () => {
  it('renders emoji, title, body, and CTA', () => {
    render(
      <SuccessCard
        emoji="✅"
        title="Fatto"
        body="Email inviata"
        cta="Vai alla home"
        onCta={() => {}}
      />
    );
    expect(screen.getByText('✅')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Fatto' })).toBeInTheDocument();
    expect(screen.getByText('Email inviata')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Vai alla home' })).toBeInTheDocument();
  });

  it('emoji wrapper is aria-hidden', () => {
    render(
      <SuccessCard emoji="✅" title="Fatto" body="" cta="OK" onCta={() => {}} />
    );
    const emoji = screen.getByText('✅');
    expect(emoji.parentElement).toHaveAttribute('aria-hidden', 'true');
  });

  it('fires onCta when CTA clicked', () => {
    const onCta = vi.fn();
    render(<SuccessCard emoji="✅" title="Fatto" body="" cta="OK" onCta={onCta} />);
    fireEvent.click(screen.getByRole('button', { name: 'OK' }));
    expect(onCta).toHaveBeenCalledTimes(1);
  });

  it('accepts body as ReactNode', () => {
    render(
      <SuccessCard
        emoji="✅"
        title="Fatto"
        body={<p data-testid="body-node">Rich content</p>}
        cta="OK"
        onCta={() => {}}
      />
    );
    expect(screen.getByTestId('body-node')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/v2/success-card`
Expected: FAIL — module not found

- [ ] **Step 3: Implement primitive**

```tsx
// apps/web/src/components/ui/v2/success-card/success-card.tsx
import type { JSX, ReactNode } from 'react';
import { Btn } from '../btn';

export interface SuccessCardProps {
  readonly emoji: string;
  readonly title: string;
  readonly body: string | ReactNode;
  readonly cta: string;
  readonly onCta: () => void;
  readonly className?: string;
}

export function SuccessCard({
  emoji,
  title,
  body,
  cta,
  onCta,
  className,
}: SuccessCardProps): JSX.Element {
  return (
    <div
      className={`flex flex-col items-center text-center gap-3 p-6 ${className ?? ''}`}
    >
      <div
        aria-hidden="true"
        className="w-16 h-16 flex items-center justify-center rounded-full bg-[hsl(var(--c-toolkit)/0.1)] text-3xl"
      >
        <span>{emoji}</span>
      </div>
      <h2 className="font-heading font-bold text-xl text-foreground m-0">{title}</h2>
      {typeof body === 'string' ? (
        <p className="font-body text-sm text-muted-foreground m-0">{body}</p>
      ) : (
        body
      )}
      <Btn variant="primary" onClick={onCta} className="mt-2">
        {cta}
      </Btn>
    </div>
  );
}
```

- [ ] **Step 4: Create barrel**

```ts
// apps/web/src/components/ui/v2/success-card/index.ts
export { SuccessCard } from './success-card';
export type { SuccessCardProps } from './success-card';
```

- [ ] **Step 5: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/v2/success-card`
Expected: PASS 4/4

- [ ] **Step 6: Verify Btn export path**

Run: `cat apps/web/src/components/ui/v2/btn/index.ts`
Expected: contains `export { Btn } from './btn';` — if the exported name differs (e.g. `Button`), update the import on step 3 accordingly before commit.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/ui/v2/success-card/
git commit -m "feat(v2): add SuccessCard primitive with emoji + CTA"
```

---

## Milestone M2 — Refactor forms

### Task 6: Refactor LoginForm to use InputField + PwdInput + Btn

**Files:**
- Modify: `apps/web/src/components/auth/LoginForm.tsx`
- Modify: `apps/web/src/components/auth/__tests__/LoginForm.test.tsx`

- [ ] **Step 1: Read existing test to understand baseline**

Run: `cat apps/web/src/components/auth/__tests__/LoginForm.test.tsx | head -80`
Expected: baseline with test IDs `login-email`, `login-password`, `login-submit`.

- [ ] **Step 2: Verify Btn API compatibility (before writing test/impl)**

Run: `cat apps/web/src/components/ui/v2/btn/btn.tsx | head -40`
Expected: `Btn` props include `variant`, `fullWidth`, `loading`, `type`. If any prop differs in name (e.g. `isLoading` instead of `loading`, or `block` instead of `fullWidth`), adapt all JSX usages in Step 5 below (and in Task 7, 10, 11, 12, 13) to the actual prop names. Doing this now avoids spurious test failures.

- [ ] **Step 3: Update test to assert v2 primitives**

Add the following test case to `apps/web/src/components/auth/__tests__/LoginForm.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { LoginForm } from '../LoginForm';

describe('LoginForm v2 primitives', () => {
  it('renders password field with eye toggle', () => {
    render(<LoginForm onSubmit={vi.fn()} />);
    expect(
      screen.getByRole('button', { name: /mostra password/i })
    ).toBeInTheDocument();
  });

  it('toggles password visibility', () => {
    render(<LoginForm onSubmit={vi.fn()} />);
    const pwd = screen.getByLabelText(/password/i);
    expect(pwd).toHaveAttribute('type', 'password');
    fireEvent.click(screen.getByRole('button', { name: /mostra password/i }));
    expect(pwd).toHaveAttribute('type', 'text');
  });

  it('marks email aria-invalid on validation error', async () => {
    render(<LoginForm onSubmit={vi.fn()} />);
    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: '' } });
    fireEvent.submit(screen.getByTestId('login-form'));
    const email = await screen.findByLabelText(/email/i);
    expect(email).toHaveAttribute('aria-invalid', 'true');
  });
});
```

- [ ] **Step 4: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/auth/__tests__/LoginForm.test.tsx`
Expected: FAIL — eye toggle not found

- [ ] **Step 5: Refactor LoginForm.tsx**

Replace entire `apps/web/src/components/auth/LoginForm.tsx` with:

```tsx
import { zodResolver } from '@hookform/resolvers/zod';
import { Controller, useForm } from 'react-hook-form';
import * as z from 'zod';

import { useTranslation } from '@/hooks/useTranslation';
import { Btn } from '@/components/ui/v2/btn';
import { InputField } from '@/components/ui/v2/input-field';
import { PwdInput } from '@/components/ui/v2/pwd-input';

export interface LoginFormData {
  email: string;
  password: string;
}

export interface LoginFormProps {
  onSubmit: (data: LoginFormData) => Promise<void> | void;
  loading?: boolean;
  error?: string;
  onErrorDismiss?: () => void;
}

export function LoginForm({
  onSubmit,
  loading = false,
  error,
  onErrorDismiss,
}: LoginFormProps) {
  const { t } = useTranslation();

  const loginSchema = z.object({
    email: z
      .string()
      .min(1, t('validation.emailRequired'))
      .email(t('validation.invalidEmail')),
    password: z
      .string()
      .min(8, t('validation.passwordMin'))
      .max(100, t('validation.passwordMax')),
  });

  const {
    control,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  const isLoading = loading || isSubmitting;

  const onFormSubmit = async (data: LoginFormData) => {
    if (onErrorDismiss) onErrorDismiss();
    await onSubmit(data);
  };

  return (
    <form
      onSubmit={handleSubmit(onFormSubmit)}
      className="space-y-4"
      noValidate
      data-testid="login-form"
    >
      <Controller
        name="email"
        control={control}
        render={({ field }) => (
          <InputField
            label={t('auth.login.emailLabel')}
            id="login-email"
            type="email"
            placeholder={t('auth.login.emailPlaceholder')}
            autoComplete="email"
            value={field.value}
            onChange={field.onChange}
            error={errors.email?.message}
            disabled={isLoading}
            required
          />
        )}
      />
      <Controller
        name="password"
        control={control}
        render={({ field }) => (
          <PwdInput
            label={t('auth.login.passwordLabel')}
            id="login-password"
            placeholder={t('auth.login.passwordPlaceholder')}
            autoComplete="current-password"
            value={field.value}
            onChange={field.onChange}
            error={errors.password?.message}
            disabled={isLoading}
            required
          />
        )}
      />
      {error && (
        <div
          className="rounded-md bg-[hsl(var(--c-danger)/0.1)] border border-[hsl(var(--c-danger)/0.3)] p-3"
          role="alert"
          aria-live="polite"
        >
          <p className="text-sm text-[hsl(var(--c-danger))]">{error}</p>
        </div>
      )}
      <Btn
        type="submit"
        variant="primary"
        fullWidth
        loading={isLoading}
        data-testid="login-submit"
      >
        {t('auth.login.loginButton')}
      </Btn>
    </form>
  );
}
```

- [ ] **Step 6: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/auth/__tests__/LoginForm.test.tsx`
Expected: PASS including 3 new tests. If existing tests that relied on `AccessibleFormInput` internals fail (e.g. `id` matching, CSS classes), update them to assert observable behavior (label text, role=button, aria-invalid).

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/auth/LoginForm.tsx apps/web/src/components/auth/__tests__/LoginForm.test.tsx
git commit -m "refactor(auth): migrate LoginForm to v2 InputField/PwdInput/Btn"
```

---

### Task 7: Refactor RegisterForm with StrengthMeter + terms checkbox

**Files:**
- Modify: `apps/web/src/components/auth/RegisterForm.tsx`
- Modify: `apps/web/src/components/auth/__tests__/RegisterForm.test.tsx`

- [ ] **Step 1: Read existing component to preserve contract**

Run: `cat apps/web/src/components/auth/RegisterForm.tsx | head -60`
Expected: identify `onSubmit` signature — it must accept `termsAcceptedAt` + `honeypot`.

- [ ] **Step 2: Add v2-specific test cases**

Append to `apps/web/src/components/auth/__tests__/RegisterForm.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { RegisterForm } from '../RegisterForm';

describe('RegisterForm v2 primitives', () => {
  it('shows strength meter after typing password', () => {
    render(<RegisterForm onSubmit={vi.fn()} />);
    fireEvent.change(screen.getByLabelText(/password/i), {
      target: { value: 'abc123' },
    });
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('blocks submit until terms checkbox checked', async () => {
    const onSubmit = vi.fn();
    render(<RegisterForm onSubmit={onSubmit} />);
    fireEvent.change(screen.getByLabelText(/email/i), {
      target: { value: 'a@b.c' },
    });
    fireEvent.change(screen.getByLabelText(/password/i), {
      target: { value: 'SecurePass1!' },
    });
    fireEvent.submit(screen.getByTestId('register-form'));
    // Wait a tick for async validation
    await new Promise((r) => setTimeout(r, 20));
    expect(onSubmit).not.toHaveBeenCalled();
    expect(
      await screen.findByText(/accetta.*(termini|condizioni)/i)
    ).toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/components/auth/__tests__/RegisterForm.test.tsx`
Expected: FAIL — strength meter not present, terms checkbox missing

- [ ] **Step 4: Refactor RegisterForm.tsx**

Replace entire file with:

```tsx
import { zodResolver } from '@hookform/resolvers/zod';
import { Controller, useForm } from 'react-hook-form';
import * as z from 'zod';

import { useTranslation } from '@/hooks/useTranslation';
import { Btn } from '@/components/ui/v2/btn';
import { InputField } from '@/components/ui/v2/input-field';
import { PwdInput } from '@/components/ui/v2/pwd-input';

export interface RegisterFormData {
  email: string;
  password: string;
  termsAccepted: boolean;
  honeypot?: string;
}

export interface RegisterFormProps {
  onSubmit: (data: {
    email: string;
    password: string;
    termsAcceptedAt: Date;
    honeypot?: string;
  }) => Promise<void> | void;
  loading?: boolean;
  error?: string;
  onErrorDismiss?: () => void;
}

export function RegisterForm({
  onSubmit,
  loading = false,
  error,
  onErrorDismiss,
}: RegisterFormProps) {
  const { t } = useTranslation();

  const schema = z.object({
    email: z
      .string()
      .min(1, t('validation.emailRequired'))
      .email(t('validation.invalidEmail')),
    password: z
      .string()
      .min(8, t('validation.passwordMin'))
      .max(100, t('validation.passwordMax')),
    termsAccepted: z
      .boolean()
      .refine((v) => v === true, { message: t('auth.register.termsRequired') }),
    honeypot: z.string().optional(),
  });

  const {
    control,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormData>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '', termsAccepted: false, honeypot: '' },
  });

  const isLoading = loading || isSubmitting;

  const onFormSubmit = async (data: RegisterFormData) => {
    if (onErrorDismiss) onErrorDismiss();
    await onSubmit({
      email: data.email,
      password: data.password,
      termsAcceptedAt: new Date(),
      honeypot: data.honeypot,
    });
  };

  return (
    <form
      onSubmit={handleSubmit(onFormSubmit)}
      className="space-y-4"
      noValidate
      data-testid="register-form"
    >
      <Controller
        name="email"
        control={control}
        render={({ field }) => (
          <InputField
            label={t('auth.register.emailLabel')}
            id="register-email"
            type="email"
            placeholder={t('auth.register.emailPlaceholder')}
            autoComplete="email"
            value={field.value}
            onChange={field.onChange}
            error={errors.email?.message}
            disabled={isLoading}
            required
          />
        )}
      />
      <Controller
        name="password"
        control={control}
        render={({ field }) => (
          <PwdInput
            label={t('auth.register.passwordLabel')}
            id="register-password"
            placeholder={t('auth.register.passwordPlaceholder')}
            autoComplete="new-password"
            value={field.value}
            onChange={field.onChange}
            error={errors.password?.message}
            disabled={isLoading}
            required
            showStrength
          />
        )}
      />

      {/* Honeypot (hidden) */}
      <Controller
        name="honeypot"
        control={control}
        render={({ field }) => (
          <input
            type="text"
            tabIndex={-1}
            autoComplete="off"
            aria-hidden="true"
            className="absolute left-[-9999px] w-px h-px"
            {...field}
          />
        )}
      />

      <Controller
        name="termsAccepted"
        control={control}
        render={({ field }) => (
          <label className="flex items-start gap-2 text-sm font-body text-foreground cursor-pointer">
            <input
              type="checkbox"
              checked={field.value}
              onChange={(e) => field.onChange(e.target.checked)}
              disabled={isLoading}
              className="mt-0.5 w-4 h-4 rounded border-border text-[hsl(var(--c-toolkit))] focus:ring-2 focus:ring-[hsl(var(--c-toolkit)/0.3)]"
              aria-invalid={!!errors.termsAccepted}
              data-testid="register-terms"
            />
            <span>
              {t('auth.register.termsPrefix')}{' '}
              <a
                href="/terms"
                target="_blank"
                rel="noopener noreferrer"
                className="underline text-[hsl(var(--c-toolkit))]"
              >
                {t('auth.register.termsLink')}
              </a>{' '}
              {t('auth.register.termsAnd')}{' '}
              <a
                href="/privacy"
                target="_blank"
                rel="noopener noreferrer"
                className="underline text-[hsl(var(--c-toolkit))]"
              >
                {t('auth.register.privacyLink')}
              </a>
              .
            </span>
          </label>
        )}
      />
      {errors.termsAccepted && (
        <p role="alert" className="text-xs text-[hsl(var(--c-danger))] font-body">
          {errors.termsAccepted.message}
        </p>
      )}

      {error && (
        <div
          className="rounded-md bg-[hsl(var(--c-danger)/0.1)] border border-[hsl(var(--c-danger)/0.3)] p-3"
          role="alert"
          aria-live="polite"
        >
          <p className="text-sm text-[hsl(var(--c-danger))]">{error}</p>
        </div>
      )}

      <Btn
        type="submit"
        variant="primary"
        fullWidth
        loading={isLoading}
        data-testid="register-submit"
      >
        {t('auth.register.registerButton')}
      </Btn>
    </form>
  );
}
```

- [ ] **Step 5: Verify translation keys exist**

Run: `grep -rn 'auth.register.termsPrefix\|auth.register.termsLink\|auth.register.termsAnd\|auth.register.privacyLink\|auth.register.termsRequired' apps/web/src/locales/ 2>/dev/null | head`
Expected: if keys missing, add them to `apps/web/src/locales/it.json` (and en.json if present):

```json
{
  "auth": {
    "register": {
      "termsPrefix": "Accetto i",
      "termsLink": "Termini di servizio",
      "termsAnd": "e la",
      "privacyLink": "Privacy Policy",
      "termsRequired": "Devi accettare i termini per continuare"
    }
  }
}
```

- [ ] **Step 6: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/auth/__tests__/RegisterForm.test.tsx`
Expected: PASS including new tests. Update pre-existing tests only where assertions tie to old DOM structure.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/auth/RegisterForm.tsx apps/web/src/components/auth/__tests__/RegisterForm.test.tsx apps/web/src/locales/
git commit -m "refactor(auth): migrate RegisterForm to v2 with strength meter and terms checkbox"
```

---

## Milestone M3 — AuthModal + delete legacy

### Task 8: Refactor AuthModal with v2 segmented tabs

**Files:**
- Modify: `apps/web/src/components/auth/AuthModal.tsx`
- Create: `apps/web/src/components/auth/__tests__/AuthModal.test.tsx`

- [ ] **Step 1: Write new test file**

```tsx
// apps/web/src/components/auth/__tests__/AuthModal.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { AuthModal } from '../AuthModal';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}));

describe('AuthModal', () => {
  it('renders login tab by default', () => {
    render(<AuthModal open onClose={vi.fn()} />);
    expect(screen.getByTestId('login-form')).toBeInTheDocument();
  });

  it('renders register tab when defaultMode=register', () => {
    render(<AuthModal open onClose={vi.fn()} defaultMode="register" />);
    expect(screen.getByTestId('register-form')).toBeInTheDocument();
  });

  it('switches between tabs via segmented control', () => {
    render(<AuthModal open onClose={vi.fn()} />);
    fireEvent.click(screen.getByRole('tab', { name: /registrati|register/i }));
    expect(screen.getByTestId('register-form')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('tab', { name: /accedi|login/i }));
    expect(screen.getByTestId('login-form')).toBeInTheDocument();
  });

  it('calls onClose when close triggered', () => {
    const onClose = vi.fn();
    render(<AuthModal open onClose={onClose} />);
    // AccessibleModal close — fires on Escape
    fireEvent.keyDown(document.body, { key: 'Escape' });
    expect(onClose).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run test — should fail because current AuthModal uses Radix Tabs**

Run: `cd apps/web && pnpm vitest run src/components/auth/__tests__/AuthModal.test.tsx`
Expected: Some tests may pass (login-form visible), `role="tab"` assertion likely fails since existing AuthModal uses shadcn Tabs which DO render role=tab. Adjust — first read existing AuthModal to understand current markup:

Run: `cat apps/web/src/components/auth/AuthModal.tsx`

- [ ] **Step 3: Refactor AuthModal — replace shadcn Tabs with v2 segmented control**

Replace the Tabs block in `AuthModal.tsx` (keep `AccessibleModal` outer shell + TwoFactorVerification handoff logic intact). Target structure:

```tsx
import { useState, type JSX } from 'react';
import clsx from 'clsx';

import { AccessibleModal } from '@/components/accessible';
import { useTranslation } from '@/hooks/useTranslation';
import { Divider } from '@/components/ui/v2/divider';
import { OAuthButton } from '@/components/ui/v2/oauth-buttons';
import { LoginForm } from './LoginForm';
import { RegisterForm } from './RegisterForm';
// ... keep existing imports for TwoFactorVerification, API calls, etc.

export interface AuthModalProps {
  readonly open: boolean;
  readonly onClose: () => void;
  readonly defaultMode?: 'login' | 'register';
  // ...keep existing props
}

type Mode = 'login' | 'register';

export function AuthModal({
  open,
  onClose,
  defaultMode = 'login',
}: AuthModalProps): JSX.Element {
  const { t } = useTranslation();
  const [mode, setMode] = useState<Mode>(defaultMode);
  // ... keep existing state (error, 2FA challenge, etc.)

  return (
    <AccessibleModal open={open} onClose={onClose} ariaLabel={t('auth.modal.title')}>
      <div className="p-6 max-w-md w-full">
        <div
          role="tablist"
          aria-label={t('auth.modal.tabsLabel')}
          className="grid grid-cols-2 gap-1 p-1 bg-muted rounded-lg mb-4"
        >
          {(['login', 'register'] as const).map((m) => (
            <button
              key={m}
              role="tab"
              type="button"
              aria-selected={mode === m}
              onClick={() => setMode(m)}
              className={clsx(
                'px-3 py-1.5 rounded-md text-sm font-body transition-colors',
                mode === m
                  ? 'bg-background text-foreground shadow-sm'
                  : 'text-muted-foreground hover:text-foreground'
              )}
            >
              {m === 'login' ? t('auth.modal.loginTab') : t('auth.modal.registerTab')}
            </button>
          ))}
        </div>

        {mode === 'login' ? (
          <LoginForm onSubmit={/* existing handler */ async () => {}} />
        ) : (
          <RegisterForm onSubmit={/* existing handler */ async () => {}} />
        )}

        <Divider label={t('auth.modal.or')} />

        <div className="flex flex-col gap-2">
          <OAuthButton provider="google" onClick={/* existing OAuth handler */} />
          <OAuthButton provider="discord" onClick={/* existing OAuth handler */} />
          <OAuthButton provider="github" onClick={/* existing OAuth handler */} />
        </div>
      </div>
    </AccessibleModal>
  );
}
```

**Preserve existing behavior**: DO NOT strip 2FA challenge handling, error state, or API call handlers. Read the existing file and port only the Tabs→segmented-control and OAuthButtons→v2 OAuthButton replacements. Keep all business logic.

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/auth/__tests__/AuthModal.test.tsx`
Expected: PASS 4/4

- [ ] **Step 5: Run full auth test suite to check for regressions**

Run: `cd apps/web && pnpm vitest run src/components/auth`
Expected: all tests PASS

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/auth/AuthModal.tsx apps/web/src/components/auth/__tests__/AuthModal.test.tsx
git commit -m "refactor(auth): migrate AuthModal to v2 segmented tabs + OAuthButton + Divider"
```

---

### Task 9: Delete legacy OAuthButtons

**Files:**
- Delete: `apps/web/src/components/auth/OAuthButtons.tsx`
- Delete: `apps/web/src/components/auth/OAuthButtons.stories.tsx`
- Modify: `apps/web/src/components/auth/index.ts` (if it re-exports OAuthButtons)
- Modify: any consumer still importing from `@/components/auth/OAuthButtons`

- [ ] **Step 1: Find all consumers**

Run: `cd apps/web && grep -rn "from.*components/auth/OAuthButtons\|from '@/components/auth/OAuthButtons'" src/`
Expected: list of files — must all be migrated to `@/components/ui/v2/oauth-buttons` before delete.

- [ ] **Step 2: Migrate each consumer** (only if any remain — AuthModal done in Task 8)

For each consumer found: replace import and adapt props. The v2 `OAuthButton` takes `provider` + `onClick` (no list — render one per provider). Example migration:

```tsx
// Before
<OAuthButtons onProviderClick={handleOAuth} />
// After
<div className="flex flex-col gap-2">
  <OAuthButton provider="google" onClick={() => handleOAuth('google')} />
  <OAuthButton provider="discord" onClick={() => handleOAuth('discord')} />
  <OAuthButton provider="github" onClick={() => handleOAuth('github')} />
</div>
```

- [ ] **Step 3: Delete legacy files**

```bash
rm apps/web/src/components/auth/OAuthButtons.tsx
rm apps/web/src/components/auth/OAuthButtons.stories.tsx
```

- [ ] **Step 4: Remove from barrel**

Open `apps/web/src/components/auth/index.ts` and remove the line that re-exports `OAuthButtons`, if present.

- [ ] **Step 5: Verify typecheck and test**

```bash
cd apps/web && pnpm typecheck && pnpm vitest run src/components/auth
```
Expected: no type errors, all tests pass.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore(auth): delete legacy OAuthButtons — all consumers on v2"
```

---

## Milestone M4 — Page rewrites

### Task 9.5: Audit + add missing i18n keys

**Files:**
- Modify: `apps/web/src/locales/it/auth.json` (or equivalent)
- Modify: `apps/web/src/locales/en/auth.json` (or equivalent)

M4 tasks (10-13) and the refactored forms (Task 6-7) use translation keys under `auth.login.*`, `auth.register.*`, `auth.verification.*`, `auth.reset.*`. Some of these may already exist; others (especially under `auth.reset.*` and `auth.verification.*`) are new.

- [ ] **Step 1: Locate existing locale files**

Run: `find apps/web/src -name "auth.json" -o -name "*.locale.ts" | head -10`
Expected: identify locale file paths and format (JSON vs TS module).

- [ ] **Step 2: Audit required keys against existing**

Collect the following keys used by Task 6, 7, 10, 11, 12, 13 and check if each already exists in the locale files. Add missing keys to **both** `it` and `en` variants with sensible copy:

```
auth.login.*          (emailLabel, passwordLabel, loginButton, forgotPassword, noAccount, signUp, or continueWithGoogle, oauthDivider)
auth.register.*       (emailLabel, passwordLabel, registerButton, termsLabel, termsLink, privacyLink, hasAccount, signIn)
auth.verification.*   (title, subtitle, resendCta, cooldownMessage, changeEmailCta, sentConfirmation)
auth.reset.*          (requestTitle, requestSubtitle, requestCta, emailLabel, sentTitle, sentBody, backToLogin,
                       confirmTitle, confirmSubtitle, confirmCta, passwordLabel, successTitle, successBody, goToLogin,
                       genericError, invalidToken)
```

- [ ] **Step 3: Commit locale additions**

```bash
git add apps/web/src/locales/
git commit -m "i18n(auth): add missing keys for v2 auth flow (reset, verification, register terms)"
```

Note: if the project uses a different i18n system (e.g. `next-intl`, inline `useTranslation` with embedded strings), adapt the locations accordingly — the key list above is authoritative regardless of backend.

---

### Task 10: Rewrite /login _content.tsx with AuthCard + OAuthButton + Divider

**Files:**
- Modify: `apps/web/src/app/(auth)/login/_content.tsx`

- [ ] **Step 1: Read existing content**

Run: `cat apps/web/src/app/\(auth\)/login/_content.tsx`
Expected: identify session redirect logic and onSubmit handler.

- [ ] **Step 2: Rewrite file**

```tsx
// apps/web/src/app/(auth)/login/_content.tsx
'use client';

import { useRouter } from 'next/navigation';
import Link from 'next/link';

import { AuthCard } from '@/components/ui/v2/auth-card';
import { Divider } from '@/components/ui/v2/divider';
import { OAuthButton } from '@/components/ui/v2/oauth-buttons';
import { LoginForm, type LoginFormData } from '@/components/auth/LoginForm';
import { useAuthStore } from '@/stores/authStore';
import { useTranslation } from '@/hooks/useTranslation';
import { useState } from 'react';

export function LoginContent(): JSX.Element {
  const router = useRouter();
  const { t } = useTranslation();
  const login = useAuthStore((s) => s.login);
  const [error, setError] = useState<string | undefined>();

  const handleSubmit = async (data: LoginFormData): Promise<void> => {
    try {
      await login(data.email, data.password);
      router.push('/library');
    } catch (e) {
      setError(e instanceof Error ? e.message : t('auth.login.genericError'));
    }
  };

  const handleOAuth = (provider: 'google' | 'discord' | 'github'): void => {
    window.location.href = `/api/v1/auth/oauth/${provider}`;
  };

  return (
    <AuthCard
      title={t('auth.login.title')}
      subtitle={t('auth.login.subtitle')}
      footerAction={
        <span>
          {t('auth.login.noAccount')}{' '}
          <Link
            href="/register"
            className="text-[hsl(var(--c-toolkit))] underline font-medium"
          >
            {t('auth.login.registerCta')}
          </Link>
        </span>
      }
    >
      <LoginForm
        onSubmit={handleSubmit}
        error={error}
        onErrorDismiss={() => setError(undefined)}
      />
      <Divider />
      <div className="flex flex-col gap-2">
        <OAuthButton provider="google" onClick={() => handleOAuth('google')} />
        <OAuthButton provider="discord" onClick={() => handleOAuth('discord')} />
        <OAuthButton provider="github" onClick={() => handleOAuth('github')} />
      </div>
      <div className="mt-4 text-center">
        <Link
          href="/reset-password"
          className="text-sm text-muted-foreground hover:text-foreground underline"
        >
          {t('auth.login.forgotPassword')}
        </Link>
      </div>
    </AuthCard>
  );
}
```

**IMPORTANT**: read the pre-existing `_content.tsx` first and preserve any auth-store API (the exact name of the login action, e.g. `signIn` vs `login`) and redirect target. The above is a template — adapt to match existing store surface.

- [ ] **Step 3: Run existing page test**

Run: `cd apps/web && pnpm vitest run src/app/\\(auth\\)/login`
Expected: existing test either passes or needs updates for new DOM. Update assertions only to observe behavior (form submit, redirect, error display) — not specific CSS classes.

- [ ] **Step 4: Typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: no errors.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(auth\)/login/_content.tsx
git commit -m "refactor(auth): migrate /login page to v2 AuthCard layout"
```

---

### Task 11: Rewrite /register _content.tsx

**Files:**
- Modify: `apps/web/src/app/(auth)/register/_content.tsx`

- [ ] **Step 1: Read existing content to identify invite-only mode**

Run: `cat apps/web/src/app/\(auth\)/register/_content.tsx`
Expected: find branching between public registration and `RequestAccessForm` (invite-only).

- [ ] **Step 2: Rewrite preserving invite-only branch**

Replace the public-mode branch to use `AuthCard` + `RegisterForm` + `OAuthButton` + `Divider`, keep the invite-only branch using `RequestAccessForm` as-is (unchanged):

```tsx
'use client';

import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { useState } from 'react';

import { AuthCard } from '@/components/ui/v2/auth-card';
import { Divider } from '@/components/ui/v2/divider';
import { OAuthButton } from '@/components/ui/v2/oauth-buttons';
import { RegisterForm } from '@/components/auth/RegisterForm';
import { RequestAccessForm } from '@/components/auth/RequestAccessForm';
import { useAuthStore } from '@/stores/authStore';
import { useTranslation } from '@/hooks/useTranslation';
import { isInviteOnly } from '@/lib/config'; // adapt to actual helper name

export function RegisterContent(): JSX.Element {
  const router = useRouter();
  const { t } = useTranslation();
  const register = useAuthStore((s) => s.register); // adapt to actual action
  const [error, setError] = useState<string | undefined>();

  if (isInviteOnly()) {
    return (
      <AuthCard
        title={t('auth.register.inviteOnlyTitle')}
        subtitle={t('auth.register.inviteOnlySubtitle')}
      >
        <RequestAccessForm />
      </AuthCard>
    );
  }

  const handleSubmit = async (data: {
    email: string;
    password: string;
    termsAcceptedAt: Date;
    honeypot?: string;
  }): Promise<void> => {
    try {
      await register(data);
      router.push('/verification-pending');
    } catch (e) {
      setError(e instanceof Error ? e.message : t('auth.register.genericError'));
    }
  };

  const handleOAuth = (provider: 'google' | 'discord' | 'github'): void => {
    window.location.href = `/api/v1/auth/oauth/${provider}`;
  };

  return (
    <AuthCard
      title={t('auth.register.title')}
      subtitle={t('auth.register.subtitle')}
      footerAction={
        <span>
          {t('auth.register.hasAccount')}{' '}
          <Link
            href="/login"
            className="text-[hsl(var(--c-toolkit))] underline font-medium"
          >
            {t('auth.register.loginCta')}
          </Link>
        </span>
      }
    >
      <RegisterForm
        onSubmit={handleSubmit}
        error={error}
        onErrorDismiss={() => setError(undefined)}
      />
      <Divider />
      <div className="flex flex-col gap-2">
        <OAuthButton provider="google" onClick={() => handleOAuth('google')} />
        <OAuthButton provider="discord" onClick={() => handleOAuth('discord')} />
        <OAuthButton provider="github" onClick={() => handleOAuth('github')} />
      </div>
    </AuthCard>
  );
}
```

Adapt `isInviteOnly()` import and `register` action name to match the existing store/config API.

- [ ] **Step 3: Typecheck + tests**

```bash
cd apps/web && pnpm typecheck && pnpm vitest run src/app/\\(auth\\)/register
```
Expected: no type errors; page test passes (update assertions if they targeted old markup).

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/\(auth\)/register/_content.tsx
git commit -m "refactor(auth): migrate /register page to v2 with invite-only branch preserved"
```

---

### Task 12: Rewrite /verification-pending _content.tsx

**Files:**
- Modify: `apps/web/src/app/(auth)/verification-pending/_content.tsx`

- [ ] **Step 1: Read existing content**

Run: `cat apps/web/src/app/\(auth\)/verification-pending/_content.tsx`
Expected: identify resend action and email from session.

- [ ] **Step 2: Write test first for 30s countdown**

```tsx
// apps/web/src/app/(auth)/verification-pending/_content.test.tsx
import { render, screen, fireEvent, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { VerificationPendingContent } from './_content';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}));

vi.mock('@/lib/api', () => ({
  api: { auth: { resendVerificationEmail: vi.fn(() => Promise.resolve()) } },
}));

describe('VerificationPendingContent', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('renders envelope emoji and title', () => {
    render(<VerificationPendingContent email="a@b.c" />);
    expect(screen.getByText('📧')).toBeInTheDocument();
  });

  it('starts 30s cooldown after resend click', () => {
    render(<VerificationPendingContent email="a@b.c" />);
    const btn = screen.getByRole('button', { name: /invia di nuovo|resend/i });
    fireEvent.click(btn);
    expect(btn).toBeDisabled();
    act(() => { vi.advanceTimersByTime(1000); });
    expect(btn).toHaveTextContent(/29/);
    act(() => { vi.advanceTimersByTime(29000); });
    expect(btn).not.toBeDisabled();
  });
});
```

- [ ] **Step 3: Run test to verify fail**

Run: `cd apps/web && pnpm vitest run src/app/\\(auth\\)/verification-pending`
Expected: FAIL — component doesn't match new shape

- [ ] **Step 4: Rewrite content**

```tsx
// apps/web/src/app/(auth)/verification-pending/_content.tsx
'use client';

import { useEffect, useState, type JSX } from 'react';
import Link from 'next/link';

import { AuthCard } from '@/components/ui/v2/auth-card';
import { Btn } from '@/components/ui/v2/btn';
import { useTranslation } from '@/hooks/useTranslation';
import { api } from '@/lib/api';

const COOLDOWN_SECONDS = 30;

export interface VerificationPendingContentProps {
  readonly email?: string;
}

export function VerificationPendingContent({
  email,
}: VerificationPendingContentProps): JSX.Element {
  const { t } = useTranslation();
  const [cooldown, setCooldown] = useState(0);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | undefined>();

  useEffect(() => {
    if (cooldown <= 0) return;
    const id = setInterval(() => setCooldown((s) => Math.max(0, s - 1)), 1000);
    return () => clearInterval(id);
  }, [cooldown]);

  const handleResend = async (): Promise<void> => {
    setError(undefined);
    try {
      await api.auth.resendVerificationEmail();
      setSent(true);
      setCooldown(COOLDOWN_SECONDS);
    } catch (e) {
      setError(e instanceof Error ? e.message : t('auth.verifPending.resendError'));
    }
  };

  return (
    <AuthCard title={t('auth.verifPending.title')} subtitle={email ?? ''}>
      <div className="flex flex-col items-center gap-4 py-2">
        <div
          aria-hidden="true"
          className="w-16 h-16 rounded-full bg-[hsl(var(--c-event)/0.15)] flex items-center justify-center text-3xl"
        >
          <span>📧</span>
        </div>
        <p className="text-sm text-muted-foreground text-center font-body">
          {t('auth.verifPending.body')}
        </p>
        {sent && !error && (
          <p
            role="status"
            aria-live="polite"
            className="text-xs text-[hsl(var(--c-toolkit))] font-body"
          >
            {t('auth.verifPending.sentConfirm')}
          </p>
        )}
        {error && (
          <p role="alert" className="text-xs text-[hsl(var(--c-danger))] font-body">
            {error}
          </p>
        )}
        <Btn
          variant="primary"
          onClick={handleResend}
          disabled={cooldown > 0}
          fullWidth
        >
          {cooldown > 0
            ? `${t('auth.verifPending.resendCooldown')} (${cooldown})`
            : t('auth.verifPending.resendCta')}
        </Btn>
        <Link
          href="/register"
          className="text-sm text-muted-foreground hover:text-foreground underline"
        >
          {t('auth.verifPending.changeEmail')}
        </Link>
      </div>
    </AuthCard>
  );
}
```

- [ ] **Step 5: Run tests**

Run: `cd apps/web && pnpm vitest run src/app/\\(auth\\)/verification-pending`
Expected: PASS 3/3

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/\(auth\)/verification-pending/
git commit -m "refactor(auth): migrate /verification-pending to v2 with 30s cooldown"
```

---

### Task 13: Rewrite /reset-password _content.tsx (dual-mode)

**Files:**
- Modify: `apps/web/src/app/(auth)/reset-password/_content.tsx`

- [ ] **Step 1: Read existing 554-line file to understand dual-mode branching**

Run: `cat apps/web/src/app/\(auth\)/reset-password/_content.tsx | head -100`
Expected: one branch for "request reset" (no token), another for "confirm reset" (token query param).

- [ ] **Step 2: Write test for success state**

```tsx
// apps/web/src/app/(auth)/reset-password/_content.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ResetPasswordContent } from './_content';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}));

const requestReset = vi.fn(() => Promise.resolve());
const confirmReset = vi.fn(() => Promise.resolve());
vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      requestPasswordReset: (...args: unknown[]) => requestReset(...args),
      confirmPasswordReset: (...args: unknown[]) => confirmReset(...args),
    },
  },
}));

describe('ResetPasswordContent', () => {
  it('shows SuccessCard after request-reset submit', async () => {
    render(<ResetPasswordContent token={null} />);
    fireEvent.change(screen.getByLabelText(/email/i), {
      target: { value: 'a@b.c' },
    });
    fireEvent.submit(screen.getByTestId('reset-request-form'));
    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /email inviata|sent/i })).toBeInTheDocument();
    });
  });

  it('shows SuccessCard after confirm-reset submit', async () => {
    render(<ResetPasswordContent token="abc" />);
    fireEvent.change(screen.getByLabelText(/password/i), {
      target: { value: 'NewPass123!' },
    });
    fireEvent.submit(screen.getByTestId('reset-confirm-form'));
    await waitFor(() => {
      expect(
        screen.getByRole('heading', { name: /password aggiornata|updated/i })
      ).toBeInTheDocument();
    });
  });
});
```

- [ ] **Step 3: Run test — fail expected**

Run: `cd apps/web && pnpm vitest run src/app/\\(auth\\)/reset-password`
Expected: FAIL

- [ ] **Step 4: Rewrite content**

```tsx
// apps/web/src/app/(auth)/reset-password/_content.tsx
'use client';

import { useState, type JSX } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';

import { AuthCard } from '@/components/ui/v2/auth-card';
import { Btn } from '@/components/ui/v2/btn';
import { InputField } from '@/components/ui/v2/input-field';
import { PwdInput } from '@/components/ui/v2/pwd-input';
import { SuccessCard } from '@/components/ui/v2/success-card';
import { api } from '@/lib/api';
import { useTranslation } from '@/hooks/useTranslation';

export interface ResetPasswordContentProps {
  readonly token: string | null;
}

export function ResetPasswordContent({ token }: ResetPasswordContentProps): JSX.Element {
  return token ? <ConfirmReset token={token} /> : <RequestReset />;
}

function RequestReset(): JSX.Element {
  const { t } = useTranslation();
  const router = useRouter();
  const [email, setEmail] = useState('');
  const [loading, setLoading] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | undefined>();

  const handleSubmit = async (e: React.FormEvent): Promise<void> => {
    e.preventDefault();
    setError(undefined);
    setLoading(true);
    try {
      await api.auth.requestPasswordReset(email);
      setSent(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : t('auth.reset.genericError'));
    } finally {
      setLoading(false);
    }
  };

  if (sent) {
    return (
      <AuthCard title={t('auth.reset.requestTitle')}>
        <SuccessCard
          emoji="📧"
          title={t('auth.reset.sentTitle')}
          body={t('auth.reset.sentBody')}
          cta={t('auth.reset.backToLogin')}
          onCta={() => router.push('/login')}
        />
      </AuthCard>
    );
  }

  return (
    <AuthCard
      title={t('auth.reset.requestTitle')}
      subtitle={t('auth.reset.requestSubtitle')}
      footerAction={
        <Link href="/login" className="text-[hsl(var(--c-toolkit))] underline">
          {t('auth.reset.backToLogin')}
        </Link>
      }
    >
      <form
        onSubmit={handleSubmit}
        data-testid="reset-request-form"
        className="space-y-4"
      >
        <InputField
          label={t('auth.reset.emailLabel')}
          type="email"
          value={email}
          onChange={setEmail}
          autoComplete="email"
          required
          disabled={loading}
        />
        {error && (
          <p role="alert" className="text-xs text-[hsl(var(--c-danger))] font-body">
            {error}
          </p>
        )}
        <Btn type="submit" variant="primary" fullWidth loading={loading}>
          {t('auth.reset.requestCta')}
        </Btn>
      </form>
    </AuthCard>
  );
}

function ConfirmReset({ token }: { readonly token: string }): JSX.Element {
  const { t } = useTranslation();
  const router = useRouter();
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [done, setDone] = useState(false);
  const [error, setError] = useState<string | undefined>();

  const handleSubmit = async (e: React.FormEvent): Promise<void> => {
    e.preventDefault();
    setError(undefined);
    setLoading(true);
    try {
      await api.auth.confirmPasswordReset(token, password);
      setDone(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : t('auth.reset.invalidToken'));
    } finally {
      setLoading(false);
    }
  };

  if (done) {
    return (
      <AuthCard title={t('auth.reset.confirmTitle')}>
        <SuccessCard
          emoji="✅"
          title={t('auth.reset.successTitle')}
          body={t('auth.reset.successBody')}
          cta={t('auth.reset.goToLogin')}
          onCta={() => router.push('/login')}
        />
      </AuthCard>
    );
  }

  return (
    <AuthCard
      title={t('auth.reset.confirmTitle')}
      subtitle={t('auth.reset.confirmSubtitle')}
    >
      <form
        onSubmit={handleSubmit}
        data-testid="reset-confirm-form"
        className="space-y-4"
      >
        <PwdInput
          label={t('auth.reset.passwordLabel')}
          value={password}
          onChange={setPassword}
          autoComplete="new-password"
          required
          disabled={loading}
          showStrength
        />
        {error && (
          <p role="alert" className="text-xs text-[hsl(var(--c-danger))] font-body">
            {error}
          </p>
        )}
        <Btn type="submit" variant="primary" fullWidth loading={loading}>
          {t('auth.reset.confirmCta')}
        </Btn>
      </form>
    </AuthCard>
  );
}
```

- [ ] **Step 5: Check if `page.tsx` passes `token` prop**

Run: `cat apps/web/src/app/\(auth\)/reset-password/page.tsx`
Expected: reads `searchParams.token` and passes to content. If the existing page reads the token differently (e.g. via `useSearchParams`), inline that logic in `ResetPasswordContent` instead of taking it as prop — adjust accordingly.

- [ ] **Step 6: Run tests**

Run: `cd apps/web && pnpm vitest run src/app/\\(auth\\)/reset-password`
Expected: PASS 2/2 new tests; pre-existing tests may need assertion updates.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/app/\(auth\)/reset-password/
git commit -m "refactor(auth): unify /reset-password dual-mode with v2 SuccessCard on success"
```

---

### Task 14: Audit /verify-email _content.tsx

**Files:**
- Modify (only if needed): `apps/web/src/app/(auth)/verify-email/_content.tsx`

- [ ] **Step 1: Read existing file**

Run: `cat apps/web/src/app/\(auth\)/verify-email/_content.tsx`
Expected: mostly backend-driven confirm-by-token page; check for direct HTML styling or legacy primitives.

- [ ] **Step 2: Apply minimal v2 alignment**

If the file uses raw colors (e.g. `text-green-500`, `bg-red-50`), replace with entity tokens:
- success state → `hsl(var(--c-toolkit))` + `SuccessCard`
- error state → `hsl(var(--c-danger))`

Wrap the main content in `AuthCard` if it isn't already.

Skip this task entirely if the existing file already uses `AuthCard` and entity tokens consistently.

- [ ] **Step 3: Typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: no errors

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm vitest run src/app/\\(auth\\)/verify-email`
Expected: PASS

- [ ] **Step 5: Commit (only if changes made)**

```bash
git add apps/web/src/app/\(auth\)/verify-email/
git commit -m "refactor(auth): align /verify-email with v2 entity tokens"
```

---

## Milestone M5 — Polish, bundle, validation

### Task 15: Run full frontend test suite + typecheck + lint

- [ ] **Step 1: Full test run**

```bash
cd apps/web
pnpm typecheck
pnpm lint
pnpm vitest run
```
Expected: 0 type errors, 0 lint errors, all tests pass (933+ existing + ~25 new).

- [ ] **Step 2: Fix any regressions**

If existing tests fail due to new DOM shape, update assertions to observe behavior (role, aria, label text) — not internal CSS classes or Radix-specific data attributes.

- [ ] **Step 3: Commit fixes**

```bash
git add -A
git commit -m "test(auth): update assertions for v2 DOM"
```

---

### Task 16: Update bundle baseline

**Files:**
- Modify: `apps/web/bundle-size-baseline.json` (or equivalent file tracking bundle size)

- [ ] **Step 1: Build to measure**

```bash
cd apps/web && pnpm build
```
Expected: success; note new gzipped bundle size for auth chunks.

- [ ] **Step 2: Read current baseline**

Run: `cat apps/web/bundle-size-baseline.json 2>/dev/null || find apps/web -name "*baseline*" -maxdepth 3`
Expected: locate the baseline file (name may be `bundle-size-baseline.json` or similar).

- [ ] **Step 3: Update baseline with new total**

Edit the baseline file and increase the total bytes field by the observed delta (+15–30 KB per spec).

- [ ] **Step 4: Commit**

```bash
git add apps/web/bundle-size-baseline.json
git commit -m "chore(bundle): update baseline after auth v2 primitives (+5 components)"
```

---

### Task 17: Final PR comment + handoff

- [ ] **Step 1: Push branch**

```bash
git push -u origin refactor/redesign-v2-m6
```

- [ ] **Step 2: Verify PR #484 (or create if missing)**

Run: `gh pr list --head refactor/redesign-v2-m6 --json number,title,baseRefName`
Expected: if PR exists, it targets the correct parent branch (check `frontend-dev` or redesign-v2 per CLAUDE.md rules). If missing, create against the detected parent.

- [ ] **Step 3: Add PR comment summarizing Fase 2.6**

```bash
gh pr comment <PR_NUMBER> --body "$(cat <<'EOF'
## Fase 2.6 — Auth flow v2 migration complete

### New primitives (5)
- `InputField` — labeled input + error/hint/right slot
- `PwdInput` — password w/ show/hide toggle + optional strength
- `StrengthMeter` — 4-segment password score bar
- `Divider` — horizontal separator w/ label
- `SuccessCard` — centered success state w/ emoji + CTA

### Refactored
- `LoginForm` + `RegisterForm` → v2 primitives, removed `AccessibleFormInput`/`LoadingButton`
- `AuthModal` → v2 segmented tabs, OAuthButton, Divider (AccessibleModal shell preserved)
- Pages: `/login`, `/register`, `/verification-pending`, `/reset-password` → `AuthCard` layout

### Deleted
- `src/components/auth/OAuthButtons.tsx` (legacy) + stories

### Tests
- 25+ new unit tests across 5 primitives + refactored forms + AuthModal + page specs
- All 933+ existing Vitest tests green

### Out of scope (Fase 2.6.5)
- 2FA setup screen (backend endpoints unverified)
- E2E Playwright additions
EOF
)"
```

- [ ] **Step 4: Close Fase 2.6 task in TodoWrite**

Mark task #33 (Fase 2.6) as completed.

---

## Self-Review

### Spec coverage check
| Spec section | Task(s) |
|---|---|
| `InputField` primitive + tests | Task 1 |
| `PwdInput` primitive + tests | Task 3 |
| `StrengthMeter` primitive + tests | Task 2 |
| `Divider` primitive + tests | Task 4 |
| `SuccessCard` primitive + tests | Task 5 |
| `LoginForm` refactor | Task 6 |
| `RegisterForm` refactor + strength + terms | Task 7 |
| `AuthModal` refactor (segmented tabs, Divider, OAuthButton) | Task 8 |
| Delete legacy `OAuthButtons.tsx` | Task 9 |
| i18n keys audit + add missing | Task 9.5 |
| `/login` page rewrite | Task 10 |
| `/register` page rewrite + invite-only preservation | Task 11 |
| `/verification-pending` rewrite w/ 30s cooldown | Task 12 |
| `/reset-password` dual-mode w/ `SuccessCard` | Task 13 |
| `/verify-email` audit | Task 14 |
| No regressions on existing 933+ tests | Task 15 |
| Bundle baseline updated | Task 16 |
| A11y (aria-invalid, aria-describedby, role=status, role=separator, role=tab, aria-pressed) | Tasks 1, 3, 4, 8 |
| Out of scope (2FA setup, E2E, screenshot tests, email templates, OAuth changes, backend) | Not implemented — noted in PR comment |

### Type consistency check
- `InputFieldProps.onChange: (value: string) => void` — consistent in Task 1 + 6 + 7 + 13
- `PwdInputProps.showStrength?: boolean` — consistent in Task 3 + 7 + 13
- `SuccessCardProps.onCta: () => void` — consistent in Task 5 + 13
- `OAuthButton` API (`provider`, `onClick`) — existing v2 primitive reused unchanged
- `Btn` API (`variant`, `fullWidth`, `loading`, `type`) — verification step added in Task 6 (step 5) before relying on it

### Placeholder check
- No "TBD", "similar to Task N", or "implement later" — each code-changing step includes full code.
- Optional edits (Task 14) are clearly conditional.
- Adaptation hints (e.g. "adapt to actual store API") include a specific command to read the real file first, so the engineer doesn't guess.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-04-21-auth-flow-v2-migration.md`. Two execution options:

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
