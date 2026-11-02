import { FormEvent, lazy, Suspense, useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { api } from '../services/api';
import { useAuth } from '../context/auth';

const Orb = lazy(() => import('../components/Orb'));
declare global { interface Window { Razorpay: any } }
type Tool = { key: string; name: string; description: string; cost: number };
const Msg = ({ t }: { t: string }) => (t ? <p className="mt-3 text-sm text-slate-300" role="status">{t}</p> : null);
const Wrap = ({ children, narrow }: { children: React.ReactNode; narrow?: boolean }) => (
  <section className={`mx-auto px-6 py-16 ${narrow ? 'max-w-md' : 'max-w-6xl'}`}>{children}</section>
);

function useTools() {
  const [tools, setTools] = useState<Tool[]>([]);
  useEffect(() => { api<Tool[]>('/api/tools').then(setTools).catch(() => {}); }, []);
  return tools;
}

function ToolGrid() {
  const tools = useTools();
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {tools.map(t => (
        <Link key={t.key} to={`/tools/${t.key}`} className="card hover:border-violet-400/70 transition">
          <h3 className="font-display text-lg">{t.name}</h3>
          <p className="text-sm text-slate-400 mt-1">{t.description}</p>
        </Link>
      ))}
    </div>
  );
}

export function Home() {
  // Heavy 3D only when WebGL works and the visitor has not asked for reduced motion.
  const webgl = useMemo(() => {
    try { return !matchMedia('(prefers-reduced-motion: reduce)').matches && !!document.createElement('canvas').getContext('webgl'); }
    catch { return false; }
  }, []);
  return (
    <>
      <div className="fixed inset-0 -z-10">
        {webgl ? <Suspense fallback={null}><Orb /></Suspense>
               : <div className="h-full bg-[radial-gradient(60%_50%_at_70%_30%,rgba(139,92,246,.45),transparent)]" />}
      </div>
      <section className="mx-auto flex min-h-[88vh] max-w-6xl flex-col justify-center px-6">
        <h1 className="max-w-4xl font-display text-5xl font-bold leading-none md:text-8xl">Your Ideas. Powered by Intelligence.</h1>
        <p className="mt-6 max-w-xl text-lg text-slate-300">AURA is one AI workspace for writing, code, image prompts, video scripts and business ideas.</p>
        <div className="mt-8 flex flex-wrap gap-3"><Link className="btn" to="/signup">Start Creating Free</Link><Link className="btn-ghost" to="/tools">Explore AI Tools</Link></div>
      </section>
      <Wrap>
        <h2 className="mb-8 font-display text-4xl">Twelve tools. One workspace.</h2><ToolGrid />
        <h2 className="mb-8 mt-24 font-display text-4xl">From prompt to result in three steps</h2>
        <div className="grid gap-4 md:grid-cols-3">
          {['Describe it|Pick a tool and say what you need.', 'AURA generates|Your request runs on our secure server.', 'Copy, refine, save|Regenerate or find it later in your history.'].map(s => {
            const [h, p] = s.split('|');
            return <div className="card" key={h}><h3 className="font-display text-lg">{h}</h3><p className="mt-1 text-sm text-slate-400">{p}</p></div>;
          })}
        </div>
      </Wrap>
    </>
  );
}

export const ToolsPage = () => <Wrap><h1 className="mb-2 font-display text-4xl">AI Tools</h1><p className="mb-8 text-slate-400">Every tool uses credits from the same balance.</p><ToolGrid /></Wrap>;

