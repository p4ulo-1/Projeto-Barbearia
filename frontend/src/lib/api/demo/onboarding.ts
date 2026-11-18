// DEMO ADAPTER — stands in for the backend AI conversation. In production the
// conversation flow is decided entirely by the backend/AI service, never by the UI.
import { ApiError } from "../http";
import type { OnboardingSessionDto, SiteConfigDto, UploadedFileDto } from "../types";
import { delay, load, save, uid, type DemoSession } from "./db";

const STEPS = [
  {
    key: "services",
    label: "Serviços",
    ask: (n: string) =>
      `Olá! Sou a assistente do Servia e vou ajudar a criar o site de ${n}. Para começar: quais serviços vocês oferecem?`,
  },
  {
    key: "hours",
    label: "Horário de atendimento",
    ask: () => "Ótimo. Quais são os dias e horários de atendimento?",
  },
  {
    key: "contact",
    label: "Contato",
    ask: () =>
      "Como os clientes podem falar com vocês? Pode informar telefone, WhatsApp ou redes sociais.",
  },
  {
    key: "about",
    label: "Sobre o negócio",
    ask: () =>
      "Conte um pouco sobre o estabelecimento: história, diferenciais ou o que torna o atendimento especial.",
  },
  {
    key: "style",
    label: "Estilo visual",
    ask: () =>
      "Como você imagina o visual do site? Cores, sensação (moderno, acolhedor, sofisticado…).",
  },
  {
    key: "files",
    label: "Logotipo e fotos",
    ask: () =>
      "Se tiver, envie o logotipo e algumas fotos do espaço ou dos trabalhos pelo clipe 📎. Não é obrigatório — quando terminar, é só me avisar.",
  },
];

const RULES = { accept: ["image/png", "image/jpeg"], maxSizeBytes: 2 * 1024 * 1024, maxFiles: 10 };

function msg(role: "assistant" | "user", content: string, attachmentIds?: string[]) {
  return { id: uid(), role, content, createdAt: new Date().toISOString(), attachmentIds };
}
const u_name = (_s: DemoSession) => "seu estabelecimento";
function progress(s: DemoSession) {
  const total = STEPS.length + 2;
  const map = {
    in_progress: Math.min(s.step + 1, STEPS.length + 1),
    generating: STEPS.length + 1,
    ready: STEPS.length + 2,
    publishing: total,
    published: total,
    failed: total,
  };
  const cur = map[s.status];
  const label =
    s.status === "in_progress"
      ? (STEPS[s.step]?.label ?? "Pronto para gerar")
      : s.status === "generating"
        ? "Gerando configuração"
        : "Prévia e publicação";
  return { current: cur, total, label };
}
function view(s: DemoSession): OnboardingSessionDto {
  const { step: _s, publishStartedAt: _p, config: _c, fileData, ...dto } = s;
  return {
    ...dto,
    files: s.files.map((f) => ({ ...f, url: fileData[f.id] })),
    progress: progress(s),
    publicUrl: undefined,
    uploadRules: RULES,
  };
}

export function createSessionFor(userId: string, establishmentName: string) {
  const db = load();
  db.sessions[userId] = {
    id: uid(),
    status: "in_progress",
    step: 0,
    messages: [msg("assistant", STEPS[0]!.ask(establishmentName))],
    files: [],
    collected: [],
    canGenerate: false,
    progress: { current: 1, total: 8, label: "" },
    uploadRules: RULES,
    fileData: {},
  };
  save(db);
}

function withSession<T>(userId: string, fn: (s: DemoSession, db: ReturnType<typeof load>) => T): T {
  const db = load();
  const s = db.sessions[userId];
  if (!s) throw new ApiError("Sessão de onboarding não encontrada.", 404);
  const r = fn(s, db);
  save(db);
  return r;
}

