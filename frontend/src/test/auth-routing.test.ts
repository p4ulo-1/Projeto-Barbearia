import { describe, expect, it } from "vitest";
import { homeFor } from "@/lib/auth/AuthProvider";
import type { UserDto } from "@/lib/api/types";

const u = (s: UserDto["onboardingStatus"]) => ({
  id: "1",
  name: "",
  email: "",
  establishmentName: "",
  segment: "",
  onboardingStatus: s,
});
describe("post-login destination", () => {
  it("pending card authorization goes to billing", () =>
    expect(homeFor({ ...u("in_progress"), billingSetupRequired: true })).toBe(
      "/painel/assinatura",
    ));
  it("incomplete onboarding goes to the chat", () =>
    expect(homeFor(u("in_progress"))).toBe("/onboarding"));
  it("generated config goes to preview", () =>
    expect(homeFor(u("ready"))).toBe("/onboarding/previa"));
  it("published goes to the panel", () => expect(homeFor(u("published"))).toBe("/painel"));
});