const STYLES = ['Default', 'Short', 'Detailed', 'Professional', 'Casual'];
export function ToolPage() {
  const { key } = useParams();
  const { user, refresh } = useAuth();
  const tool = useTools().find(t => t.key === key);
  const [input, setInput] = useState(''); const [style, setStyle] = useState(STYLES[0]);
  const [out, setOut] = useState(''); const [busy, setBusy] = useState(false); const [err, setErr] = useState('');
  const [hist, setHist] = useState<any[]>([]);
  const loadHist = () => api<any[]>(`/api/history?tool=${key}&take=10`).then(setHist).catch(() => {});
  useEffect(() => { setOut(''); setErr(''); loadHist(); }, [key]);

  const generate = async () => {
    if (!input.trim()) { setErr('Write what you need first.'); return; }
    setBusy(true); setErr('');
    try {
      const r = await api('/api/ai/generate', { toolKey: key, template: style, input });
      setOut(r.output); await refresh(); loadHist();
    } catch (e: any) { setErr(e.status === 402 ? 'You are out of credits. Upgrade on the Pricing page.' : e.message); }
    setBusy(false);
  };
  if (!tool) return <Wrap><p>Loading tool…</p></Wrap>;
  return (
    <Wrap>
      <h1 className="font-display text-4xl">{tool.name}</h1><p className="mb-6 text-slate-400">{tool.description}</p>
      <div className="card">
        <div className="flex items-center justify-between text-sm text-cyan-300"><span>{user?.credits ?? 0} credits left</span><span>This costs {tool.cost}</span></div>
        <select className="input" value={style} onChange={e => setStyle(e.target.value)} aria-label="Style">{STYLES.map(s => <option key={s}>{s}</option>)}</select>
        <textarea className="input" rows={5} value={input} onChange={e => setInput(e.target.value)} placeholder="Describe what you need" aria-label="Your request" />
        <div className="flex flex-wrap gap-3"><button className="btn" disabled={busy} onClick={generate}>{busy ? 'Generating…' : 'Generate'}</button>
          <button className="btn-ghost" disabled={busy || !out} onClick={generate}>Regenerate</button>
          <button className="btn-ghost" disabled={!out} onClick={() => navigator.clipboard.writeText(out)}>Copy</button></div>
        <Msg t={err} />
      </div>
      <div className={`card mt-4 min-h-[120px] whitespace-pre-wrap ${busy ? 'animate-pulse' : ''}`} aria-live="polite">{out || 'Your result will appear here.'}</div>
      <h2 className="mb-2 mt-10 font-display text-xl">History</h2>
      {hist.length === 0 && <p className="text-sm text-slate-400">Nothing saved yet.</p>}
      {hist.map(h => <button key={h.id} className="block w-full border-t border-white/10 py-2 text-left text-sm text-slate-400 hover:text-white" onClick={() => { setInput(h.input); setOut(h.output); }}>{h.input.slice(0, 90)}</button>)}
    </Wrap>
  );
}

function loadRazorpay(): Promise<boolean> {
  return new Promise(res => {
    if (window.Razorpay) return res(true);
    const s = document.createElement('script'); s.src = 'https://checkout.razorpay.com/v1/checkout.js';
    s.onload = () => res(true); s.onerror = () => res(false); document.body.appendChild(s);
  });
}

export function Pricing() {
  const { user, refresh } = useAuth(); const nav = useNavigate();
  const [plans, setPlans] = useState<any[]>([]); const [msg, setMsg] = useState('');
  useEffect(() => { api<any[]>('/api/plans').then(setPlans).catch(() => {}); }, []);

  const buy = async (plan: string) => {
    if (!user) { nav('/signup'); return; }
    setMsg('');
    try {
      if (!(await loadRazorpay())) throw new Error('Could not load the payment window.');
      const o = await api('/api/billing/checkout', { plan });
      new window.Razorpay({
        key: o.keyId, amount: o.amount, currency: o.currency, order_id: o.orderId, name: 'AURA', description: `${plan} plan, 30 days`,
        // This only closes the window. Credits are added by the server webhook, never by this callback.
        handler: async () => {
          setMsg('Payment received. Activating your plan…');
          for (let i = 0; i < 10; i++) {
            await new Promise(r => setTimeout(r, 2000)); await refresh();
            const m = await api('/api/me'); if (m.plan === plan) { setMsg('Your plan is active.'); return; }
          }
          setMsg('Still confirming your payment. Your plan will appear shortly.');
        },
      }).open();
    } catch (e: any) { setMsg(e.message); }
  };
  return (
    <Wrap>
      <h1 className="mb-2 font-display text-4xl">Start free. Upgrade when you grow.</h1><p className="mb-8 text-slate-400">Paid plans last 30 days. Credits are added after your payment is confirmed.</p>
      <div className="grid gap-4 md:grid-cols-3">
        {plans.map(p => (
          <div key={p.name} className={`card ${p.name === 'Pro' ? 'border-violet-400 shadow-[0_0_60px_rgba(139,92,246,.35)]' : ''}`}>
            <h3 className="font-display text-xl">{p.name}</h3>
            <p className="my-2 font-display text-4xl">₹{p.priceInr}<span className="text-sm text-slate-400"> /30 days</span></p>
            <p className="mb-5 text-slate-400">{p.monthlyCredits.toLocaleString()} credits</p>
            {p.priceInr === 0 ? <Link className="btn-ghost" to="/signup">Start free</Link>
              : <button className="btn" disabled={user?.plan === p.name} onClick={() => buy(p.name)}>{user?.plan === p.name ? 'Current plan' : `Choose ${p.name}`}</button>}
          </div>
        ))}
      </div>
      <Msg t={msg} />
    </Wrap>
  );
}

