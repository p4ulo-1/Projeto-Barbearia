import {
  Globe,
  CalendarCheck,
  LayoutGrid,
  HeartHandshake,
  SlidersHorizontal,
  Sparkles,
  UserPlus,
  MessagesSquare,
  Eye,
  Rocket,
  Paperclip,
  TrendingUp,
  Search,
} from "lucide-react";
import s from "./styles";

export function Benefits() {
  const rest = [
    [
      CalendarCheck,
      "Agendamentos organizados",
      "Permita que seus clientes agendem serviços pela internet.",
    ],
    [
      LayoutGrid,
      "Gestão centralizada",
      "Reúna as principais atividades do estabelecimento em um único lugar.",
    ],
    [
      HeartHandshake,
      "Relacionamento com clientes",
      "Organize informações e histórico de atendimento.",
    ],
    [
      SlidersHorizontal,
      "Mais autonomia",
      "Tenha controle sobre serviços, profissionais e operação.",
    ],
  ] as const;
  return (
    <section className={s.section}>
      <div className={s.container}>
        <div className={s.sectionHead}>
          <span className={s.kicker}>Benefícios</span>
          <h2 className={s.h2}>Muito além de uma agenda.</h2>
          <p className={s.lead}>
            Presença digital, agendamento e gestão trabalhando juntos para o seu estabelecimento
            crescer.
          </p>
        </div>
        <div className={s.bento}>
          <article className={`${s.bentoItem} ${s.bentoFeature}`}>
            <Globe size={28} />
            <h3>Sua presença digital</h3>
            <p>
              Tenha um site profissional para apresentar seu negócio — com a sua identidade, seus
              serviços e sua equipe.
            </p>
            <div className={s.urlPill}>
              <Search size={14} /> seunegocio.servia
            </div>
          </article>
          <article className={`${s.bentoItem} ${s.bentoAi}`}>
            <Sparkles size={24} />
            <h3>Templates profissionais</h3>
            <p>Personalize um site desenhado para o seu segmento.</p>
          </article>
          {rest.map(([I, t, d]) => (
            <article key={t} className={`${s.bentoItem} ${s.bentoSmall}`}>
              <span className={s.iconBox}>
                <I size={20} />
              </span>
              <div>
                <h3>{t}</h3>
                <p>{d}</p>
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}

function SiteDemo() {
  return (
    <div className={s.demoSite}>
      <div className={s.demoSiteHero}>
        <b>Barbearia Norte</b>
        <small>Tradição e estilo desde 2012</small>
      </div>
      <div className={s.demoSiteRow}>
        {["Corte", "Barba", "Combo"].map((x) => (
          <span key={x}>{x}</span>
        ))}
      </div>
      <div className={s.swatches}>
        {[s.sw1, s.sw2, s.sw3].map((c) => (
          <i key={c} className={c} />
        ))}
        <small>Sua identidade visual</small>
      </div>
    </div>
  );
}
function BookingDemo() {
  return (
    <div className={s.demoCard}>
      <p className={s.demoLabel}>Escolha um horário · Sex, 13</p>
      <div className={s.slots}>
        {["09:00", "09:30", "10:00", "11:30", "14:00", "15:30"].map((t, i) => (
          <span key={t} className={i === 2 ? s.slotOn : i === 3 ? s.slotOff : ""}>
            {t}
          </span>
        ))}
      </div>
      <div className={s.proRow}>
        {["AN", "RB", "CM"].map((x, i) => (
          <span key={x} className={i === 0 ? s.proOn : ""}>
            {x}
          </span>
        ))}
        <small>Profissional</small>
      </div>
    </div>
  );
}
function ClientsDemo() {
  return (
    <div className={s.demoCard}>
      {[
        ["Mariana Costa", "12 atendimentos", "Há 3 dias"],
        ["Lucas Ferreira", "5 atendimentos", "Há 2 semanas"],
        ["Ana Beatriz", "21 atendimentos", "Hoje"],
      ].map(([n, a, l]) => (
        <div key={n} className={s.tableRow}>
          <span className={s.avatar}>{n?.[0]}</span>
          <b>{n}</b>
          <small>{a}</small>
          <small>{l}</small>
        </div>
      ))}
    </div>
  );
}
function TeamDemo() {
  const days = ["S", "T", "Q", "Q", "S", "S", "D"];
  return (
    <div className={s.demoCard}>
      {[
        ["Rafael", [1, 1, 1, 1, 1, 1, 0]],
        ["Juliana", [0, 1, 1, 1, 1, 1, 0]],
        ["Paula", [1, 1, 0, 1, 1, 0, 0]],
      ].map(([n, w]) => (
        <div key={n as string} className={s.teamRow}>
          <b>{n as string}</b>
          <div>
            {(w as number[]).map((on, i) => (
              <span key={i} className={on ? s.dayOn : ""}>
                {days[i]}
              </span>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}
function FinanceDemo() {
  const bars = [40, 55, 48, 70, 62, 85, 78];
  return (
    <div className={s.demoCard}>
      <div className={s.finHead}>
        <div>
          <small>Receita do período</small>
          <b>R$ ••••</b>
        </div>
        <span className={s.trend}>
          <TrendingUp size={14} /> evolução
        </span>
      </div>
      <div className={s.bars}>
        {bars.map((h, i) => (
          <span key={i} style={{ height: `${h}%` }} />
        ))}
      </div>
    </div>
  );
}
function BuilderDemo() {
  return (
    <div className={s.chat}>
      <div className={s.msgAi}>Identidade visual</div>
      <div className={s.msgUser}>Paleta · Areia e vinho</div>
      <div className={s.msgAi}>Logo e fotos do estabelecimento</div>
      <div className={s.msgFile}>
        <Paperclip size={14} /> logotipo.png
      </div>
      <div className={s.chatInput}>
        <span>Prévia em desktop, tablet e celular</span>
      </div>
    </div>
  );
}

const features = [
  [
    "Site próprio",
    "Um site personalizado com a identidade visual e as informações do seu estabelecimento, sem precisar contratar um desenvolvedor.",
    SiteDemo,
  ],
  [
    "Agendamento online",
    "Disponibilize serviços, profissionais e horários para que seus clientes agendem quando quiserem.",
    BookingDemo,
  ],
  [
    "Gestão de clientes",
    "Cadastros e informações dos seus clientes organizados e sempre à mão.",
    ClientsDemo,
  ],
  [
    "Gestão de equipe",
    "Administre profissionais, os serviços que cada um realiza e seus horários de trabalho.",
    TeamDemo,
  ],
  [
    "Gestão financeira",
    "Acompanhe as informações financeiras do estabelecimento com clareza.",
    FinanceDemo,
  ],
  [
    "Criação guiada",
    "Selecione seu segmento e ajuste cores, fotos e textos com prévia para desktop, tablet e celular.",
    BuilderDemo,
  ],
] as const;

export function Features() {
  return (
    <section id="funcionalidades" className={`${s.section} ${s.sectionAlt} ${s.featuresSection}`}>
      <div className={s.container}>
        <div className={s.sectionHead}>
          <span className={s.kicker}>Funcionalidades</span>
          <h2 className={s.h2}>Tudo o que seu estabelecimento precisa, em um só lugar.</h2>
        </div>
        <div className={s.features}>
          {features.map(([t, d, Demo], i) => (
            <div key={t} className={`${s.feature} ${i % 2 ? s.featureRev : ""}`}>
              <div className={s.featureText}>
                <span className={s.featureNum}>0{i + 1}</span>
                <h3 className={s.h3}>{t}</h3>
                <p>{d}</p>
              </div>
              <div className={s.featureVisual} aria-hidden>
                <Demo />
              </div>
            </div>
          ))}
        </div>
        <p className={s.mockNote}>Interfaces ilustrativas</p>
      </div>
    </section>
  );
}

export function HowItWorks() {
  const steps = [
    [
      UserPlus,
      "Cadastre seu estabelecimento",
      "Crie sua conta e informe os dados iniciais do negócio.",
    ],
    [
      MessagesSquare,
      "Personalize seu site",
      "Escolha o segmento e ajuste a identidade visual com as informações e imagens do seu estabelecimento.",
    ],
    [Eye, "Revise e aprove", "Visualize a prévia, solicite ajustes e aprove o resultado."],
    [
      Rocket,
      "Publique e gerencie",
      "Tenha seu site publicado e utilize o painel do Servia para gerenciar seu estabelecimento.",
    ],
  ] as const;
  return (
    <section id="como-funciona" className={`${s.section} ${s.sectionDark}`}>
      <div className={s.container}>
        <div className={s.sectionHead}>
          <span className={s.kicker}>Como funciona</span>
          <h2 className={s.h2}>Do cadastro ao site publicado em quatro passos.</h2>
        </div>
        <ol className={s.steps}>
          {steps.map(([I, t, d], i) => (
            <li key={t} className={s.step}>
              <div className={s.stepTop}>
                <span className={s.stepNum}>{i + 1}</span>
                <I size={20} />
              </div>
              <h3>{t}</h3>
              <p>{d}</p>
            </li>
          ))}
        </ol>
      </div>
    </section>
  );
}

export function Segments() {
  const list = [
    "Barbearias",
    "Salões de beleza",
    "Nail designers",
    "Clínicas",
    "Estúdios de estética",
    "Sobrancelhas e cílios",
    "Tatuagem",
    "Massoterapia",
    "Maquiagem",
    "Depilação",
    "Tranças",
  ];
  return (
    <section id="segmentos" className={s.section}>
      <div className={`${s.container} ${s.segments}`}>
        <div>
          <span className={s.kicker}>Segmentos</span>
          <h2 className={s.h2}>Feito para quem vive de prestar serviços.</h2>
          <p className={s.lead}>
            O Servia se adapta ao seu tipo de negócio — do atendimento individual ao estabelecimento
            com equipe.
          </p>
        </div>
        <ul className={s.chips}>
          {list.map((x) => (
            <li key={x}>{x}</li>
          ))}
          <li className={s.chipOther}>+ Outros estabelecimentos de serviços</li>
        </ul>
      </div>
    </section>
  );
}
