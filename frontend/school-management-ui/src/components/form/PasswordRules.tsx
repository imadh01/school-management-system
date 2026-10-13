import { Check, Circle } from "lucide-react";
import { PASSWORD_RULES } from "@/utils/password";

/** The password checklist that ticks as you type. Each rule says "met" / "not met yet" to screen readers. */
export function PasswordRules({
  password,
  id,
}: {
  password: string;
  id?: string;
}) {
  return (
    <ul className="password-rules" id={id} aria-label="Password rules">
      {PASSWORD_RULES.map((r) => {
        const met = r.test(password);
        return (
          <li key={r.label} className={met ? "met" : undefined}>
            {met ? (
              <Check size={14} aria-hidden="true" />
            ) : (
              <Circle size={12} aria-hidden="true" />
            )}
            {r.label}
            <span className="visually-hidden">
              {met ? " (met)" : " (not met yet)"}
            </span>
          </li>
        );
      })}
    </ul>
  );
}