export function AuthPage({ mode }: { mode: 'login' | 'signup' }) {
  const { login } = useAuth(); const nav = useNavigate();
  const [f, setF] = useState({ name: '', email: '', password: '' }); const [msg, setMsg] = useState(''); const [busy, setBusy] = useState(false);
  const submit = async (e: FormEvent) => {
    e.preventDefault(); setBusy(true); setMsg('');
    try {
      if (mode === 'signup') { const r = await api('/api/auth/signup', f); setMsg(r.message); }
      else { await login(f.email, f.password); nav('/dashboard'); }
    } catch (e: any) { setMsg(e.message); }
    setBusy(false);
  };
  const set = (k: string) => (e: React.ChangeEvent<HTMLInputElement>) => setF({ ...f, [k]: e.target.value });
  return (
    <Wrap narrow>
      <h1 className="mb-4 font-display text-3xl">{mode === 'login' ? 'Log in' : 'Create your account'}</h1>
      <form className="card" onSubmit={submit}>
        {mode === 'signup' && <input className="input" placeholder="Name" required value={f.name} onChange={set('name')} autoComplete="name" />}
        <input className="input" type="email" placeholder="Email" required value={f.email} onChange={set('email')} autoComplete="email" />
        <input className="input" type="password" placeholder="Password" required minLength={8} value={f.password} onChange={set('password')} autoComplete={mode === 'login' ? 'current-password' : 'new-password'} />
        <button className="btn mt-2" disabled={busy}>{mode === 'login' ? 'Log in' : 'Start Creating Free'}</button>
        <Msg t={msg} />
        {mode === 'login' && <p className="mt-4 text-sm"><Link className="text-cyan-300" to="/forgot-password">Forgot password?</Link></p>}
      </form>
    </Wrap>
  );
}

export function Confirm() {
  const [q] = useSearchParams(); const [msg, setMsg] = useState('Confirming…');
  useEffect(() => { api('/api/auth/confirm-email', { userId: q.get('userId'), token: q.get('token') }).then(r => setMsg(r.message)).catch(e => setMsg(e.message)); }, []);
  return <Wrap narrow><h1 className="mb-4 font-display text-3xl">Email confirmation</h1><p>{msg}</p><Link className="btn mt-6" to="/login">Log in</Link></Wrap>;
}

export function Forgot() {
  const [email, setEmail] = useState(''); const [msg, setMsg] = useState('');
  const go = async (e: FormEvent) => { e.preventDefault(); try { setMsg((await api('/api/auth/forgot-password', { email })).message); } catch (x: any) { setMsg(x.message); } };
  return <Wrap narrow><h1 className="mb-4 font-display text-3xl">Reset your password</h1><form className="card" onSubmit={go}><input className="input" type="email" required placeholder="Email" value={email} onChange={e => setEmail(e.target.value)} /><button className="btn mt-2">Send reset link</button><Msg t={msg} /></form></Wrap>;
}

