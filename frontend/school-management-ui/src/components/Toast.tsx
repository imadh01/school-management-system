import {
  createContext,
  useCallback,
  useContext,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { CircleAlert, CircleCheck, Info, X } from "lucide-react";

type ToastKind = "success" | "error" | "info";

interface ToastItem {
  id: number;
  kind: ToastKind;
  message: string;
}

interface ToastApi {
  success: (message: string) => void;
  error: (message: string) => void;
  info: (message: string) => void;
}

const ToastContext = createContext<ToastApi | undefined>(undefined);

const ICONS = { success: CircleCheck, error: CircleAlert, info: Info };
const DURATION_MS = { success: 4000, info: 5000, error: 8000 };

/**
 * U1: short messages in the corner instead of window.alert (which blocks the page and looks
 * different in every browser). The region is aria-live, so screen readers announce each one.
 */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const nextId = useRef(1);

  const dismiss = useCallback((id: number) => {
    setToasts((list) => list.filter((t) => t.id !== id));
  }, []);

  const show = useCallback(
    (kind: ToastKind, message: string) => {
      const id = nextId.current++;
      setToasts((list) => [...list.slice(-3), { id, kind, message }]);
      window.setTimeout(() => dismiss(id), DURATION_MS[kind]);
    },
    [dismiss],
  );

  const api: ToastApi = {
    success: (m) => show("success", m),
    error: (m) => show("error", m),
    info: (m) => show("info", m),
  };

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div className="toast-region" role="status" aria-live="polite">
        {toasts.map((t) => {
          const Icon = ICONS[t.kind];
          return (
            <div key={t.id} className={`toast toast--${t.kind}`}>
              <Icon className="toast__icon" size={18} aria-hidden="true" />
              <span className="toast__text">{t.message}</span>
              <button
                type="button"
                className="toast__close"
                aria-label="Dismiss message"
                onClick={() => dismiss(t.id)}
              >
                <X size={16} aria-hidden="true" />
              </button>
            </div>
          );
        })}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast(): ToastApi {
  const context = useContext(ToastContext);
  if (!context) throw new Error("useToast must be used within a ToastProvider");
  return context;
}