export const demoOnboarding = {
  async get(userId: string) {
    await delay(300);
    return withSession(userId, (s, db) => {
      if (
        s.status === "publishing" &&
        s.publishStartedAt &&
        Date.now() - s.publishStartedAt > 4000
      ) {
        s.status = "published";
        s.publishedAt = new Date().toISOString();
        const u = db.users.find((x) => x.id === userId);
        if (u) u.onboardingStatus = "published";
      }
      return view(s);
    });
  },
  async send(userId: string, content: string, attachmentIds: string[]) {
    await delay(900);
    return withSession(userId, (s) => {
      if (s.status !== "in_progress")
        throw new ApiError("A conversa não aceita mensagens neste momento.", 409);
      s.messages.push(msg("user", content, attachmentIds));
      const step = STEPS[s.step];
      if (
        step &&
        step.key !== "files" &&
        attachmentIds.length &&
        /^Enviei \d+ arquivo/.test(content)
      ) {
        s.messages.push(msg("assistant", `Recebi os arquivos, obrigada! ${step.ask(u_name(s))}`));
        return view(s);
      }
      if (step) {
        if (step.key !== "files") {
          const prev = s.collected.find((c) => c.key === step.key);
          if (prev) prev.value = content;
          else s.collected.push({ key: step.key, label: step.label, value: content });
        } else if (attachmentIds.length && !/pronto|terminei|só isso|acabei|^ok/i.test(content)) {
          s.messages.push(
            msg("assistant", "Recebi os arquivos! Pode enviar mais ou me avisar quando terminar."),
          );
          return view(s);
        }
        s.step++;
      }
      const next = STEPS[s.step];
      if (next) s.messages.push(msg("assistant", next.ask("")));
      else {
        s.canGenerate = true;
        s.messages.push(
          msg(
            "assistant",
            s.step === STEPS.length &&
              s.messages.filter((m) => m.role === "user").length === STEPS.length
              ? "Perfeito, já tenho o necessário. Quando quiser, clique em “Gerar meu site” para preparar a prévia."
              : "Anotado! Quando quiser, clique em “Gerar meu site” para atualizar a prévia.",
          ),
        );
      }
      return view(s);
    });
  },
  async upload(
    userId: string,
    file: File,
    onProgress: (p: number) => void,
  ): Promise<UploadedFileDto> {
    if (!RULES.accept.includes(file.type)) throw new ApiError("Formato não aceito.", 400);
    if (file.size > RULES.maxSizeBytes) throw new ApiError("Arquivo maior que o permitido.", 400);
    for (let p = 0; p <= 100; p += 20) {
      onProgress(p);
      await delay(120);
    }
    const data = await new Promise<string>((res, rej) => {
      const r = new FileReader();
      r.onload = () => res(r.result as string);
      r.onerror = rej;
      r.readAsDataURL(file);
    });
    return withSession(userId, (s) => {
      if (s.files.length >= RULES.maxFiles) throw new ApiError("Limite de arquivos atingido.", 400);
      const f = { id: uid(), name: file.name, contentType: file.type, size: file.size };
      s.files.push(f);
      s.fileData[f.id] = data;
      return { ...f, url: data };
    });
  },
  async generate(userId: string) {
    withSession(userId, (s) => {
      if (!s.canGenerate) throw new ApiError("Ainda faltam informações para gerar o site.", 409);
      s.status = "generating";
    });
    await delay(2800);
    const db = load();
    const u = db.users.find((x) => x.id === userId)!;
    return withSession(userId, (s, db2) => {
      const get = (k: string) => s.collected.find((c) => c.key === k)?.value;
      const images = s.files.map((f) => s.fileData[f.id]).filter(Boolean) as string[];
      const logo = s.files.find((f) => /logo/i.test(f.name));
      const config: SiteConfigDto = {
        templateId: "demo-base",
        establishment: { name: u.establishmentName, segment: u.segment, address: u.address },
        sections: [
          ["about", "Sobre nós"],
          ["services", "Serviços"],
          ["hours", "Horário de atendimento"],
          ["contact", "Contato"],
        ]
          .filter(([k]) => get(k!))
          .map(([k, t]) => ({ key: k!, title: t!, content: get(k!)! })),
        theme: { styleNotes: get("style") },
        logoUrl: logo ? s.fileData[logo.id] : undefined,
        gallery: images.filter((i) => !logo || i !== s.fileData[logo.id]),
      };
      s.config = config;
      s.status = "ready";
      const uu = db2.users.find((x) => x.id === userId);
      if (uu) uu.onboardingStatus = "ready";
      return view(s);
    });
  },
  async preview(userId: string) {
    await delay(400);
    return withSession(userId, (s) => {
      if (!s.config) throw new ApiError("A configuração ainda não foi gerada.", 404);
      return s.config;
    });
  },
  async requestChanges(userId: string) {
    await delay(300);
    return withSession(userId, (s, db) => {
      s.status = "in_progress";
      s.messages.push(msg("assistant", "Claro! O que você gostaria de ajustar no site?"));
      const u = db.users.find((x) => x.id === userId);
      if (u) u.onboardingStatus = "in_progress";
      return view(s);
    });
  },
  async approve(userId: string) {
    await delay(600);
    return withSession(userId, (s, db) => {
      if (!s.config) throw new ApiError("Nenhuma configuração para aprovar.", 409);
      s.status = "publishing";
      s.publishStartedAt = Date.now();
      const u = db.users.find((x) => x.id === userId);
      if (u) u.onboardingStatus = "publishing";
      return view(s);
    });
  },
};
