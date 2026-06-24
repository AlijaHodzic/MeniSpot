const isLocalhost = ['localhost', '127.0.0.1'].includes(globalThis.location.hostname);

export const API_URL = isLocalhost ? 'http://localhost:5158/api' : '/api';
