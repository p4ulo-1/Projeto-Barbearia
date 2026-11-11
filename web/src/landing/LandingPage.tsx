/**
 * Landing page institucional e comercial (F21.5).
 *
 * Segue a especificação do design system v1.0: wordmark, tokens, Hero
 * híbrido, benefícios, funcionalidades, como funciona, segmentos, planos,
 * FAQ, CTA final e footer.
 *
 * <b>Regra de honestidade:</b> a página anuncia apenas o que existe. Com
 * F16–F19 implementados, todos os recursos listados são reais — nada
 * precisa ser marcado "em breve".
 *
 * <b>Planos:</b> lidos da API (`/billing/plans`), nunca duplicados aqui.
 * Um preço no frontend divergiria do cobrado.
 */

import { useCallback, useEffect, useState } from 'react';
import {
  BarChart3,
  Bot,
  CalendarCheck,
  Globe,
  Menu,
  MessageCircle,
  Sparkles,
  Users,
  X,
} from 'lucide-react';
import type { Plan } from '../api/types';
import './tokens.css';

const API_URL = import.meta.env['VITE_API_URL'] ?? 'http://localhost:8080';

interface LandingPageProps {
  readonly onSignup: () => void;
  readonly onSignIn: () => void;
}

/** Benefícios — foco no resultado para o empreendedor. */
const BENEFITS = [
  {
    icon: Globe,
    title: 'Presença digital profissional',
    description:
      'Seu estabelecimento com site próprio, publicado em subdomínio, sem depender de agência.',
  },
  {
    icon: CalendarCheck,
    title: 'Agendamentos organizados',
    description:
      'Horários controlados, conflitos impedidos e disponibilidade sempre atualizada.',
  },
  {
    icon: BarChart3,
    title: 'Gestão centralizada',
    description:
      'Serviços, equipe, clientes e financeiro em um só lugar, sem planilhas paralelas.',
  },
  {
    icon: Users,
    title: 'Relacionamento com clientes',
    description:
      'Histórico, preferências e fidelidade para atender melhor quem já confia em você.',
  },
  {
    icon: MessageCircle,
    title: 'Automação de processos',
    description:
      'Confirmações, lembretes e cobranças acontecem sozinhos, sem trabalho manual.',
  },
  {
    icon: Sparkles,
    title: 'Personalização com IA',
    description:
      'Descreva como quer seu site e a IA monta a configuração. Você revisa antes de publicar.',
  },
];

/** Funcionalidades — todas implementadas. */
const FEATURES = [
  {
    icon: Globe,
    title: 'Site personalizado',
    items: [
      'Site próprio para o estabelecimento',
      'Seções configuráveis por componente',
      'Publicação em subdomínio',
      'Prévia antes de publicar',
    ],
  },
  {
    icon: CalendarCheck,
    title: 'Agendamentos',
    items: [
      'Agendamento online',
      'Organização de horários',
      'Gestão de disponibilidade',
      'Controle de profissionais',
    ],
  },
  {
    icon: Users,
    title: 'Clientes',
    items: [
      'Cadastro e histórico',
      'Preferências por cliente',
      'Anonimização conforme a LGPD',
    ],
  },
  {
    icon: BarChart3,
    title: 'Gestão',
    items: [
      'Gestão operacional',
      'Indicadores financeiros',
      'Comissões por profissional',
      'Controle de recursos por plano',
    ],
  },
  {
    icon: Bot,
    title: 'Inteligência artificial',
    items: [
      'Assistente para personalização do site',
      'Interpretação das preferências',
      'Configuração estruturada',
      'Prévia e aprovação antes de publicar',
    ],
  },
  {
    icon: MessageCircle,
    title: 'Integrações e automações',
    items: [
      'WhatsApp com atendimento automatizado',
      'Notificações e lembretes',
      'Pagamentos via Mercado Pago',
      'Rotinas automáticas de cobrança',
    ],
  },
];

/** Como funciona — quatro etapas. */
const STEPS = [
  {
    title: 'Cadastre seu estabelecimento',
    description: 'Informe os dados, os serviços e a identidade do seu negócio.',
  },
  {
    title: 'Converse com a IA',
    description: 'Explique como deseja que seu site seja apresentado.',
  },
  {
    title: 'Personalize e aprove',
    description: 'Visualize o resultado e solicite ajustes até ficar do seu jeito.',
  },
  {
    title: 'Publique e gerencie',
    description: 'Coloque o site no ar e administre o negócio pelo Servia.',
  },
];

