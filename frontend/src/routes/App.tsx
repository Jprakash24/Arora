import { ReactNode } from 'react';
import { Link, Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from '../context/auth';
import { Home, ToolsPage, ToolPage, Pricing, AuthPage, Dashboard, Admin, Static, Confirm, Forgot, Reset } from '../pages';

function Protected({ children, admin }: { children: ReactNode; admin?: boolean }) {
  const { user, ready } = useAuth();
  if (!ready) return <p className="p-10">Loading…</p>;
  if (!user) return <Navigate to="/login" replace />;
  if (admin && !user.isAdmin) return <Navigate to="/dashboard" replace />;
  return <>{children}</>;
}

export default function App() {
  const { user, logout } = useAuth();
  return (
    <>
      <header className="fixed top-0 inset-x-0 z-20 flex items-center justify-between px-6 py-3 backdrop-blur bg-[#050816]/60 border-b border-white/10">
        <Link to="/" className="font-display text-2xl font-bold">AURA</Link>
        <nav className="flex items-center gap-5 text-sm text-slate-300">
          <Link to="/tools">Tools</Link><Link to="/pricing">Pricing</Link>
          <Link className="hidden sm:inline" to="/about">About</Link>
          {user ? (<><Link to="/dashboard" className="btn !py-2">Dashboard</Link><button onClick={logout}>Log out</button></>)
                : (<><Link to="/login">Log in</Link><Link to="/signup" className="btn !py-2">Start free</Link></>)}
        </nav>
      </header>
      <main className="pt-16 min-h-screen">
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/tools" element={<ToolsPage />} />
          <Route path="/tools/:key" element={<Protected><ToolPage /></Protected>} />
          <Route path="/pricing" element={<Pricing />} />
          <Route path="/login" element={<AuthPage mode="login" />} />
          <Route path="/signup" element={<AuthPage mode="signup" />} />
          <Route path="/confirm-email" element={<Confirm />} />
          <Route path="/forgot-password" element={<Forgot />} />
          <Route path="/reset-password" element={<Reset />} />
          <Route path="/dashboard" element={<Protected><Dashboard /></Protected>} />
          <Route path="/admin" element={<Protected admin><Admin /></Protected>} />
          <Route path="/about" element={<Static title="About AURA" text="AURA is one workspace for the AI work people do every day: writing, planning, coding and creating. One place, one balance of credits, one saved history." />} />
          <Route path="/contact" element={<Static title="Contact" text="Email us at hello@aura.example. Replace this address with your real support email." />} />
          <Route path="/privacy" element={<Static title="Privacy Policy" text="Draft for legal review. We store your account details, credits, prompts and generated results so the product works. Prompts are sent to our AI provider to produce results. Payments are handled by Razorpay. Replace this draft with a lawyer-reviewed policy before launch." />} />
          <Route path="/terms" element={<Static title="Terms and Conditions" text="Draft for legal review. AI output can be wrong, so check it. Credits have no cash value. Paid plans last 30 days and do not renew automatically. Replace this draft with lawyer-reviewed terms before launch." />} />
          <Route path="*" element={<Static title="Page not found" text="That page doesn’t exist." />} />
        </Routes>
      </main>
      <footer className="text-center text-sm text-slate-400 py-8 border-t border-white/10">
        <Link className="mx-3" to="/privacy">Privacy</Link><Link className="mx-3" to="/terms">Terms</Link><Link className="mx-3" to="/contact">Contact</Link>
        <p className="mt-2">© 2026 AURA. One Platform. Infinite Possibilities.</p>
      </footer>
    </>
  );
}
