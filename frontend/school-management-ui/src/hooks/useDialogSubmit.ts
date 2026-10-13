import { useState } from "react";
import { getApiErrors } from "@/utils/apiError";

/**
 * The submit step of a pop-up (confirm, reject, enrol, link a child...): busy flag, the API's
 * messages shown inside the pop-up, and closing it when the request succeeds.
 *
 *   const { isSubmitting, errors, submit } = useDialogSubmit(onClose);
 *   <Dialog errors={errors} closable={!isSubmitting} footer={<DialogFooter onSubmit={() =>
 *     submit(() => onSubmit(form), "Could not reject this application.")} ... />}>
 */
export function useDialogSubmit(onClose: () => void) {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errors, setErrors] = useState<string[]>([]);

  const submit = async (run: () => Promise<unknown>, fallback: string) => {
    setErrors([]);
    setIsSubmitting(true);
    try {
      await run();
      onClose();
    } catch (err) {
      setErrors(getApiErrors(err, fallback));
      setIsSubmitting(false);
    }
  };

  return { isSubmitting, errors, setErrors, submit };
}
