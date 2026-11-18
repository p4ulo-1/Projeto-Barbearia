import { ApiError, http, upload } from "./http";
import { latestSession } from "./auth";
import {
  onboardingPath,
  sessionPath,
  type Asset,
  type Configuration,
  type Session,
} from "./contracts";

export interface Message {
  id?: string;
  role: string;
  content?: string;
  text?: string;
  message?: string;
  createdAt?: string;
}

const post = <T>(path: string, json?: unknown) => http<T>(path, { method: "POST", json });

export const realOnboarding = {
  latest: latestSession,
  start: (businessName: string, segment: number) =>
    post<Session>(`${onboardingPath}/start`, { businessName, segment }),
  messages: (id: string) => http<Message[]>(`${sessionPath(id)}/messages`),
  send: (id: string, message: string) =>
    post<{ question: string; isComplete: boolean; status: Session["status"] }>(
      `${sessionPath(id)}/messages`,
      { message },
    ),
  assets: (id: string) => http<Asset[]>(`${sessionPath(id)}/assets`),
  async upload(id: string, kind: string, file: File) {
    if (
      !/\.(png|jpe?g)$/i.test(file.name) ||
      (/\.png$/i.test(file.name) ? file.type !== "image/png" : file.type !== "image/jpeg") ||
      file.size > 5 * 1024 * 1024
    )
      throw new ApiError("Escolha uma imagem PNG ou JPEG de até 5 MB.", 400);
    const image = await createImageBitmap(file).catch(() => null);
    if (!image) throw new ApiError("Não foi possível abrir esta imagem.", 400);
    const tooLarge = image.width > 8000 || image.height > 8000;
    image.close();
    if (tooLarge) throw new ApiError("A imagem deve ter até 8.000 pixels por dimensão.", 400);
    const form = new FormData();
    form.append("kind", kind);
    form.append("file", file);
    return upload<Asset>(`${sessionPath(id)}/assets`, form, () => undefined);
  },
  assetDecision: (id: string, asset: string, approve: boolean, reason?: string) =>
    post<Asset>(
      `${sessionPath(id)}/assets/${encodeURIComponent(asset)}/${approve ? "approve" : "reject"}`,
      approve ? undefined : { reason },
    ),
  generate: (id: string) => post<Configuration>(`${sessionPath(id)}/generate`),
  preview: (id: string) => http<Configuration>(`${sessionPath(id)}/preview`),
  adjust: (id: string, instruction: string) =>
    post<Configuration>(`${sessionPath(id)}/adjustments`, { instruction }),
  submit: (id: string) => post<Session>(`${sessionPath(id)}/submit`),
  approve: (id: string) => post<Session>(`${sessionPath(id)}/approve`),
  publish: (id: string, subdomain: string) =>
    post<{ subdomain: string; version: number; publishedAt: string }>(
      `${sessionPath(id)}/publish`,
      { subdomain },
    ),
};
