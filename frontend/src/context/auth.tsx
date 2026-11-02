import { createContext, ReactNode, useContext, useEffect, useState } from 'react';
import { api, setToken } from '../services/api';

export type User = { name: string | null; credits: number; plan: string; isAdmin: boolean };
type Ctx = { user: User | null; ready: boolean; login: (e: string, p: string) => Promise<void>; logout: () => void; refresh: () => Promise<void> };
const AuthCtx = createContext<Ctx>(null!);
export const useAuth = () => useContext(AuthCtx);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [ready, setReady] = useState(false);

  const refresh = async () => {
    if (!localStorage.getItem('aura_token')) { setUser(null); return; }
    try {
      const m = await api('/api/me');
      setUser({ name: m.displayName, credits: m.credits, plan: m.plan, isAdmin: localStorage.getItem('aura_admin') === '1' });
    } catch { setToken(null); setUser(null); }
  };
  useEffect(() => { refresh().finally(() => setReady(true)); }, []);

  const login = async (email: string, password: string) => {
    const r = await api('/api/auth/login', { email, password });
    setToken(r.token);
    localStorage.setItem('aura_admin', r.isAdmin ? '1' : '0'); // UI hint only. The server enforces the role.
    await refresh();
  };
  const logout = () => { setToken(null); setUser(null); };
  return <AuthCtx.Provider value={{ user, ready, login, logout, refresh }}>{children}</AuthCtx.Provider>;
}
