import { ApiError, IS_DEMO } from "./http";
import { demoOnboarding } from "./demo/onboarding";
import { userIdFrom } from "./demo/db";
import { tokenStore } from "@/lib/auth/token";

const me = () => {
  if (!IS_DEMO) throw new ApiError("Use a configuração assistida conectada à API do Servia.", 501);
  return userIdFrom(tokenStore.get()) ?? "";
};

export const onboardingService = {
  getSession: () => demoOnboarding.get(me()),
  sendMessage: (content: string, ids: string[]) => demoOnboarding.send(me(), content, ids),
  uploadFile: (file: File, progress: (value: number) => void, _signal?: AbortSignal) =>
    demoOnboarding.upload(me(), file, progress),
  generate: () => demoOnboarding.generate(me()),
  getPreview: () => demoOnboarding.preview(me()),
  requestChanges: () => demoOnboarding.requestChanges(me()),
  approve: () => demoOnboarding.approve(me()),
};
