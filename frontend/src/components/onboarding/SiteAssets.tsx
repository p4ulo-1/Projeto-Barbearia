import { useState } from "react";
import { Alert, Button } from "@/components/ui-sv";
import { readSiteImage } from "@/lib/site-workspace";
import type { SiteImage } from "@/lib/site-templates";
import s from "./styles";

export function SiteAssets({
  label,
  images,
  onChange,
  max = 1,
  describe = false,
}: {
  label: string;
  images: SiteImage[];
  onChange: (images: SiteImage[]) => void;
  max?: number;
  describe?: boolean;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  return (
    <fieldset className={s.builderAssets}>
      <legend>{label}</legend>
      {images.map((image, index) => (
        <div className={s.builderAsset} key={index}>
          <span>
            {image.name} · {image.approved ? "Aprovada" : "Pendente"}
          </span>
          {describe && (
            <label className={s.builderField}>
              Descrição da imagem
              <input
                value={image.alt ?? ""}
                maxLength={180}
                disabled={busy}
                onChange={(event) =>
                  onChange(
                    images.map((item, i) =>
                      i === index ? { ...item, alt: event.target.value } : item,
                    ),
                  )
                }
              />
            </label>
          )}
          {!image.approved && (
            <Button
              variant="subtle"
              type="button"
              disabled={busy}
              onClick={() =>
                onChange(
                  images.map((item, i) => (i === index ? { ...item, approved: true } : item)),
                )
              }
            >
              Aprovar
            </Button>
          )}
          <Button
            variant="subtle"
            type="button"
            disabled={busy}
            onClick={() => onChange(images.filter((_, i) => i !== index))}
          >
            Remover
          </Button>
        </div>
      ))}
      {(max === 1 || images.length < max) && (
        <label className={s.builderField}>
          {images.length && max === 1 ? "Substituir arquivo" : "Escolher arquivo"}
          <input
            type="file"
            accept="image/png,image/jpeg"
            disabled={busy}
            onChange={async ({ currentTarget }) => {
              const file = currentTarget.files?.[0];
              currentTarget.value = "";
              if (!file) return;
              setBusy(true);
              setError("");
              try {
                const image = await readSiteImage(file);
                onChange(max === 1 ? [image] : [...images, image]);
              } catch (failure) {
                setError(
                  failure instanceof Error
                    ? failure.message
                    : "Não foi possível carregar a imagem.",
                );
              } finally {
                setBusy(false);
              }
            }}
          />
        </label>
      )}
      {busy && <p role="status">Validando imagem…</p>}
      {error && <Alert tone="error">{error}</Alert>}
    </fieldset>
  );
}
