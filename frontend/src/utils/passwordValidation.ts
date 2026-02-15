import type { TFunction } from 'i18next';

export interface PasswordError {
  hasError: boolean;
  message: string;
}

/**
 * Validates password against backend Identity rules:
 * - Min 8 characters
 * - At least one digit
 * - At least one uppercase letter
 * - At least one lowercase letter
 * - At least one non-alphanumeric character
 */
export function validatePassword(password: string, t: TFunction): PasswordError {
  if (password.length < 8) {
    return { hasError: true, message: t('validation.passwordMinLength') };
  }
  if (!/[0-9]/.test(password)) {
    return { hasError: true, message: t('validation.passwordRequireDigit') };
  }
  if (!/[A-Z]/.test(password)) {
    return { hasError: true, message: t('validation.passwordRequireUpper') };
  }
  if (!/[a-z]/.test(password)) {
    return { hasError: true, message: t('validation.passwordRequireLower') };
  }
  if (!/[^a-zA-Z0-9]/.test(password)) {
    return { hasError: true, message: t('validation.passwordRequireSpecial') };
  }
  return { hasError: false, message: '' };
}
