/**
 * Componentes de formulário compartilhados (F18).
 *
 * Centralizam o estado de envio, o tratamento de erro e o feedback ao
 * usuário. Sem isso, cada página repetiria a mesma lógica — e cada uma
 * trataria o erro de um jeito diferente.
 */

import { useCallback, useState, type ReactNode } from 'react';
import { ApiRequestError } from '../api/client';

/** Estado de uma operação de escrita. */
export interface SubmitState {
  readonly isSubmitting: boolean;
  readonly error: string | null;
  readonly success: string | null;
}

/** Hook que encapsula envio, erro e confirmação. */
export function useSubmit() {
  const [state, setState] = useState<SubmitState>({
    isSubmitting: false,
    error: null,
    success: null,
  });

  const submit = useCallback(
    async (operation: () => Promise<void>, successMessage: string): Promise<boolean> => {
      setState({ isSubmitting: true, error: null, success: null });

      try {
        await operation();
        setState({ isSubmitting: false, error: null, success: successMessage });

        return true;
      } catch (exception) {
        // A mensagem vem do servidor: o cliente não inventa texto de erro.
        const message =
          exception instanceof ApiRequestError
            ? exception.message
            : 'Não foi possível concluir a operação.';

        setState({ isSubmitting: false, error: message, success: null });

        return false;
      }
    },
    [],
  );

  const reset = useCallback(() => {
    setState({ isSubmitting: false, error: null, success: null });
  }, []);

  return { ...state, submit, reset };
}

/** Mensagem de erro com papel acessível. */
export function FormError({ message }: { message: string | null }) {
  if (message === null) {
    return null;
  }

  return (
    <p role="alert" className="form-error">
      {message}
    </p>
  );
}

/** Mensagem de confirmação. */
export function FormSuccess({ message }: { message: string | null }) {
  if (message === null) {
    return null;
  }

  return (
    <p role="status" className="form-success">
      {message}
    </p>
  );
}

/** Campo de texto com rótulo associado. */
export function Field({
  id,
  label,
  type = 'text',
  value,
  onChange,
  required = false,
  min,
  max,
  step,
  placeholder,
  help,
}: {
  readonly id: string;
  readonly label: string;
  readonly type?: string;
  readonly value: string;
  readonly onChange: (value: string) => void;
  readonly required?: boolean;
  readonly min?: number | string;
  readonly max?: number | string;
  readonly step?: number | string;
  readonly placeholder?: string;
  readonly help?: string;
}) {
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <input
        id={id}
        type={type}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        required={required}
        {...(min !== undefined ? { min } : {})}
        {...(max !== undefined ? { max } : {})}
        {...(step !== undefined ? { step } : {})}
        {...(placeholder !== undefined ? { placeholder } : {})}
        aria-describedby={help !== undefined ? `${id}-help` : undefined}
      />
      {help !== undefined && (
        <p id={`${id}-help`} className="field-help">
          {help}
        </p>
      )}
    </div>
  );
}

/** Botão com estado de carregamento. */
export function SubmitButton({
  children,
  isSubmitting,
  label = 'Salvar',
}: {
  readonly children?: ReactNode;
  readonly isSubmitting: boolean;
  readonly label?: string;
}) {
  return (
    <button type="submit" disabled={isSubmitting} className="btn-primary">
      {isSubmitting ? 'Enviando…' : (children ?? label)}
    </button>
  );
}

/** Painel recolhível para formulários de criação. */
export function FormPanel({
  title,
  isOpen,
  onToggle,
  children,
}: {
  readonly title: string;
  readonly isOpen: boolean;
  readonly onToggle: () => void;
  readonly children: ReactNode;
}) {
  return (
    <section className="form-panel">
      <button
        type="button"
        className="btn-secondary"
        onClick={onToggle}
        aria-expanded={isOpen}
      >
        {isOpen ? 'Cancelar' : title}
      </button>

      {isOpen && <div className="form-panel-body">{children}</div>}
    </section>
  );
}
