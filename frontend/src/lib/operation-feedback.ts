import { ApiError } from "@/lib/api/http";

export function fieldFeedback(error: unknown) {
  return error instanceof ApiError ? Object.values(error.fieldErrors).filter(Boolean) : [];
}

export function operationErrorMessage(error: unknown) {
  if (!(error instanceof ApiError)) return "Não foi possível concluir a ação. Tente novamente.";
  switch (error.status) {
    case 0:
      return "Não foi possível conectar ao Servia. Verifique sua conexão e tente novamente.";
    case 400:
    case 409:
    case 422:
      return typeof error.message === "string" && error.message.length <= 240
        ? error.message
        : "Confira as informações e tente novamente.";
    case 401:
      return "Sua sessão expirou. Entre novamente.";
    case 403:
      return "Seu perfil não tem acesso a esta ação.";
    case 404:
      return "O registro não está mais disponível. Atualize a página.";
    case 429:
      return "Aguarde um instante antes de tentar novamente.";
    default:
      return "O Servia não conseguiu concluir a ação. Tente novamente.";
  }
}
