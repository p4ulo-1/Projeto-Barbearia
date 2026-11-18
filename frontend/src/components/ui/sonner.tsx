import { Toaster as Sonner } from "sonner";
import s from "@/components/ui-sv/styles";

export function Toaster() {
  return (
    <Sonner
      position="bottom-right"
      visibleToasts={3}
      closeButton
      containerAriaLabel="Notificações"
      toastOptions={{
        className: s.toast,
        descriptionClassName: s.toastDescription,
        closeButtonAriaLabel: "Fechar notificação",
      }}
    />
  );
}
