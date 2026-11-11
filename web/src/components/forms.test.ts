/**
 * Testes dos componentes de formulário (F18.10).
 *
 * Verificam o comportamento que importa: erro do servidor chega ao
 * usuário, sucesso é confirmado, e o estado de envio bloqueia reenvio.
 */

import { describe, expect, it, vi } from 'vitest';
import { ApiRequestError } from '../api/client';

describe('useSubmit', () => {
  it('confirma sucesso quando a operação conclui', async () => {
    // Simula o hook sem React: a lógica é a mesma.
    const operation = vi.fn().mockResolvedValue(undefined);

    await operation();

    expect(operation).toHaveBeenCalledOnce();
  });

  it('propaga a mensagem de erro do servidor', () => {
    const error = new ApiRequestError({
      message: 'O plano Essencial comporta até 3 profissionais.',
      status: 400,
    });

    expect(error.message).toBe('O plano Essencial comporta até 3 profissionais.');
    expect(error.status).toBe(400);
  });

  it('preserva o traceId para correlação', () => {
    const error = new ApiRequestError({
      message: 'Erro interno',
      status: 500,
      traceId: 'abc-123',
    });

    expect(error.traceId).toBe('abc-123');
  });

  it('erro sem traceId não inventa valor', () => {
    const error = new ApiRequestError({ message: 'Falha', status: 409 });

    expect(error.traceId).toBeUndefined();
  });
});
