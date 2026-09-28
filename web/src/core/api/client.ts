import type { ApiResponse } from '@/shared/types/api';
import type { AuthResponse } from '@/shared/types/auth';

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api';

export class ApiRequestError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    message: string,
    public readonly validationErrors?: Record<string, string[]> | null,
  ) {
    super(message);
    this.name = 'ApiRequestError';
  }

  /** Flattens server-side validation errors into the shape react-hook-form expects. */
  get fieldErrors(): Record<string, string> {
    const result: Record<string, string> = {};
    for (const [key, messages] of Object.entries(this.validationErrors ?? {})) {
      // Server property paths look like "Request.Amount"; the form only knows "amount".
      const field = key.split('.').pop() ?? key;
      result[field.charAt(0).toLowerCase() + field.slice(1)] = messages.join(' ');
    }
    return result;
  }
}

type TokenBundle = { accessToken: string; refreshToken: string };

let tokenProvider: () => TokenBundle | null = () => null;
let onTokensRefreshed: (tokens: AuthResponse) => void = () => undefined;
let onAuthFailed: () => void = () => undefined;

export function configureApiClient(options: {
  getTokens: () => TokenBundle | null;
  onRefreshed: (tokens: AuthResponse) => void;
  onAuthFailed: () => void;
}) {
  tokenProvider = options.getTokens;
  onTokensRefreshed = options.onRefreshed;
  onAuthFailed = options.onAuthFailed;
}

/** Guarantees only one refresh round-trip happens even when several calls 401 at once. */
let refreshInFlight: Promise<string | null> | null = null;

async function refreshAccessToken(): Promise<string | null> {
  const tokens = tokenProvider();
  if (!tokens?.refreshToken) {
    return null;
  }

  refreshInFlight ??= (async () => {
    try {
      const response = await fetch(`${BASE_URL}/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: tokens.refreshToken }),
      });

      if (!response.ok) {
        return null;
      }

      const payload = (await response.json()) as ApiResponse<AuthResponse>;
      if (!payload.data) {
        return null;
      }

      onTokensRefreshed(payload.data);
      return payload.data.accessToken;
    } catch {
      return null;
    } finally {
      refreshInFlight = null;
    }
  })();

  return refreshInFlight;
}

interface RequestOptions extends Omit<RequestInit, 'body'> {
  body?: unknown;
  skipAuth?: boolean;
  raw?: boolean;
}

async function send(path: string, options: RequestOptions, accessToken: string | null): Promise<Response> {
  const headers = new Headers(options.headers);
  const isFormData = options.body instanceof FormData;

  if (!isFormData && options.body !== undefined) {
    headers.set('Content-Type', 'application/json');
  }

  if (accessToken && !options.skipAuth) {
    headers.set('Authorization', `Bearer ${accessToken}`);
  }

  return fetch(`${BASE_URL}${path}`, {
    ...options,
    headers,
    body: isFormData ? (options.body as FormData) : options.body === undefined ? undefined : JSON.stringify(options.body),
  });
}

async function toError(response: Response): Promise<ApiRequestError> {
  let code = 'request_failed';
  let message = response.statusText || 'The request failed.';
  let validationErrors: Record<string, string[]> | null = null;

  try {
    const payload = (await response.json()) as ApiResponse<unknown>;
    if (payload.error) {
      code = payload.error.code;
      message = payload.error.message;
      validationErrors = payload.error.validationErrors ?? null;
    }
  } catch {
    // Non-JSON error body (e.g. a 502 from a proxy) - keep the status text.
  }

  return new ApiRequestError(response.status, code, message, validationErrors);
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const tokens = tokenProvider();
  let response = await send(path, options, tokens?.accessToken ?? null);

  if (response.status === 401 && !options.skipAuth) {
    const refreshed = await refreshAccessToken();
    if (!refreshed) {
      onAuthFailed();
      throw await toError(response);
    }

    response = await send(path, options, refreshed);
  }

  if (!response.ok) {
    throw await toError(response);
  }

  if (options.raw) {
    return response as unknown as T;
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const payload = (await response.json()) as ApiResponse<T>;
  return payload.data as T;
}

export const api = {
  get: <T>(path: string) => request<T>(path, { method: 'GET' }),
  post: <T>(path: string, body?: unknown) => request<T>(path, { method: 'POST', body }),
  put: <T>(path: string, body?: unknown) => request<T>(path, { method: 'PUT', body }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
  upload: <T>(path: string, formData: FormData) => request<T>(path, { method: 'POST', body: formData }),
  download: (path: string, body?: unknown) => request<Response>(path, { method: 'POST', body, raw: true }),
};
