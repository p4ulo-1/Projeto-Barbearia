/**
 * Página de cadastro público (F21.4).
 *
 * Destino do CTA "Começar agora". O visitante cria o estabelecimento com
 * trial de 14 dias e entra direto no painel.
 *
 * A mensagem de erro vem do servidor: o cliente não inventa texto. Um
 * slug já usado retorna 409 e a mensagem explica o conflito.
 */

import { useState } from 'react';
import { ApiRequestError } from '../api/client';
import { setAccessToken, setTenantId, type AuthTokens } from '../api/client';
import './tokens.css';

const API_URL = import.meta.env['VITE_API_URL'] ?? 'http://localhost:8080';

const SEGMENTS = [
  { value: 1, label: 'Barbearia' },
  { value: 2, label: 'Salão de beleza' },
  { value: 3, label: 'Clínica ou consultório' },
  { value: 4, label: 'Estúdio' },
  { value: 5, label: 'Outro negócio de serviços' },
] as const;

interface SignupPageProps {
  readonly onSuccess: () => void;
  readonly onBack: () => void;
}

export function SignupPage({ onSuccess, onBack }: SignupPageProps) {
  const [slug, setSlug] = useState('');
  const [name, setName] = useState('');
  const [segment, setSegment] = useState('1');
  const [document, setDocument] = useState('');
  const [ownerName, setOwnerName] = useState('');
  const [ownerEmail, setOwnerEmail] = useState('');
  const [ownerPassword, setOwnerPassword] = useState('');

  const [isSubmitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: React.FormEvent): Promise<void> {
    event.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
      const response = await fetch(`${API_URL}/api/v1/public/signup`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          slug,
          name,
          segment: Number(segment),
          document,
          ownerName,
          ownerEmail,
          ownerPassword,
        }),
      });

      if (!response.ok) {
        const body = (await response.json().catch(() => ({}))) as { message?: string };
        setError(body.message ?? 'Não foi possível concluir o cadastro.');

        return;
      }

      const created = (await response.json()) as { tenantId: string };

      // Entra direto: o proprietario acabou de definir a senha.
      const signIn = await fetch(`${API_URL}/api/auth/sign-in`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ tenantId: created.tenantId, email: ownerEmail, password: ownerPassword }),
      });

      if (signIn.ok) {
        const tokens = (await signIn.json()) as AuthTokens;

        setTenantId(created.tenantId);
        setAccessToken(tokens);
        onSuccess();

        return;
      }

      // Cadastro criado, mas login automatico falhou: pede login manual.
      setError('Conta criada. Entre com seu e-mail e senha.');
    } catch (exception) {
      setError(
        exception instanceof ApiRequestError
          ? exception.message
          : 'Falha de conexão. Tente novamente.',
      );
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="landing">
      <header className="lp-header">
        <div className="lp-container lp-header-inner">
          <button type="button" className="lp-wordmark" onClick={onBack}>
            Servia
          </button>
        </div>
      </header>

      <main className="lp-section">
        <div className="lp-container">
          <div className="lp-signup">
            <h1 style={{ fontSize: '1.75rem' }}>Crie sua conta</h1>
            <p style={{ marginTop: 'var(--lp-space-2)', color: 'var(--lp-text-secondary)' }}>
              14 dias de teste. Sem compromisso.
            </p>

            <form onSubmit={(event) => void handleSubmit(event)}>
              <div className="lp-field">
                <label htmlFor="signup-name">Nome do estabelecimento</label>
                <input
                  id="signup-name"
                  value={name}
                  onChange={(event) => setName(event.target.value)}
                  required
                  maxLength={150}
                />
              </div>

              <div className="lp-field">
                <label htmlFor="signup-slug">Endereço do site</label>
                <input
                  id="signup-slug"
                  value={slug}
                  onChange={(event) => setSlug(event.target.value.toLowerCase())}
                  required
                  pattern="[a-z0-9][a-z0-9-]{1,38}[a-z0-9]"
                  placeholder="minha-barbearia"
                />
                <p className="lp-field-help">
                  Letras minúsculas, números e hífen. Será usado como subdomínio.
                </p>
              </div>

              <div className="lp-field">
                <label htmlFor="signup-segment">Segmento</label>
                <select
                  id="signup-segment"
                  value={segment}
                  onChange={(event) => setSegment(event.target.value)}
                >
                  {SEGMENTS.map((item) => (
                    <option key={item.value} value={item.value}>
                      {item.label}
                    </option>
                  ))}
                </select>
              </div>

              <div className="lp-field">
                <label htmlFor="signup-document">CNPJ ou CPF</label>
                <input
                  id="signup-document"
                  value={document}
                  onChange={(event) => setDocument(event.target.value)}
                  required
                  maxLength={20}
                />
              </div>

              <div className="lp-field">
                <label htmlFor="signup-owner">Seu nome</label>
                <input
                  id="signup-owner"
                  value={ownerName}
                  onChange={(event) => setOwnerName(event.target.value)}
                  required
                  maxLength={150}
                />
              </div>

              <div className="lp-field">
                <label htmlFor="signup-email">E-mail</label>
                <input
                  id="signup-email"
                  type="email"
                  value={ownerEmail}
                  onChange={(event) => setOwnerEmail(event.target.value)}
                  required
                  maxLength={200}
                  autoComplete="email"
                />
              </div>

              <div className="lp-field">
                <label htmlFor="signup-password">Senha</label>
                <input
                  id="signup-password"
                  type="password"
                  value={ownerPassword}
                  onChange={(event) => setOwnerPassword(event.target.value)}
                  required
                  minLength={8}
                  maxLength={200}
                  autoComplete="new-password"
                />
                <p className="lp-field-help">Mínimo de 8 caracteres.</p>
              </div>

              {error !== null && (
                <p role="alert" className="lp-error">
                  {error}
                </p>
              )}

              <button type="submit" className="lp-btn lp-btn--primary" disabled={isSubmitting}>
                {isSubmitting ? 'Criando conta…' : 'Criar conta'}
              </button>
            </form>
          </div>
        </div>
      </main>
    </div>
  );
}
