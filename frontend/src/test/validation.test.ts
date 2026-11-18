import { describe, expect, it } from "vitest";
import { isValidDocument, maskDocument, isStrongPassword, validateRegister } from "@/lib/validation";

describe("document", () => {
  it("formats CPF", () => expect(maskDocument("52998224725")).toBe("529.982.247-25"));
  it("formats CNPJ", () => expect(maskDocument("11222333000181")).toBe("11.222.333/0001-81"));
  it("accepts valid CPF", () => expect(isValidDocument("529.982.247-25")).toBe(true));
  it("rejects invalid CPF", () => expect(isValidDocument("529.982.247-24")).toBe(false));
  it("accepts valid CNPJ", () => expect(isValidDocument("11.222.333/0001-81")).toBe(true));
});
describe("password", () => {
  it("requires 8 chars, upper, lower, number", () => {
    expect(isStrongPassword("Abcdefg1")).toBe(true);
    expect(isStrongPassword("abcdefg1")).toBe(false);
    expect(isStrongPassword("Abc1")).toBe(false);
  });
});
describe("register", () => {
  it("document is optional, confirmation must match", () => {
    const e = validateRegister({ establishmentName: "Studio", address: "Rua A, 10", segment: "Tatuagem", document: "", ownerName: "Ana", email: "a@b.com", password: "Abcdefg1", confirmPassword: "x" });
    expect(e.document).toBeUndefined();
    expect(e.confirmPassword).toBeDefined();
  });
});
