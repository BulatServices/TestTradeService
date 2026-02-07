import { ZodType } from 'zod';
import { env } from '../config/env';
import { ApiContractError, ApiError } from './errors';

interface RequestOptions extends RequestInit {
  query?: Record<string, string | number | undefined | null>;
}

function withQuery(path: string, query?: RequestOptions['query']) {
  if (!query || Object.keys(query).length === 0) {
    return path;
  }

  const params = new URLSearchParams();
  Object.entries(query).forEach(([key, value]) => {
    if (value !== undefined && value !== null) {
      params.set(key, String(value));
    }
  });

  return `${path}?${params.toString()}`;
}

export async function requestJson<T>(
  path: string,
  schema: ZodType<T>,
  options?: RequestOptions
): Promise<T> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), env.requestTimeoutMs);

  try {
    const response = await fetch(`${env.apiBaseUrl}${withQuery(path, options?.query)}`, {
      ...options,
      headers: {
        'Content-Type': 'application/json',
        ...(options?.headers ?? {})
      },
      signal: controller.signal
    });

    if (!response.ok) {
      const fallback = 'Ошибка запроса к серверу';
      const text = await response.text();
      throw new ApiError(text || fallback, response.status);
    }

    const json = await response.json();
    const parsed = schema.safeParse(json);
    if (!parsed.success) {
      throw new ApiContractError();
    }

    return parsed.data;
  } finally {
    clearTimeout(timeout);
  }
}