/** Segmentos atendidos. */
const SEGMENTS = [
  'Barbearias',
  'Salões de beleza',
  'Clínicas e consultórios',
  'Estúdios',
  'Nail designers',
  'Estética e sobrancelhas',
  'Tatuagem',
  'Massoterapia',
  'Depilação',
  'Tranças',
];

/** Perguntas frequentes — respostas refletem o que existe. */
const FAQ = [
  {
    question: 'O que é o Servia?',
    answer:
      'Uma plataforma para negócios que trabalham com atendimento agendado. Reúne site próprio, agenda, clientes, financeiro e automação em um só lugar.',
  },
  {
    question: 'Para quais tipos de estabelecimento é indicado?',
    answer:
      'Barbearias, salões, clínicas, estúdios e qualquer negócio baseado em atendimento com hora marcada. O sistema não é preso a um segmento.',
  },
  {
    question: 'Preciso ter conhecimento técnico para criar meu site?',
    answer:
      'Não. Você descreve o que quer e a IA monta a configuração usando componentes prontos. Você revisa e aprova antes de publicar.',
  },
  {
    question: 'Como funciona a personalização com IA?',
    answer:
      'A IA interpreta suas preferências e propõe uma configuração de site. A proposta é sempre revisada por você — nada é publicado sem aprovação.',
  },
  {
    question: 'Posso utilizar meu próprio domínio?',
    answer:
      'Todo estabelecimento recebe um subdomínio próprio. O domínio personalizado está previsto e será liberado em etapa futura.',
  },
  {
    question: 'Como funciona a cobrança?',
    answer:
      'A assinatura é mensal, conforme o plano escolhido. O plano define quantos profissionais você pode ter. Ao ultrapassar o limite, é necessário subir de categoria.',
  },
  {
    question: 'Posso alterar meu plano?',
    answer:
      'Sim. A troca é feita a qualquer momento na área de assinatura. O downgrade só é aceito se a equipe atual couber no plano de destino.',
  },
  {
    question: 'Como funciona o acesso dos funcionários?',
    answer:
      'Cada membro da equipe tem seu próprio acesso, com permissões definidas pelo papel: proprietário, administrador, profissional ou recepcionista.',
  },
];

