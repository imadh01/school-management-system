import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import axios from "axios";
import { Check, Circle } from "lucide-react";
import { apiClient } from "@/services/apiClient";
import { startSession } from "../services/session";
import type { AuthResult } from "../types/auth.types";
import { useAuth } from "@/store/AuthContext";
import { useToast } from "@/components/Toast";
import { getApiErrors } from "@/utils/apiError";
import logo from "@/assets/synergein-logo.jpg";
import "./LoginPage.css";

/** The server's rules (ChangePasswordRequestValidator), shown live so nobody has to guess. */
const RULES: { label: string; test: (p: string) => boolean }[] = [
  { label: "At least 8 characters", test: (p) => p.length >= 8 },
  { label: "An uppercase letter", test: (p) => /[A-Z]/.test(p) },
  { label: "A lowercase letter", test: (p) => /[a-z]/.test(p) },
  { label: "A digit", test: (p) => /[0-9]/.test(p) },
  { label: "A special character", test: (p) => /[^a-zA-Z0-9]/.test(p) },
];

type FieldErrors = Partial<
  Record<"currentPassword" | "newPassword" | "confirm", string>
>;

/**
 * Change password (U1). Users whose MustChangePassword flag is set are sent here before
 * anything else (ProtectedRoute); others can open it too. On success the server signs out
 * every other device and returns a new session for this one.
 */
export function ChangePasswordRequiredPage() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const toast = useToast();

  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [errors, setErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const forced = user?.mustChangePassword ?? false;

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setFormError(null);

    const next: FieldErrors = {};
    if (!currentPassword) next.currentPassword = "Enter your current password.";
    if (!RULES.every((r) => r.test(newPassword)))
      next.newPassword = "The new password does not meet all the rules below.";
    if (newPassword !== confirm)
      next.confirm = "The two new passwords do not match.";
    setErrors(next);
    if (Object.keys(next).length > 0) return;

    setIsSaving(true);
    try {
      const { data } = await apiClient.post<AuthResult>(
        "/auth/change-password",
        { currentPassword, newPassword },
      );
      startSession(data); // new access token; MustChangePassword is now false
      toast.success(
        "Password changed. You were signed out on your other devices.",
      );
      navigate("/dashboard", { replace: true });
    } catch (err) {
      if (axios.isAxiosError(err) && err.response?.status === 400) {
        const fieldErrors =
          (err.response.data as { errors?: Record<string, string[]> })
            ?.errors ?? {};
        setErrors({
          currentPassword: fieldErrors.CurrentPassword?.[0],
          newPassword: fieldErrors.NewPassword?.[0],
        });
      } else {
        setFormError(getApiErrors(err, "Could not change the password.")[0]);
      }
    } finally {
      setIsSaving(false);
    }
  };

  const field = (
    id: keyof FieldErrors,
    label: string,
    value: string,
    onChange: (v: string) => void,
    autoComplete: string,
  ) => (
    <div className="login-page__field">
      <label className="login-page__label" htmlFor={id}>
        {label}
      </label>
      <input
        id={id}
        type="password"
        className="login-page__input"
        value={value}
        autoComplete={autoComplete}
        aria-invalid={errors[id] ? true : undefined}
        aria-describedby={errors[id] ? `${id}-error` : undefined}
        onChange={(e) => onChange(e.target.value)}
      />
      {errors[id] && (
        <div
          id={`${id}-error`}
          className="field__error"
          style={{ marginTop: 6 }}
        >
          {errors[id]}
        </div>
      )}
    </div>
  );

  return (
    <div className="login-page">
      <div className="login-page__brand-panel">
        <img src={logo} alt="Synergein" className="login-page__logo" />
        <p className="login-page__brand-name">Synergein School Management</p>
      </div>

      <div className="login-page__form-panel">
        <div className="login-page__card">
          <h1 className="login-page__title">Change your password</h1>
          <p className="login-page__subtitle">
            {forced
              ? `${user?.username}, you need a new password before you can continue.`
              : "Choose a new password. You will stay signed in here and be signed out everywhere else."}
          </p>

          <form onSubmit={handleSubmit} noValidate>
            {field(
              "currentPassword",
              forced ? "Temporary or current password" : "Current password",
              currentPassword,
              setCurrentPassword,
              "current-password",
            )}
            {field(
              "newPassword",
              "New password",
              newPassword,
              setNewPassword,
              "new-password",
            )}
            <ul
              className="password-rules"
              aria-label="Password rules"
              style={{ margin: "-8px 0 18px" }}
            >
              {RULES.map((r) => {
                const met = r.test(newPassword);
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
            {field(
              "confirm",
              "Repeat the new password",
              confirm,
              setConfirm,
              "new-password",
            )}

            {formError && (
              <div className="login-page__error" role="alert">
                {formError}
              </div>
            )}

            <button
              type="submit"
              className="login-page__submit"
              disabled={isSaving}
            >
              {isSaving ? "Saving…" : "Change password"}
            </button>
          </form>

          <div style={{ marginTop: 16, textAlign: "center" }}>
            {forced ? (
              <button
                type="button"
                className="btn btn--secondary"
                onClick={async () => {
                  await logout();
                  navigate("/login");
                }}
              >
                Log out instead
              </button>
            ) : (
              <button
                type="button"
                className="btn btn--secondary"
                onClick={() => navigate(-1)}
              >
                Cancel
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
