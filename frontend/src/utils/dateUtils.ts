import i18n from '../i18n';

function getLocale(): string {
  return i18n.language === 'he' ? 'he-IL' : 'en-US';
}

export function formatDateLocal(utcDateStr: string | undefined | null): string {
  if (!utcDateStr) return '—';
  try {
    const date = new Date(utcDateStr);
    return date.toLocaleString(getLocale(), {
      timeZone: 'Asia/Jerusalem',
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return utcDateStr;
  }
}

export function formatDateOnly(utcDateStr: string | undefined | null): string {
  if (!utcDateStr) return '—';
  try {
    const date = new Date(utcDateStr);
    return date.toLocaleDateString(getLocale(), {
      timeZone: 'Asia/Jerusalem',
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  } catch {
    return utcDateStr;
  }
}

export function toInputDate(utcDateStr: string | undefined | null): string {
  if (!utcDateStr) return '';
  try {
    return new Date(utcDateStr).toISOString().split('T')[0];
  } catch {
    return '';
  }
}

export function formatCurrency(amount: number, currency: string = 'ILS'): string {
  return new Intl.NumberFormat(getLocale(), {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(amount);
}

export function formatNumber(n: number): string {
  return new Intl.NumberFormat(getLocale()).format(n);
}
