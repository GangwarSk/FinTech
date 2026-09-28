import { describe, expect, it } from 'vitest';
import {
  daysAgoIso,
  formatCurrency,
  formatDate,
  formatFileSize,
  formatNumber,
  formatPercent,
  toIsoDate,
} from '@/shared/lib/utils';

describe('formatting helpers', () => {
  it('formats currency in the Indian numbering system', () => {
    expect(formatCurrency(1234567.89)).toContain('12,34,567.89');
    expect(formatCurrency(0)).toContain('0');
  });

  it('returns a dash for missing values', () => {
    expect(formatCurrency(null)).toBe('-');
    expect(formatNumber(undefined)).toBe('-');
    expect(formatDate(null)).toBe('-');
    expect(formatPercent(null)).toBe('-');
  });

  it('formats a signed percentage', () => {
    expect(formatPercent(12.345)).toBe('+12.3%');
    expect(formatPercent(-4)).toBe('-4.0%');
  });

  it('formats file sizes across units', () => {
    expect(formatFileSize(512)).toBe('512 B');
    expect(formatFileSize(2048)).toBe('2.0 KB');
    expect(formatFileSize(5 * 1024 * 1024)).toBe('5.00 MB');
  });

  it('rejects unparseable dates instead of rendering Invalid Date', () => {
    expect(formatDate('not-a-date')).toBe('-');
  });

  it('converts a date to an ISO day without timezone drift', () => {
    expect(toIsoDate(new Date(2024, 4, 2))).toBe('2024-05-02');
    expect(toIsoDate(null)).toBeNull();
  });

  it('computes a date N days in the past', () => {
    const result = daysAgoIso(0);
    expect(result).toBe(toIsoDate(new Date()));
  });
});
