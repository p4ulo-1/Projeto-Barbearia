import { describe, expect, it } from "vitest";
import { canAccess, hasPermission } from "@/lib/permissions";

describe("panel permissions", () => {
  it("owner and administrator can view billing, only owner can manage it", () => {
    expect(canAccess({ role: "owner" }, "assinatura")).toBe(true);
    expect(canAccess({ role: "admin" }, "assinatura")).toBe(true);
    expect(hasPermission({ role: "admin" }, "ManageBilling")).toBe(false);
  });
  it("reception can view agenda, customers and financial reports", () => {
    expect(canAccess({ role: "receptionist" }, "clientes")).toBe(true);
    expect(canAccess({ role: "receptionist" }, "agenda")).toBe(true);
    expect(canAccess({ role: "receptionist" }, "financeiro")).toBe(true);
    expect(hasPermission({ role: "receptionist" }, "ManageFinancialEntries")).toBe(false);
  });
  it("a professional cannot manage services or team", () => {
    expect(canAccess({ role: "professional" }, "servicos")).toBe(true);
    expect(hasPermission({ role: "professional" }, "ManageServices")).toBe(false);
    expect(canAccess({ role: "professional" }, "equipe")).toBe(false);
  });
  it("a user without a role gets only the dashboard", () => {
    expect(canAccess({}, "dashboard")).toBe(true);
    expect(canAccess({}, "agenda")).toBe(false);
  });
});
