import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';

afterEach(() => {
  cleanup();
  globalThis.localStorage?.clear();
  vi.restoreAllMocks();
});

// jsdom does not implement these, and the UI components rely on them.
if (!globalThis.crypto?.randomUUID) {
  Object.defineProperty(globalThis, 'crypto', {
    value: { ...globalThis.crypto, randomUUID: () => Math.random().toString(36).slice(2) },
  });
}

globalThis.URL.createObjectURL ??= () => 'blob:mock';
globalThis.URL.revokeObjectURL ??= () => undefined;
