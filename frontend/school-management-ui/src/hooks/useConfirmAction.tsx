import { useState, type ReactNode } from "react";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { useToast } from "@/components/Toast";
import { getApiErrors } from "@/utils/apiError";

interface Options<T> {
  title: string;
  message: (target: T) => string;
  confirmLabel: string;
  /** Red confirm button (delete, unlink). */
  danger?: boolean;
  /** The request. Throwing shows `failure` (or the API's own message) as an error toast. */
  run: (target: T) => Promise<void>;
  success: (target: T) => string;
  failure: string;
}

/**
 * "Are you sure?" + the request + a toast, for one kind of action on a page (delete, unlink,
 * deactivate). Render `dialog` once and call `ask(target)` from any button.
 *
 *   const remove = useConfirmAction<ParentResponse>({ title: "Delete parent", ..., run: (p) => ... });
 *   <IconButton onClick={() => remove.ask(p)} />   ...   {remove.dialog}
 */
export function useConfirmAction<T>(options: Options<T>): {
  ask: (target: T) => void;
  dialog: ReactNode;
} {
  const toast = useToast();
  const [target, setTarget] = useState<T | null>(null);

  const confirm = async () => {
    const current = target;
    setTarget(null);
    if (current === null) return;
    try {
      await options.run(current);
      toast.success(options.success(current));
    } catch (err) {
      toast.error(getApiErrors(err, options.failure)[0]);
    }
  };

  const dialog = (
    <ConfirmDialog
      isOpen={target !== null}
      title={options.title}
      message={target === null ? "" : options.message(target)}
      confirmLabel={options.confirmLabel}
      danger={options.danger}
      onConfirm={confirm}
      onCancel={() => setTarget(null)}
    />
  );

  return { ask: setTarget, dialog };
}
