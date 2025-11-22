/**
 * Form Validation Schemas (FE-IMP-006)
 *
 * Zod schemas for form validation with Italian error messages.
 */

import { z } from 'zod';

// ============================================================================
// Error Messages (Italian)
// ============================================================================

const errorMessages = {
  required: 'Campo obbligatorio',
  email: 'Email non valida',
  passwordMin: 'La password deve contenere almeno 8 caratteri',
  passwordPattern:
    'La password deve contenere maiuscole, minuscole e numeri',
  passwordMatch: 'Le password non coincidono',
} as const;

// ============================================================================
// Login Form Schema
// ============================================================================

export const loginFormSchema = z.object({
  email: z
    .string({ message: errorMessages.required })
    .min(1, errorMessages.required)
    .email(errorMessages.email),
  password: z
    .string({ message: errorMessages.required })
    .min(1, errorMessages.required),
});

export type LoginFormData = z.infer<typeof loginFormSchema>;

// ============================================================================
// Register Form Schema
// ============================================================================

const passwordSchema = z
  .string({ message: errorMessages.required })
  .min(8, errorMessages.passwordMin)
  .regex(
    /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)/,
    errorMessages.passwordPattern
  );

export const registerFormSchema = z
  .object({
    email: z
      .string({ message: errorMessages.required })
      .min(1, errorMessages.required)
      .email(errorMessages.email),
    displayName: z.string().optional(),
    password: passwordSchema,
    confirmPassword: z
      .string({ message: errorMessages.required })
      .min(1, errorMessages.required),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: errorMessages.passwordMatch,
    path: ['confirmPassword'],
  });

export type RegisterFormData = z.infer<typeof registerFormSchema>;

// ============================================================================
// Export Chat Form Schema
// ============================================================================

export const exportChatFormSchema = z.object({
  chatId: z.string().min(1, 'Chat ID mancante'),
  format: z.enum(['pdf', 'txt', 'md'], {
    message: 'Seleziona un formato',
  }),
  dateFrom: z.string().optional(),
  dateTo: z.string().optional(),
});

export type ExportChatFormData = z.infer<typeof exportChatFormSchema>;
