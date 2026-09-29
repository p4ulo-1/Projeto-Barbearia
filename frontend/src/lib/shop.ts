import barber1 from "@/assets/barber-1.jpg";
import barber2 from "@/assets/barber-2.jpg";
import barber3 from "@/assets/barber-3.jpg";
import gallery1 from "@/assets/gallery-1.jpg";
import gallery2 from "@/assets/gallery-2.jpg";
import gallery3 from "@/assets/gallery-3.jpg";
import gallery4 from "@/assets/gallery-4.jpg";
import type { ApiBarber, Service } from "@/services/api";

export const SHOP = {
  name: "New Age",
  tagline: "Barbearia boutique",
  phoneLabel: "(21) 4002-8922",
  whatsapp: "5521940028922",
  email: "contato@newagebarber.com.br",
  address: "Calçada da Fama, 412 — Várzea, Teresópolis — RJ",
  maps: "https://maps.google.com/?q=Cal%C3%A7ada+da+Fama+V%C3%A1rzea+Teres%C3%B3polis+RJ",
  hours: [
    { day: "Segunda", open: "Fechado" },
    { day: "Terça a Sexta", open: "09:00 — 20:00" },
    { day: "Sábado", open: "09:00 — 18:00" },
    { day: "Domingo", open: "Fechado" },
  ],
};

export type { Service } from "@/services/api";

const SERVICE_ORDER = [
  "corte-classico",
  "corte-navalhado",
  "barba-terapia",
  "combo-premium",
  "pigmentacao",
  "kids",
];

export function orderServices(services: Service[]) {
  return [...services].sort((left, right) => {
    const leftIndex = SERVICE_ORDER.indexOf(left.id);
    const rightIndex = SERVICE_ORDER.indexOf(right.id);
    if (leftIndex === -1 && rightIndex === -1) return left.name.localeCompare(right.name);
    if (leftIndex === -1) return 1;
    if (rightIndex === -1) return -1;
    return leftIndex - rightIndex;
  });
}

export type Barber = Omit<ApiBarber, "workDays"> & {
  photo: string;
  workdays: number[]; // 0 = domingo
};

const BARBER_VISUALS: Record<string, { photo: string; order: number }> = {
  rafael: { photo: barber1, order: 0 },
  tiago: { photo: barber2, order: 1 },
  helena: { photo: barber3, order: 2 },
};

export function decorateBarbers(barbers: ApiBarber[]): Barber[] {
  return barbers
    .map(({ workDays, ...barber }) => ({
      ...barber,
      workdays: workDays,
      photo: BARBER_VISUALS[barber.id]?.photo ?? barber1,
    }))
    .sort((left, right) => {
      const leftOrder = BARBER_VISUALS[left.id]?.order ?? Number.MAX_SAFE_INTEGER;
      const rightOrder = BARBER_VISUALS[right.id]?.order ?? Number.MAX_SAFE_INTEGER;
      return leftOrder - rightOrder || left.name.localeCompare(right.name);
    });
}

export const GALLERY = [
  { src: gallery1, alt: "Barbeiro finalizando um degradê com máquina" },
  { src: gallery2, alt: "Navalha sobre toalha quente" },
  { src: gallery3, alt: "Barba sendo aparada na tesoura" },
  { src: gallery4, alt: "Lounge de couro da barbearia" },
];

export const TESTIMONIALS = [
  {
    name: "Bruno Salgado",
    role: "Cliente há 3 anos",
    text: "Nunca mais troquei. O Rafael acerta o corte no detalhe e o ambiente parece um clube privado.",
    rating: 5,
  },
  {
    name: "Diego Ferrari",
    role: "Arquiteto",
    text: "O Combo Premium virou meu ritual quinzenal. Agendo pelo site em 30 segundos e nunca esperei além do horário.",
    rating: 5,
  },
  {
    name: "Marcelo Kubo",
    role: "Cliente desde a inauguração",
    text: "A Helena salvou meu cabelo cacheado. Atendimento técnico de verdade, sem enrolação.",
    rating: 5,
  },
];

export const brl = (value: number) =>
  value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
