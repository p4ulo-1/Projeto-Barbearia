import * as Dialog from "@radix-ui/react-dialog";
import s from "./styles";

export function TenantGallery({ photos }: { photos: { src: string; alt: string }[] }) {
  return (
    <ul className={s.tenantGallery}>
      {photos.map(({ src, alt }, index) => (
        <li key={index}>
          <Dialog.Root>
            <Dialog.Trigger asChild>
              <button type="button" aria-label={`Ampliar foto ${index + 1}`}>
                <img src={src} alt={alt} width={640} height={480} loading="lazy" />
              </button>
            </Dialog.Trigger>
            <Dialog.Portal>
              <Dialog.Overlay className={s.galleryOverlay} />
              <Dialog.Content className={s.galleryDialog}>
                <Dialog.Title>Foto {index + 1}</Dialog.Title>
                <Dialog.Description>{alt}</Dialog.Description>
                <img src={src} alt={alt} width={960} height={720} />
                <Dialog.Close asChild>
                  <button type="button">Fechar</button>
                </Dialog.Close>
              </Dialog.Content>
            </Dialog.Portal>
          </Dialog.Root>
        </li>
      ))}
    </ul>
  );
}
