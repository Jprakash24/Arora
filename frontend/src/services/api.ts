const BASE: string = import.meta.env.VITE_API_URL ?? 'http://localhost:5000';
let token: string | null = localStorage.getItem('aura_token');

export function setToken(t: string | null) {
  token = t;
  if (t) localStorage.setItem('aura_token', t); else localStorage.removeItem('aura_token');
}

export async function api<T = any>(path: string, body?: unknown): Promise<T> {
  const res = await fetch(BASE + path, {
    method: body === undefined ? 'GET' : 'POST',
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const data = await res.json().catch(() => ({}));
  if (!res.ok) throw Object.assign(new Error(data.error ?? 'Something went wrong. Try again.'), { status: res.status });
  return data as T;
}
