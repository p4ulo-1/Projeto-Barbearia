/**
 * Aplicação e roteamento (F14.1, F14.6, F14.7).
 *
 * Rotas públicas: site do tenant e widget de agendamento.
 * Rotas protegidas: painel do estabelecimento.
 */

import { useMemo } from 'react';
import {
  BrowserRouter,
  Navigate,
  Route,
  Routes,
  useNavigate,
} from 'react-router-dom';
import { SessionProvider, useSession } from './auth/SessionProvider';
import { createApiClient } from './auth/SessionProvider';
import { AppLayout, SignInPage } from './pages';
import { ServicesPage, TeamPage } from './pages/catalog';
import { AgendaPage, CustomersPage } from './pages/operations';
import { FinancePage, SitePage, SubscriptionPage } from './pages/management';
import { PublicSitePage, BookingWidgetPage } from './pages/public';

const BASE_URL = import.meta.env['VITE_API_URL'] ?? 'http://localhost:8080';

/** Protege rotas que exigem sessão. */
function RequireAuth({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading } = useSession();

  if (isLoading) {
    return <p role="status">Carregando…</p>;
  }

  return isAuthenticated ? <>{children}</> : <Navigate to="/entrar" replace />;
}

function AppRoutes() {
  const { signOut } = useSession();
  const navigate = useNavigate();

  const client = useMemo(
    () =>
      createApiClient(BASE_URL, () => {
        void signOut().then(() => navigate('/entrar'));
      }),
    [signOut, navigate],
  );

  return (
    <Routes>
      {/* Públicas */}
      <Route path="/" element={<PublicSitePage baseUrl={BASE_URL} />} />
      <Route path="/agendar" element={<BookingWidgetPage baseUrl={BASE_URL} />} />
      <Route path="/entrar" element={<SignInPage />} />

      {/* Protegidas */}
      <Route
        path="/painel"
        element={
          <RequireAuth>
            <AppLayout>
              <AgendaPage client={client} />
            </AppLayout>
          </RequireAuth>
        }
      />
      <Route
        path="/painel/servicos"
        element={
          <RequireAuth>
            <AppLayout>
              <ServicesPage client={client} />
            </AppLayout>
          </RequireAuth>
        }
      />
      <Route
        path="/painel/equipe"
        element={
          <RequireAuth>
            <AppLayout>
              <TeamPage client={client} />
            </AppLayout>
          </RequireAuth>
        }
      />
      <Route
        path="/painel/clientes"
        element={
          <RequireAuth>
            <AppLayout>
              <CustomersPage client={client} />
            </AppLayout>
          </RequireAuth>
        }
      />
      <Route
        path="/painel/financeiro"
        element={
          <RequireAuth>
            <AppLayout>
              <FinancePage client={client} />
            </AppLayout>
          </RequireAuth>
        }
      />
      <Route
        path="/painel/site"
        element={
          <RequireAuth>
            <AppLayout>
              <SitePage client={client} />
            </AppLayout>
          </RequireAuth>
        }
      />
      <Route
        path="/painel/assinatura"
        element={
          <RequireAuth>
            <AppLayout>
              <SubscriptionPage client={client} />
            </AppLayout>
          </RequireAuth>
        }
      />

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export function App() {
  return (
    <BrowserRouter>
      <SessionProvider baseUrl={BASE_URL}>
        <AppRoutes />
      </SessionProvider>
    </BrowserRouter>
  );
}
