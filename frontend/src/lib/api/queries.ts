import { queryOptions } from "@tanstack/react-query";
import { onboardingService } from "./onboarding";
import { panelService } from "./panel";
import type { UserDto } from "./types";

export const onboardingSessionQuery = queryOptions({
  queryKey: ["onboarding", "session"],
  queryFn: onboardingService.getSession,
  retry: false,
});
export const onboardingPreviewQuery = queryOptions({
  queryKey: ["onboarding", "preview"],
  queryFn: onboardingService.getPreview,
  retry: false,
});
export const dashboardQuery = (user: UserDto | null, today: string) =>
  queryOptions({
    queryKey: ["panel", "dashboard", user?.id, user?.permissions, today],
    queryFn: () => panelService.getDashboard(user!, today),
    enabled: !!user,
    retry: false,
  });
