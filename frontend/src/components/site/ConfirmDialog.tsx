import { useEffect, useState, type ReactNode } from "react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Loader2 } from "lucide-react";

type Props = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: string;
  confirmLabel?: string;
  destructive?: boolean;
  requirePassword?: boolean;
  children?: ReactNode;
  onConfirm: (password: string) => Promise<void> | void;
};

/**
 * Diálogo de confirmação baseado no Dialog do shadcn/Radix:
 * foco vai para o modal ao abrir, fica preso dentro dele, fecha no ESC
 * e devolve o foco ao gatilho ao fechar.
 */
export function ConfirmDialog({
  open,
  onOpenChange,
  title,
  description,
  confirmLabel = "Confirmar",
  destructive,
  requirePassword,
  children,
  onConfirm,
}: Props) {
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!open) {
      setPassword("");
      setLoading(false);
    }
  }, [open]);

  async function handleConfirm() {
    setLoading(true);
    try {
      await onConfirm(password);
    } finally {
      setLoading(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="font-display text-xl">{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        {children}

        {requirePassword && (
          <div className="space-y-2">
            <Label htmlFor="confirm-password">Sua senha</Label>
            <Input
              id="confirm-password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter") void handleConfirm();
              }}
            />
          </div>
        )}

        <DialogFooter className="gap-2 sm:gap-2">
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={loading}>
            Voltar
          </Button>
          <Button
            variant={destructive ? "destructive" : "default"}
            onClick={() => void handleConfirm()}
            disabled={loading || (requirePassword && password.length === 0)}
          >
            {loading && <Loader2 className="size-4 animate-spin" aria-hidden />}
            {confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}