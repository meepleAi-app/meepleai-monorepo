import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import * as z from 'zod';

import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from './form';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../overlays/select';
import { Button } from '../primitives/button';
import { Checkbox } from '../primitives/checkbox';
import { Input } from '../primitives/input';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Form component for building accessible forms with react-hook-form.
 *
 * ## shadcn/ui Component
 * Integration with react-hook-form for validation and state management.
 *
 * ## Subcomponents
 * - **Form**: FormProvider wrapper
 * - **FormField**: Controller wrapper with context
 * - **FormItem**: Container for form field with spacing
 * - **FormLabel**: Accessible label with error styling
 * - **FormControl**: Slot for form input with ARIA attributes
 * - **FormDescription**: Helper text below input
 * - **FormMessage**: Validation error message
 *
 * ## Features
 * - **Validation**: Zod schema integration
 * - **Accessibility**: ARIA labels and live regions for errors
 * - **Error Handling**: Visual error states with messages
 * - **Composable**: Works with any form inputs
 *
 * ## Accessibility
 * - ✅ Form inputs linked to labels via htmlFor
 * - ✅ Error messages announced via aria-live
 * - ✅ Invalid fields marked with aria-invalid
 * - ✅ Descriptions linked via aria-describedby
 */
const meta = {
  title: 'UI/Form',
  component: Form,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'A form component built on react-hook-form with Zod validation and accessible form field primitives.',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    Story => (
      <div className="w-[400px]">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof Form>;

export default meta;
type Story = StoryObj<typeof meta>;

// Simple form schema for basic example
const simpleFormSchema = z.object({
  username: z.string().min(2, 'Username must be at least 2 characters'),
});

/**
 * Default form with single input.
 * Basic example showing FormField structure.
 */
const DefaultFormComponent = () => {
  const form = useForm<z.infer<typeof simpleFormSchema>>({
    resolver: zodResolver(simpleFormSchema),
    defaultValues: {
      username: '',
    },
  });

  const onSubmit = (data: z.infer<typeof simpleFormSchema>) => {
    console.log(data);
  };

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
        <FormField
          control={form.control}
          name="username"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Username</FormLabel>
              <FormControl>
                <Input placeholder="Enter username" {...field} />
              </FormControl>
              <FormDescription>Your public display name.</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
        <Button type="submit">Submit</Button>
      </form>
    </Form>
  );
};

export const Default: Story = {
  render: () => <DefaultFormComponent />,
};

// Registration form schema
const registrationSchema = z
  .object({
    email: z.string().email('Please enter a valid email address'),
    password: z.string().min(8, 'Password must be at least 8 characters'),
    confirmPassword: z.string(),
  })
  .refine(data => data.password === data.confirmPassword, {
    message: "Passwords don't match",
    path: ['confirmPassword'],
  });

/**
 * Registration form with validation.
 * Shows email and password fields with cross-field validation.
 */
const RegistrationFormComponent = () => {
  const form = useForm<z.infer<typeof registrationSchema>>({
    resolver: zodResolver(registrationSchema),
    defaultValues: {
      email: '',
      password: '',
      confirmPassword: '',
    },
  });

  const onSubmit = (data: z.infer<typeof registrationSchema>) => {
    console.log(data);
  };

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
        <FormField
          control={form.control}
          name="email"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Email</FormLabel>
              <FormControl>
                <Input type="email" placeholder="email@example.com" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="password"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Password</FormLabel>
              <FormControl>
                <Input type="password" placeholder="Enter password" {...field} />
              </FormControl>
              <FormDescription>At least 8 characters</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="confirmPassword"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Confirm Password</FormLabel>
              <FormControl>
                <Input type="password" placeholder="Confirm password" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <Button type="submit" className="w-full">
          Create Account
        </Button>
      </form>
    </Form>
  );
};

export const RegistrationForm: Story = {
  render: () => <RegistrationFormComponent />,
  parameters: {
    docs: {
      description: {
        story:
          'Registration form with email, password validation, and cross-field password matching.',
      },
    },
  },
};

// Form with validation errors (pre-filled with invalid data)
const errorFormSchema = z.object({
  username: z.string().min(3, 'Username must be at least 3 characters'),
  email: z.string().email('Invalid email address'),
});

/**
 * Form with validation errors.
 * Shows error states for invalid fields.
 */
const WithErrorsFormComponent = () => {
  const form = useForm<z.infer<typeof errorFormSchema>>({
    resolver: zodResolver(errorFormSchema),
    defaultValues: {
      username: 'ab', // Too short - will trigger error
      email: 'invalid-email', // Invalid email - will trigger error
    },
    mode: 'all', // Validate on every change
  });

  // Trigger validation immediately
  form.trigger();

  return (
    <Form {...form}>
      <form className="space-y-4">
        <FormField
          control={form.control}
          name="username"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Username</FormLabel>
              <FormControl>
                <Input placeholder="Enter username" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="email"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Email</FormLabel>
              <FormControl>
                <Input type="email" placeholder="email@example.com" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <Button type="submit">Submit</Button>
      </form>
    </Form>
  );
};

export const WithErrors: Story = {
  render: () => <WithErrorsFormComponent />,
  parameters: {
    docs: {
      description: {
        story: 'Form showing validation error states with error messages.',
      },
    },
  },
};

// Complex form schema
const complexFormSchema = z.object({
  name: z.string().min(2, 'Name is required'),
  email: z.string().email('Invalid email'),
  game: z.string().min(1, 'Please select a game'),
  acceptTerms: z.boolean().refine(val => val === true, {
    message: 'You must accept the terms',
  }),
});

/**
 * Complex form with multiple input types.
 * Shows Select and Checkbox integration.
 */
