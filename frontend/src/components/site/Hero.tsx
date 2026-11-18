import { Link } from "@tanstack/react-router";
import { ArrowRight, CalendarDays, LayoutDashboard, Users, Wallet, Scissors, Globe, Star, MapPin, Clock } from "lucide-react";
import s from "./styles";

const agenda = [
  ["09:00", "Corte + barba", "Rafael", s.evBlue],
  ["10:30", "Coloração", "Juliana", s.evGreen],
  ["11:00", "Manicure", "Paula", s.evAmber],
  ["14:00", "Escova", "Juliana", s.evBlue],
];

export function Hero() {
  return (
    <section id="topo" className={s.hero}>
      <div className={s.heroGrid} aria-hidden />
      <div className={`${s.container} ${s.heroInner}`}>
        <div className={s.heroCopy}>
          <span className={s.eyebrow}><span className={s.pulse} />Site · Agenda · Gestão</span>
          <h1 className={s.heroTitle}>Seu negócio merece uma presença digital <em>à altura.</em></h1>
          <p className={s.heroLead}>Crie seu próprio site, organize seus agendamentos e gerencie seu estabelecimento em uma única plataforma. Tudo de forma simples e inteligente.</p>
          <div className={s.heroCtas}>
            <Link to="/criar-conta" className={`${s.btn} ${s.btnPrimary} ${s.btnLg}`}>Começar agora <ArrowRight size={18} /></Link>
            <a href="#funcionalidades" className={`${s.btn} ${s.btnGhost} ${s.btnLg}`}>Conhecer a plataforma</a>
          </div>
        </div>

        <div className={s.heroVisual} role="img" aria-label="Representação ilustrativa do painel do Servia integrado ao site de um estabelecimento">
          {/* Dashboard mockup */}
          <div className={s.mockDash}>
            <div className={s.mockBar}><i /><i /><i /><span>painel.servia</span></div>
            <div className={s.dashBody}>
              <aside className={s.dashSide}>
                <b>servia.</b>
                {[LayoutDashboard, CalendarDays, Scissors, Users, Wallet, Globe].map((I, i) => (
                  <span key={i} className={i === 1 ? s.dashSideActive : ""}><I size={14} /></span>
                ))}
              </aside>
              <div className={s.dashMain}>
                <div className={s.dashHead}><strong>Agenda de hoje</strong><span>Qui, 12 nov</span></div>
                <div className={s.dashStats}>
                  <div><small>Agendamentos</small><b>18</b></div>
                  <div><small>Confirmados</small><b>15</b></div>
                  <div><small>Profissionais</small><b>4</b></div>
                </div>
                <ul className={s.dashList}>
                  {agenda.map(([t, sv, p, c]) => (
                    <li key={t}><time>{t}</time><span className={`${s.ev} ${c}`}><b>{sv}</b>{p}</span></li>
                  ))}
                </ul>
              </div>
            </div>
          </div>
          {/* Public site mockup (phone) */}
          <div className={s.mockPhone}>
            <div className={s.phoneCover}><span>Studio Lumière</span></div>
            <div className={s.phoneBody}>
              <div className={s.phoneMeta}><Star size={11} /> 4,9 · <MapPin size={11} /> Centro</div>
              <p className={s.phoneLabel}>Serviços</p>
              {[["Corte feminino", "45 min"], ["Escova modelada", "40 min"], ["Coloração", "2 h"]].map(([n, d]) => (
                <div key={n} className={s.phoneSvc}><span>{n}</span><small><Clock size={10} /> {d}</small></div>
              ))}
              <span className={s.phoneCta}>Agendar horário</span>
            </div>
          </div>
          <div className={s.syncChip}><span className={s.pulse} /> Novo agendamento pelo site</div>
          <p className={s.mockNote}>Representação ilustrativa</p>
        </div>
      </div>
    </section>
  );
}
