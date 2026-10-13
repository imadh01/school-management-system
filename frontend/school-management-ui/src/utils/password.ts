/**
 * The password rules of the API (PasswordRuleExtensions / ChangePasswordRequestValidator), shown
 * live on every page that sets a password. The API still checks them: this list only saves a round trip.
 */
export const PASSWORD_RULES: {
  label: string;
  test: (password: string) => boolean;
}[] = [
  { label: "At least 8 characters", test: (p) => p.length >= 8 },
  { label: "An uppercase letter", test: (p) => /[A-Z]/.test(p) },
  { label: "A lowercase letter", test: (p) => /[a-z]/.test(p) },
  { label: "A digit", test: (p) => /[0-9]/.test(p) },
  { label: "A special character", test: (p) => /[^a-zA-Z0-9]/.test(p) },
];

export function meetsPasswordRules(password: string): boolean {
  return PASSWORD_RULES.every((r) => r.test(password));
}