export function LandingPage({ onSignup, onSignIn }: LandingPageProps) {
  const [plans, setPlans] = useState<readonly Plan[]>([]);
  const [isMenuOpen, setMenuOpen] = useState(false);
  const [openFaq, setOpenFaq] = useState<number | null>(0);

  // Planos vem da API: preco no frontend divergiria do cobrado.
  const loadPlans = useCallback(async () => {
    try {
      const response = await fetch(`${API_URL}/api/v1/billing/plans`);

      if (response.ok) {
        setPlans((await response.json()) as readonly Plan[]);
      }
    } catch {
      // API indisponivel: a secao de planos fica oculta, nao quebra a pagina.
    }
  }, []);

  useEffect(() => {
    void loadPlans();
  }, [loadPlans]);

  return (
    <div className="landing">
      <header className="lp-header">
        <div className="lp-container lp-header-inner">
          <a href="#inicio" className="lp-wordmark">
            Servia
          </a>

          <nav className={`lp-nav${isMenuOpen ? ' lp-nav--open' : ''}`} aria-label="Navegação principal">
            <a href="#funcionalidades" onClick={() => setMenuOpen(false)}>Funcionalidades</a>
            <a href="#como-funciona" onClick={() => setMenuOpen(false)}>Como funciona</a>
            <a href="#planos" onClick={() => setMenuOpen(false)}>Planos</a>
            <a href="#faq" onClick={() => setMenuOpen(false)}>Perguntas frequentes</a>
          </nav>

          <div className="lp-header-actions">
            <button type="button" className="lp-btn lp-btn--secondary" onClick={onSignIn}>
              Entrar
            </button>
            <button type="button" className="lp-btn lp-btn--primary" onClick={onSignup}>
              Começar agora
            </button>
            <button
              type="button"
              className="lp-menu-toggle"
              onClick={() => setMenuOpen(!isMenuOpen)}
              aria-expanded={isMenuOpen}
              aria-label={isMenuOpen ? 'Fechar menu' : 'Abrir menu'}
            >
              {isMenuOpen ? <X size={20} /> : <Menu size={20} />}
            </button>
          </div>
        </div>
      </header>

      <main id="inicio">
        {/* Hero */}
        <section className="lp-hero">
          <div className="lp-container lp-hero-grid">
            <div className="lp-hero-copy">
              <h1>Seu negócio merece uma presença digital à altura.</h1>
              <p>
                Crie seu próprio site, organize seus agendamentos e gerencie seu
                estabelecimento em uma única plataforma. Tudo de forma simples e
                inteligente.
              </p>

              <div className="lp-hero-actions">
                <button type="button" className="lp-btn lp-btn--primary" onClick={onSignup}>
                  Começar agora
                </button>
                <a href="#funcionalidades" className="lp-btn lp-btn--secondary">
                  Conhecer a plataforma
                </a>
              </div>

              <p className="lp-hero-note">14 dias de teste. Sem compromisso.</p>
            </div>

            <div className="lp-hero-visual" aria-hidden="true">
              <div className="lp-mock lp-mock--panel">
                <div className="lp-mock-bar">
                  <span className="lp-mock-dot" />
                  <span className="lp-mock-dot" />
                  <span className="lp-mock-dot" />
                  <span className="lp-mock-title">Painel do estabelecimento</span>
                </div>
                <div className="lp-mock-body">
                  <div className="lp-mock-row">
                    <span className="lp-mock-label">Agendamentos hoje</span>
                    <span className="lp-mock-value">12</span>
                  </div>
                  <div className="lp-mock-row">
                    <span className="lp-mock-label">Próximo horário</span>
                    <span className="lp-mock-value">14:30</span>
                  </div>
                  <div className="lp-mock-row">
                    <span className="lp-mock-label">Profissionais ativos</span>
                    <span className="lp-mock-value">4</span>
                  </div>
                  <div className="lp-mock-row">
                    <span className="lp-mock-label">Site</span>
                    <span className="lp-mock-badge">Publicado</span>
                  </div>
                </div>
              </div>

              <div className="lp-mock lp-mock--site">
                <div className="lp-mock-bar">
                  <span className="lp-mock-dot" />
                  <span className="lp-mock-dot" />
                  <span className="lp-mock-dot" />
                  <span className="lp-mock-title">seu-negocio.servia.app</span>
                </div>
                <div className="lp-mock-hero">
                  <strong>Seu estabelecimento</strong>
                  <span>Agende seu horário</span>
                  <span className="lp-mock-cta">Agendar</span>
                </div>
                <p className="lp-mock-caption">Site gerado pela plataforma</p>
              </div>
            </div>
          </div>
        </section>

        {/* Benefícios */}
        <section className="lp-section lp-section--surface">
          <div className="lp-container">
            <div className="lp-section-header">
              <h2>Mais profissionalismo. Menos complicação.</h2>
              <p>
                O Servia reúne o que hoje está espalhado entre agenda, planilhas,
                conversas e ferramentas soltas.
              </p>
            </div>

            <div className="lp-grid lp-grid--3">
              {BENEFITS.map((benefit) => (
                <article key={benefit.title} className="lp-card">
                  <span className="lp-card-icon">
                    <benefit.icon size={20} aria-hidden="true" />
                  </span>
                  <h3>{benefit.title}</h3>
                  <p>{benefit.description}</p>
                </article>
              ))}
            </div>
          </div>
        </section>

        {/* Funcionalidades */}
        <section className="lp-section" id="funcionalidades">
          <div className="lp-container">
            <div className="lp-section-header">
              <h2>Tudo o que seu negócio precisa</h2>
              <p>Recursos disponíveis hoje, sem promessa de futuro.</p>
            </div>

            <div className="lp-grid lp-grid--3">
              {FEATURES.map((feature) => (
                <article key={feature.title} className="lp-card">
                  <span className="lp-card-icon">
                    <feature.icon size={20} aria-hidden="true" />
                  </span>
                  <h3>{feature.title}</h3>
                  <ul>
                    {feature.items.map((item) => (
                      <li key={item}>{item}</li>
                    ))}
                  </ul>
                </article>
              ))}
            </div>
          </div>
        </section>

        {/* Como funciona */}
        <section className="lp-section lp-section--surface" id="como-funciona">
          <div className="lp-container">
            <div className="lp-section-header">
              <h2>Seu estabelecimento online em poucos passos.</h2>
            </div>

            <div className="lp-steps">
              {STEPS.map((step, index) => (
                <article key={step.title} className="lp-step">
                  <span className="lp-step-number">{index + 1}</span>
                  <h3>{step.title}</h3>
                  <p>{step.description}</p>
                </article>
              ))}
            </div>
          </div>
        </section>

        {/* Segmentos */}
        <section className="lp-section">
          <div className="lp-container">
            <div className="lp-section-header">
              <h2>Para quem trabalha com atendimento</h2>
              <p>
                O Servia não é preso a um segmento. Se o seu negócio funciona com
                hora marcada, ele se adapta.
              </p>
            </div>

            <div className="lp-segments">
              {SEGMENTS.map((segment) => (
                <span key={segment} className="lp-segment">
                  {segment}
                </span>
              ))}
            </div>
          </div>
        </section>

        {/* Planos */}
        {plans.length > 0 && (
          <section className="lp-section lp-section--surface" id="planos">
            <div className="lp-container">
              <div className="lp-section-header">
                <h2>Encontre o plano ideal para seu negócio.</h2>
                <p>
                  Todos os planos incluem todos os recursos. A diferença está na
                  quantidade de profissionais e no grau de personalização do site.
                </p>
              </div>

              <div className="lp-plans">
                {plans.map((plan) => (
                  <article
                    key={plan.code}
                    className={`lp-plan${plan.code === 'PRO' ? ' lp-plan--featured' : ''}`}
                  >
                    {plan.code === 'PRO' && <span className="lp-plan-tag">Mais escolhido</span>}

                    <span className="lp-plan-name">{plan.name}</span>
                    <span className="lp-plan-price">
                      R$ {plan.monthlyPrice.toFixed(2)}
                      <span>/mês</span>
                    </span>

                    <ul>
                      <li>
                        {plan.includedProfessionals === 0
                          ? 'Profissionais ilimitados'
                          : `Até ${plan.includedProfessionals} profissionais`}
                      </li>
                      <li>Personalização do site: {plan.customizationLevel}</li>
                      <li>{plan.includedAiUnits} unidades de IA por mês</li>
                      <li>Todos os módulos incluídos</li>
                    </ul>

                    <button type="button" className="lp-btn lp-btn--primary" onClick={onSignup}>
                      Começar agora
                    </button>
                  </article>
                ))}
              </div>
            </div>
          </section>
        )}

        {/* FAQ */}
        <section className="lp-section" id="faq">
          <div className="lp-container">
            <div className="lp-section-header">
              <h2>Perguntas frequentes</h2>
            </div>

            <div className="lp-faq">
              {FAQ.map((item, index) => (
                <div key={item.question} className="lp-faq-item">
                  <button
                    type="button"
                    className="lp-faq-question"
                    onClick={() => setOpenFaq(openFaq === index ? null : index)}
                    aria-expanded={openFaq === index}
                  >
                    {item.question}
                    <span aria-hidden="true">{openFaq === index ? '−' : '+'}</span>
                  </button>

                  {openFaq === index && <p className="lp-faq-answer">{item.answer}</p>}
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* CTA final */}
        <section className="lp-cta">
          <div className="lp-container">
            <h2>Está na hora de profissionalizar a gestão do seu negócio.</h2>
            <p>
              Tenha seu site, seus agendamentos e sua gestão em um só lugar.
              Comece a construir a presença digital do seu estabelecimento.
            </p>
            <button type="button" className="lp-btn lp-btn--primary" onClick={onSignup}>
              Começar agora
            </button>
          </div>
        </section>
      </main>

      <footer className="lp-footer">
        <div className="lp-container">
          <div className="lp-footer-grid">
            <div>
              <span className="lp-wordmark">Servia</span>
              <p>
                Plataforma de gestão e presença digital para negócios de serviços
                agendados.
              </p>
            </div>

            <div>
              <h3>Produto</h3>
              <ul>
                <li><a href="#funcionalidades">Funcionalidades</a></li>
                <li><a href="#como-funciona">Como funciona</a></li>
                <li><a href="#planos">Planos</a></li>
              </ul>
            </div>

            <div>
              <h3>Suporte</h3>
              <ul>
                <li><a href="#faq">Perguntas frequentes</a></li>
                <li><button type="button" className="lp-btn lp-btn--tertiary" onClick={onSignIn}>Entrar</button></li>
              </ul>
            </div>

            <div>
              <h3>Conta</h3>
              <ul>
                <li><button type="button" className="lp-btn lp-btn--tertiary" onClick={onSignup}>Criar conta</button></li>
              </ul>
            </div>
          </div>

          <p className="lp-footer-bottom">
            © {new Date().getFullYear()} Servia. Todos os direitos reservados.
          </p>
        </div>
      </footer>
    </div>
  );
}
