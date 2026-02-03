/**
 * Auth Components Index
 *
 * Centralized exports for authentication components
 */

export { AuthModal } from './AuthModal';
export { LoginForm } from './LoginForm';
export { RegisterForm } from './RegisterForm';
export { default as OAuthButtons } from './OAuthButtons';

// Email Verification Components (Issue #3076)
export { VerificationPending } from './VerificationPending';
export { VerificationSuccess } from './VerificationSuccess';
export { VerificationError } from './VerificationError';

// Two-Factor Authentication Components (Issue #3077)
export { TwoFactorVerification } from './TwoFactorVerification';
export { TwoFactorSetup } from './TwoFactorSetup';
export { TwoFactorRecoveryCodes } from './TwoFactorRecoveryCodes';
export { TwoFactorDisable } from './TwoFactorDisable';

export type { AuthModalProps } from './AuthModal';
export type { LoginFormProps, LoginFormData } from './LoginForm';
export type { RegisterFormProps, RegisterFormData } from './RegisterForm';
export type { VerificationPendingProps } from './VerificationPending';
export type { VerificationSuccessProps } from './VerificationSuccess';
export type { VerificationErrorProps } from './VerificationError';
export type { TwoFactorVerificationProps, TwoFactorVerificationData } from './TwoFactorVerification';
export type { TwoFactorSetupProps, TwoFactorSetupData } from './TwoFactorSetup';
export type { TwoFactorRecoveryCodesProps } from './TwoFactorRecoveryCodes';
export type { TwoFactorDisableProps, TwoFactorDisableData } from './TwoFactorDisable';
