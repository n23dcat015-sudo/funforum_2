import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios';

declare module 'axios' {
  interface AxiosRequestConfig {
    skipAuthRefresh?: boolean;
    retriedAfterRefresh?: boolean;
  }
  interface InternalAxiosRequestConfig {
    skipAuthRefresh?: boolean;
    retriedAfterRefresh?: boolean;
  }
}

let accessToken: string | null = null;
let refreshRequest: Promise<string> | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export const api = axios.create({
  baseURL: '/api',
  withCredentials: true,
});

export function refreshAccessToken(): Promise<string> {
  refreshRequest ??= api
    .post<{ accessToken: string }>('/auth/refresh', {}, { skipAuthRefresh: true })
    .then(({ data }) => data.accessToken)
    .finally(() => { refreshRequest = null; });
  return refreshRequest;
}

api.interceptors.request.use((config) => {
  if (accessToken) config.headers.Authorization = `Bearer ${accessToken}`;
  return config;
});

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const config = error.config as InternalAxiosRequestConfig | undefined;
    if (
      error.response?.status !== 401 ||
      !config ||
      config.skipAuthRefresh ||
      config.retriedAfterRefresh ||
      config.url?.includes('/auth/')
    ) return Promise.reject(error);

    try {
      const nextToken = await refreshAccessToken();
      setAccessToken(nextToken);
      window.dispatchEvent(new CustomEvent('funforum:session-refreshed', { detail: nextToken }));
      config.retriedAfterRefresh = true;
      config.headers.Authorization = `Bearer ${nextToken}`;
      return api(config);
    } catch (refreshError) {
      setAccessToken(null);
      window.dispatchEvent(new Event('funforum:session-expired'));
      return Promise.reject(refreshError);
    }
  },
);