import { Plus } from "lucide-react";
import s from "./styles";

const faq = [
  [
    "O que é o Servia?",
    "O Servia é uma plataforma que reúne site próprio, agendamento online e gestão para estabelecimentos que trabalham com prestação de serviços.",
  ],
  [
    "Para quais tipos de estabelecimentos ele é indicado?",
    "Barbearias, salões de beleza, nail designers, clínicas, estúdios de estética, sobrancelhas e cílios, tatuagem, massoterapia, maquiagem, depilação, tranças e outros negócios de serviços.",
  ],
  [
    "Preciso ter conhecimentos técnicos para criar meu site?",
    "Não. Todo o processo é guiado e você não precisa saber programar nem entender de design.",
  ],
  [
    "Como funciona a criação do site?",
    "O Servia monta a prévia com um template do seu segmento. Você pode ajustar as cores, imagens e textos antes de revisar e aprovar o site.",
  ],
  [
    "Posso personalizar meu site?",
    "Sim. O site é configurado com as informações e a identidade do seu estabelecimento, e pode ser gerenciado pelo painel.",
  ],
  [
    "Como meus clientes realizam agendamentos?",
    "Pelo seu site, escolhendo o serviço, o profissional e um horário disponível.",
  ],
  [
    "Posso cadastrar diferentes profissionais?",
    "Sim. Você pode cadastrar sua equipe, definir os serviços de cada profissional e seus horários de trabalho.",
  ],
  [
    "Como funciona a assinatura?",
    "O Servia funciona por assinatura. Os planos e condições disponíveis são apresentados na seção de planos e no momento da contratação.",
  ],
];

export function Faq() {
  return (
    <section id="faq" className={s.section}>
      <div className={`${s.container} ${s.faqWrap}`}>
        <div>
          <span className={s.kicker}>FAQ</span>
          <h2 className={s.h2}>Perguntas frequentes</h2>
        </div>
        <div className={s.faq}>
          {faq.map(([q, a]) => (
            <details key={q} className={s.faqItem}>
              <summary>
                {q}
                <Plus size={18} aria-hidden />
              </summary>
              <p>{a}</p>
            </details>
          ))}
        </div>
      </div>
    </section>
  );
}
