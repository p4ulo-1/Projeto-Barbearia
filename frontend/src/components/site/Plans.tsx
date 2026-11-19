import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { Check, CloudOff, RefreshCw } from "lucide-react";
import { IS_DEMO } from "@/lib/api/http";
import { planBenefits, plansService } from "@/lib/api/plans";
import s from "./styles";

export function Plans() {
  const q = useQuery({ queryKey: ["plans"], queryFn: plansService.list, retry: 1 });
  return (
    <section id="planos" className={`${s.section} ${s.sectionAlt}`}>
      <div className={s.container}>
        <div className={s.sectionHead}>
          <span className={s.kicker}>Planos</span>
          <h2 className={s.h2}>Escolha o plano ideal para o seu negócio.</h2>
        </div>
        {q.isPending && (
          <div className={s.plans} aria-busy>
            {[0, 1, 2].map((i) => (
              <div key={i} className={`${s.plan} ${s.skeleton}`} />
            ))}
          </div>
        )}
        {q.isError && (
          <div className={s.stateBox} role="status">
            <CloudOff size={28} />
            <h3>Os planos não puderam ser carregados agora.</h3>
            <p>
              Tente novamente em instantes ou crie sua conta para conhecer as opções disponíveis.
            </p>
            <div className={s.heroCtas}>
              <button className={`${s.btn} ${s.btnGhost}`} onClick={() => q.refetch()}>
                <RefreshCw size={16} /> Tentar novamente
              </button>
              <Link to="/criar-conta" className={`${s.btn} ${s.btnPrimary}`}>
                Criar conta
              </Link>
            </div>
          </div>
        )}
        {q.data &&
          (q.data.length === 0 ? (
            <div className={s.stateBox}>
              <h3>
                {IS_DEMO
                  ? "Os preços serão exibidos quando a API do Servia estiver conectada."
                  : "Nenhum plano disponível no momento."}
              </h3>
            </div>
          ) : (
            <div className={s.plans}>
              {q.data.map((p) => (
                <article key={p.code} className={s.plan}>
                  <h3>{p.name}</h3>
                  <div className={s.price}>
                    {new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(
                      p.monthlyPrice,
                    )}
                    <small>/mês</small>
                  </div>
                  <ul>
                    {planBenefits(p).map((benefit) => (
                      <li key={benefit}>
                        <Check size={16} aria-hidden />
                        {benefit}
                      </li>
                    ))}
                  </ul>
                  <Link to="/criar-conta" className={`${s.btn} ${s.btnGhost}`}>
                    Criar conta
                  </Link>
                </article>
              ))}
            </div>
          ))}
      </div>
    </section>
  );
}
