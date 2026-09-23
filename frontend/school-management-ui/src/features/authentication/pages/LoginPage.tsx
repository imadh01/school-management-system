import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { authService } from "../services/authService";
import { useAuth } from "../../../store/AuthContext";
import logo from "../../../assets/synergein-logo.jpg";
import "./LoginPage.css";

export function LoginPage() {
  const [usernameOrEmail, setUsernameOrEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const { login } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      const result = await authService.login({ usernameOrEmail, password });
      login(result);
      navigate("/");
    } catch {
      setError("Invalid username/email or password.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="login-page">
      <div className="login-page__brand-panel">
        <img src={logo} alt="Synergein" className="login-page__logo" />
        <h1 className="login-page__brand-name">Synergein School Management</h1>
        <p className="login-page__brand-tagline">
          One system for admissions, academics, attendance, fees, and everything
          in between.
        </p>
      </div>

      <div className="login-page__form-panel">
        <div className="login-page__card">
          <h2 className="login-page__title">Welcome back</h2>
          <p className="login-page__subtitle">
            Sign in to continue to your dashboard
          </p>

          <form onSubmit={handleSubmit}>
            <div className="login-page__field">
              <label className="login-page__label" htmlFor="usernameOrEmail">
                Username or email
              </label>
              <input
                id="usernameOrEmail"
                type="text"
                className="login-page__input"
                value={usernameOrEmail}
                onChange={(e) => setUsernameOrEmail(e.target.value)}
                required
              />
            </div>
            <div className="login-page__field">
              <label className="login-page__label" htmlFor="password">
                Password
              </label>
              <input
                id="password"
                type="password"
                className="login-page__input"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
              />
            </div>
            {error && <div className="login-page__error">{error}</div>}
            <button
              type="submit"
              className="login-page__submit"
              disabled={isSubmitting}
            >
              {isSubmitting ? "Signing in…" : "Sign in"}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
