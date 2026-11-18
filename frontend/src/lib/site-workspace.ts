import type { Draft, GeneratedSite, SiteImage } from "./site-templates";

export type SiteReview = "Reviewing" | "AwaitingApproval" | "Approved" | "Published";
export interface SiteWorkspace {
  scope: string;
  step: number;
  status: SiteReview;
  draft: Draft;
  approved?: GeneratedSite;
  publication?: { slug: string; site: GeneratedSite; date: string };
}

function open() {
  return new Promise<IDBDatabase>((resolve, reject) => {
    const request = indexedDB.open("servia-site-workspace", 1);
    request.onupgradeneeded = () => {
      const workspace = request.result.createObjectStore("sites", { keyPath: "scope" });
      workspace.createIndex("publishedSlug", "publication.slug", { unique: true });
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}
async function read(value: string, published: boolean) {
  const db = await open();
  try {
    return await new Promise<SiteWorkspace | undefined>((resolve, reject) => {
      const store = db.transaction("sites").objectStore("sites");
      const request = published ? store.index("publishedSlug").get(value) : store.get(value);
      request.onsuccess = () => resolve(request.result as SiteWorkspace | undefined);
      request.onerror = () => reject(request.error);
    });
  } finally {
    db.close();
  }
}
export const loadWorkspace = (scope: string) => read(scope, false);
export async function loadPublishedDemo(slug: string) {
  const workspace = await read(slug, true);
  if (!workspace?.publication || !workspace.scope.startsWith("demo:")) return undefined;
  return { site: workspace.publication.site, owner: workspace.scope.slice("demo:".length) };
}

export async function persistWorkspace(workspace: SiteWorkspace) {
  const db = await open();
  try {
    await new Promise<void>((resolve, reject) => {
      const transaction = db.transaction("sites", "readwrite");
      transaction.objectStore("sites").put(workspace);
      transaction.oncomplete = () => resolve();
      transaction.onabort = () =>
        reject(
          transaction.error?.name === "ConstraintError"
            ? new Error("Este endereço já pertence a outro estabelecimento de demonstração.")
            : new Error(
                "Não foi possível salvar no navegador. Confira o armazenamento disponível.",
              ),
        );
    });
  } finally {
    db.close();
  }
}

export async function readSiteImage(file: File): Promise<SiteImage> {
  if (!["image/png", "image/jpeg"].includes(file.type) || file.size > 5 * 1024 * 1024)
    throw new Error("Escolha uma imagem PNG ou JPEG de até 5 MB.");
  const image = await createImageBitmap(file).catch(() => null);
  if (!image) throw new Error("Não foi possível abrir a imagem.");
  const tooLarge = image.width > 8000 || image.height > 8000;
  image.close();
  if (tooLarge) throw new Error("A imagem pode ter até 8.000 pixels por dimensão.");
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve({ src: String(reader.result), name: file.name, approved: false });
    reader.onerror = () => reject(new Error("Não foi possível ler a imagem."));
    reader.readAsDataURL(file);
  });
}
