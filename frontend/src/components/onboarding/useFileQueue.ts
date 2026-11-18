import { useCallback, useState } from "react";
import { onboardingService } from "@/lib/api/onboarding";
import { ApiError } from "@/lib/api/http";
import type { OnboardingSessionDto, UploadedFileDto } from "@/lib/api/types";
import { formatBytes } from "@/lib/validation";

export interface QueuedFile {
  key: string;
  file: File;
  status: "pending" | "uploading" | "done" | "error";
  progress: number;
  error?: string | undefined;
  /** Rejected by validation before upload; never sent. */
  invalid?: boolean | undefined;
  result?: UploadedFileDto | undefined;
}

/** Files selected in the chat composer, validated against backend-provided rules, uploaded on send. */
export function useFileQueue(rules: OnboardingSessionDto["uploadRules"] | undefined, alreadySent: number) {
  const [queue, setQueue] = useState<QueuedFile[]>([]);
  const patch = (key: string, p: Partial<QueuedFile>) => setQueue((q) => q.map((f) => (f.key === key ? { ...f, ...p } : f)));

  const add = useCallback((input: FileList | File[]) => {
    const files = Array.from(input);
    setQueue((q) => {
      const next = [...q];
      for (const file of files) {
        let error: string | undefined;
        if (rules && !rules.accept.includes(file.type)) error = "Formato não aceito.";
        else if (rules && file.size > rules.maxSizeBytes) error = `Maior que ${formatBytes(rules.maxSizeBytes)}.`;
        else if (rules && alreadySent + next.filter((f) => !f.error).length >= rules.maxFiles) error = `Limite de ${rules.maxFiles} arquivos.`;
        next.push({ key: `${file.name}-${file.size}-${Math.random()}`, file, status: error ? "error" : "pending", progress: 0, error, invalid: !!error });
      }
      return next;
    });
  }, [rules, alreadySent]);

  const remove = (key: string) => setQueue((q) => q.filter((f) => f.key !== key));

  /** Uploads every pending file; returns ids of successful uploads, throws if any failed. */
  const uploadAll = async (): Promise<string[]> => {
    const ids: string[] = [];
    let failed = false;
    for (const item of queue) {
      if (item.status === "done" && item.result) { ids.push(item.result.id); continue; }
      if (item.invalid) continue;
      patch(item.key, { status: "uploading", progress: 0, error: undefined });
      try {
        const r = await onboardingService.uploadFile(item.file, (p) => patch(item.key, { progress: p }));
        patch(item.key, { status: "done", progress: 100, result: r });
        ids.push(r.id);
      } catch (e) {
        failed = true;
        patch(item.key, { status: "error", error: e instanceof ApiError ? e.message : "Falha no envio." });
      }
    }
    if (failed) throw new ApiError("Alguns arquivos não foram enviados. Remova-os ou tente novamente.", 0);
    return ids;
  };

  const clear = () => setQueue([]);
  const sendable = queue.filter((f) => !f.invalid);
  return { queue, add, remove, uploadAll, clear, hasSendable: sendable.length > 0, hasInvalid: queue.some((f) => f.invalid) };
}
