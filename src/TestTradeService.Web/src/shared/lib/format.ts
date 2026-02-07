import dayjs from 'dayjs';

export function formatDateTime(value: string): string {
  return dayjs(value).format('DD.MM.YYYY HH:mm:ss');
}

export function formatNumber(value: number | string, digits = 2): string {
  const numeric = typeof value === 'string' ? Number(value) : value;
  if (Number.isNaN(numeric)) {
    return '—';
  }

  return new Intl.NumberFormat('ru-RU', {
    minimumFractionDigits: 0,
    maximumFractionDigits: digits
  }).format(numeric);
}

