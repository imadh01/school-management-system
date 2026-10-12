import { useEffect } from "react";
import { useBlocker } from "react-router-dom";
import { ConfirmDialog } from "../ConfirmDialog";

interface Props {
  /** True while the form has changes that are not saved yet. */
  when: boolean;
}

/**
 * U2: asks before the user leaves a form with unsaved changes.
 *
 * - Inside the app (sidebar link, Back, browser back button): react-router's useBlocker holds the
 *   navigation and this shows our own dialog. Needs the data router (see routes/AppRoutes.tsx).
 * - Closing the tab or reloading: the browser's own "Leave site?" prompt (beforeunload). Browsers
 *   do not allow a custom dialog or message there.
 */
export function UnsavedChangesGuard({ when }: Props) {
  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      when && currentLocation.pathname !== nextLocation.pathname,
  );

  useEffect(() => {
    if (!when) return;
    const onBeforeUnload = (e: BeforeUnloadEvent) => {
      e.preventDefault();
      e.returnValue = ""; // older browsers need this to show the prompt
    };
    window.addEventListener("beforeunload", onBeforeUnload);
    return () => window.removeEventListener("beforeunload", onBeforeUnload);
  }, [when]);

  return (
    <ConfirmDialog
      isOpen={blocker.state === "blocked"}
      title="Leave without saving?"
      message="You have changes that are not saved. If you leave now, they will be lost."
      confirmLabel="Leave without saving"
      cancelLabel="Stay on this page"
      danger
      onConfirm={() => blocker.proceed?.()}
      onCancel={() => blocker.reset?.()}
    />
  );
}
