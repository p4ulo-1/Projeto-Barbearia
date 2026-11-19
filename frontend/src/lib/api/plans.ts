import { http, IS_DEMO } from "./http";

export interface PlanDto {
  code: string;
  name: string;
  monthlyPrice: number;
  includedProfessionals: number;
  customizationLevel: string;
  includedAiUnits: number;
}

export function planBenefits(plan: PlanDto): string[] {
  const level = (
    {
      Basic: "básica",
      Intermediate: "intermediária",
      Advanced: "avançada",
      Full: "completa",
    } as Record<string, string>
  )[plan.customizationLevel];
  return [
    plan.includedProfessionals === 0
      ? "Profissionais elegíveis ilimitados"
      : `Até ${plan.includedProfessionals} profissionais elegíveis`,
    ...(level ? [`Personalização do site: ${level}`] : []),
  ];
}

export const realPlans = {
  list: () => http<PlanDto[]>("/api/v1/billing/plans", { anonymous: true }),
};
export const demoPlans: typeof realPlans = {
  async list() {
    return [];
  },
};
export const plansService = IS_DEMO ? demoPlans : realPlans;
