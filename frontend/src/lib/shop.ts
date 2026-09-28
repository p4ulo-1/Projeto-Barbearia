import barber1 from "@/assets/barber-1.jpg";
import barber2 from "@/assets/barber-2.jpg";
import barber3 from "@/assets/barber-3.jpg";
import gallery1 from "@/assets/gallery-1.jpg";
import gallery2 from "@/assets/gallery-2.jpg";
import gallery3 from "@/assets/gallery-3.jpg";
import gallery4 from "@/assets/gallery-4.jpg";

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

export type Service = {
  id: string;
  name: string;
  description: string;
  price: number;
  duration: number; // minutos
  highlight?: boolean;
};

export const SERVICES: Service[] = [
  {
    id: "corte-classico",
    name: "Corte Clássico",
    description: "Tesoura e máquina, finalização com pomada e toalha quente.",
    price: 35,
    duration: 45,
  },
  {
    id: "corte-navalhado",
    name: "Corte Navalhado",
    description: "Fade fechado com acabamento na navalha e contorno milimétrico.",
    price: 40,
    duration: 60,
    highlight: true,
  },
  {
    id: "barba-terapia",
    name: "Barba Terapia",
    description: "Toalha quente, óleo essencial, navalha e balm calmante.",
    price: 35,
    duration: 40,
  },
  {
    id: "combo-premium",
    name: "Combo Premium",
    description: "Corte navalhado + barba terapia, com dose de whisky por conta da casa.",
    price: 70,
    duration: 90,
    highlight: true,
  },
  {
    id: "pigmentacao",
    name: "Pigmentação de Barba",
    description: "Correção de falhas com pigmento à prova d'água.",
    price: 30,
    duration: 30,
  },
  {
    id: "kids",
    name: "Corte Infantil",
    description: "Para os cavalheiros de até 10 anos, com paciência inclusa.",
    price: 25,
    duration: 30,
  },
];

export type Barber = {
  id: string;
  name: string;
  role: string;
  bio: string;
  photo: string;
  workdays: number[]; // 0 = domingo
  start: string;
  end: string;
};

export const BARBERS: Barber[] = [
  {
    id: "rafael",
    name: "Rafael Nunes",
    role: "Master barber & sócio",
    bio: "18 anos de ofício. Especialista em cortes clássicos e barba desenhada.",
    photo: barber1,
    workdays: [2, 3, 4, 5, 6],
    start: "09:00",
    end: "20:00",
  },
  {
    id: "tiago",
    name: "Tiago Marques",
    role: "Barbeiro sênior",
    bio: "Referência em fades e degradês navalhados de alta precisão.",
    photo: barber2,
    workdays: [2, 3, 4, 5, 6],
    start: "10:00",
    end: "20:00",
  },
  {
    id: "helena",
    name: "Helena Duarte",
    role: "Barbeira & colorista",
    bio: "Cortes texturizados, pigmentação e cuidados com cabelos cacheados.",
    photo: barber3,
    workdays: [3, 4, 5, 6],
    start: "09:00",
    end: "18:00",
  },
];

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

export const serviceById = (id: string) => SERVICES.find((s) => s.id === id);
export const barberById = (id: string) => BARBERS.find((b) => b.id === id);