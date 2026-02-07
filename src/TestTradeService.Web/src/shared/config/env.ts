export const env = {
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000',
  signalRUrl: import.meta.env.VITE_SIGNALR_URL ?? 'http://localhost:5000/hubs/market-data',
  requestTimeoutMs: Number(import.meta.env.VITE_REQUEST_TIMEOUT_MS ?? 15000)
};