export function Reset() {
  const [q] = useSearchParams(); const [pw, setPw] = useState(''); const [msg, setMsg] = useState('');
  const go = async (e: FormEvent) => { e.preventDefault(); try { setMsg((await api('/api/auth/reset-password', { email: q.get('email'), token: q.get('token'), newPassword: pw })).message); } catch (x: any) { setMsg(x.message); } };
  return <Wrap narrow><h1 className="mb-4 font-display text-3xl">Choose a new password</h1><form className="card" onSubmit={go}><input className="input" type="password" required minLength={8} placeholder="New password" value={pw} onChange={e => setPw(e.target.value)} /><button className="btn mt-2">Update password</button><Msg t={msg} /></form></Wrap>;
}

export function Dashboard() {
  const [d, setD] = useState<any>(null); const tools = useTools();
  useEffect(() => { api('/api/dashboard').then(setD).catch(() => {}); }, []);
  const name = (k: string) => tools.find(t => t.key === k)?.name ?? k;
  if (!d) return <Wrap><p>Loading…</p></Wrap>;
  return (
    <Wrap>
      <h1 className="mb-8 font-display text-4xl">Welcome{d.displayName ? `, ${d.displayName}` : ''}</h1>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div className="card"><p className="font-display text-4xl">{d.credits}</p><p className="text-slate-400">credits left</p></div>
        <div className="card"><p className="font-display text-4xl">{d.totalGenerations}</p><p className="text-slate-400">generations</p></div>
        <div className="card"><p className="font-display text-2xl">{d.favouriteTool ? name(d.favouriteTool) : 'None yet'}</p><p className="text-slate-400">most used tool</p></div>
        <div className="card"><p className="font-display text-2xl">{d.plan} plan</p><Link className="btn mt-3 !py-2" to="/pricing">Upgrade plan</Link></div>
      </div>
      <h2 className="mb-2 mt-10 font-display text-xl">Recent activity</h2>
      {d.recent.length === 0 && <p className="text-sm text-slate-400">No activity yet. Open a tool and generate something.</p>}
      {d.recent.map((r: any) => <Link key={r.id} to={`/tools/${r.toolKey}`} className="block border-t border-white/10 py-2 text-sm text-slate-400 hover:text-white"><b>{name(r.toolKey)}</b> – {r.preview}</Link>)}
    </Wrap>
  );
}

export function Admin() {
  const [s, setS] = useState<any>(null); const [users, setUsers] = useState<any[]>([]); const [msg, setMsg] = useState('');
  const load = () => { api('/api/admin/stats').then(setS).catch(e => setMsg(e.message)); api('/api/admin/users').then(setUsers).catch(() => {}); };
  useEffect(load, []);
  const grant = async (id: string) => {
    const a = Number(prompt('Credits to add (use a negative number to remove):')); if (!a) return;
    try { await api(`/api/admin/users/${id}/credits`, { amount: a }); load(); } catch (e: any) { setMsg(e.message); }
  };
  return (
    <Wrap>
      <h1 className="mb-6 font-display text-4xl">Admin</h1><Msg t={msg} />
      {s && <div className="grid gap-4 sm:grid-cols-3 lg:grid-cols-5">{Object.entries(s).map(([k, v]) => <div className="card" key={k}><p className="font-display text-3xl">{String(v)}</p><p className="text-sm text-slate-400">{k}</p></div>)}</div>}
      <h2 className="mb-2 mt-10 font-display text-xl">Users</h2>
      <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead className="text-slate-400"><tr><th>Email</th><th>Plan</th><th>Credits</th><th></th></tr></thead>
        <tbody>{users.map(u => <tr key={u.id} className="border-t border-white/10"><td className="py-2">{u.email}</td><td>{u.plan}</td><td>{u.credits}</td><td><button className="text-cyan-300" onClick={() => grant(u.id)}>Adjust credits</button></td></tr>)}</tbody></table></div>
    </Wrap>
  );
}

export const Static = ({ title, text }: { title: string; text: string }) => <Wrap narrow><h1 className="mb-4 font-display text-4xl">{title}</h1><p className="text-slate-300">{text}</p></Wrap>;