const ComplexFormComponent = () => {
  const form = useForm<z.infer<typeof complexFormSchema>>({
    resolver: zodResolver(complexFormSchema),
    defaultValues: {
      name: '',
      email: '',
      game: '',
      acceptTerms: false,
    },
  });

  const onSubmit = (data: z.infer<typeof complexFormSchema>) => {
    console.log(data);
  };

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
        <FormField
          control={form.control}
          name="name"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Name</FormLabel>
              <FormControl>
                <Input placeholder="Your name" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="email"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Email</FormLabel>
              <FormControl>
                <Input type="email" placeholder="email@example.com" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="game"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Favorite Game</FormLabel>
              <Select onValueChange={field.onChange} defaultValue={field.value}>
                <FormControl>
                  <SelectTrigger>
                    <SelectValue placeholder="Select a game" />
                  </SelectTrigger>
                </FormControl>
                <SelectContent>
                  <SelectItem value="catan">Catan</SelectItem>
                  <SelectItem value="wingspan">Wingspan</SelectItem>
                  <SelectItem value="azul">Azul</SelectItem>
                  <SelectItem value="7wonders">7 Wonders</SelectItem>
                </SelectContent>
              </Select>
              <FormDescription>Your favorite board game</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="acceptTerms"
          render={({ field }) => (
            <FormItem className="flex flex-row items-start space-x-3 space-y-0">
              <FormControl>
                <Checkbox checked={field.value} onCheckedChange={field.onChange} />
              </FormControl>
              <div className="space-y-1 leading-none">
                <FormLabel>Accept terms and conditions</FormLabel>
                <FormDescription>
                  You agree to our Terms of Service and Privacy Policy.
                </FormDescription>
                <FormMessage />
              </div>
            </FormItem>
          )}
        />
        <Button type="submit" className="w-full">
          Submit
        </Button>
      </form>
    </Form>
  );
};

export const ComplexForm: Story = {
  render: () => <ComplexFormComponent />,
  parameters: {
    docs: {
      description: {
        story: 'Complex form with Input, Select, and Checkbox components.',
      },
    },
  },
};

// Profile form for MeepleAI context
const profileFormSchema = z.object({
  displayName: z.string().min(2, 'Display name is required'),
  bio: z.string().max(160, 'Bio must be 160 characters or less').optional(),
});

/**
 * MeepleAI profile form.
 * Domain-specific form for user profile editing.
 */
const ProfileFormComponent = () => {
  const form = useForm<z.infer<typeof profileFormSchema>>({
    resolver: zodResolver(profileFormSchema),
    defaultValues: {
      displayName: 'BoardGamer42',
      bio: 'Love strategy games and puzzles!',
    },
  });

  const onSubmit = (data: z.infer<typeof profileFormSchema>) => {
    console.log(data);
  };

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
        <FormField
          control={form.control}
          name="displayName"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Display Name</FormLabel>
              <FormControl>
                <Input placeholder="Your display name" {...field} />
              </FormControl>
              <FormDescription>This is your public username.</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="bio"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Bio</FormLabel>
              <FormControl>
                <Input placeholder="Tell us about yourself" {...field} />
              </FormControl>
              <FormDescription>A short bio. Maximum 160 characters.</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
        <div className="flex gap-2">
          <Button type="button" variant="outline">
            Cancel
          </Button>
          <Button type="submit">Save Profile</Button>
        </div>
      </form>
    </Form>
  );
};

export const ProfileForm: Story = {
  render: () => <ProfileFormComponent />,
  parameters: {
    docs: {
      description: {
        story: 'MeepleAI user profile form with display name and bio.',
      },
    },
  },
};

/**
 * Form label styles showcase.
 * Shows FormLabel variants and states.
 */
const LabelStylesComponent = () => {
  const form = useForm({
    defaultValues: {
      normal: '',
      error: '',
    },
    mode: 'all',
  });

  return (
    <Form {...form}>
      <form className="space-y-4">
        <FormField
          control={form.control}
          name="normal"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Normal Label</FormLabel>
              <FormControl>
                <Input placeholder="Normal field" {...field} />
              </FormControl>
              <FormDescription>Helper text appears here.</FormDescription>
            </FormItem>
          )}
        />
        <div className="space-y-2">
          <label className="font-semibold text-destructive font-quicksand">
            Error Label (simulated)
          </label>
          <Input placeholder="Field with error" className="border-destructive" />
          <p className="text-sm font-medium text-destructive">Validation error message</p>
        </div>
      </form>
    </Form>
  );
};

export const LabelStyles: Story = {
  render: () => <LabelStylesComponent />,
  parameters: {
    docs: {
      description: {
        story: 'Demonstration of FormLabel styles including error states.',
      },
    },
  },
};

/**
 * Inline form layout.
 * Horizontal form layout for compact interfaces.
 */
const InlineFormComponent = () => {
  const form = useForm({
    defaultValues: {
      search: '',
    },
  });

  return (
    <Form {...form}>
      <form className="flex items-end gap-2">
        <FormField
          control={form.control}
          name="search"
          render={({ field }) => (
            <FormItem className="flex-1">
              <FormLabel>Search Games</FormLabel>
              <FormControl>
                <Input placeholder="Enter game name..." {...field} />
              </FormControl>
            </FormItem>
          )}
        />
        <Button type="submit">Search</Button>
      </form>
    </Form>
  );
};

export const InlineForm: Story = {
  render: () => <InlineFormComponent />,
  parameters: {
    docs: {
      description: {
        story: 'Inline form layout with input and button on same row.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Shows form appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => <DefaultFormComponent />,
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
