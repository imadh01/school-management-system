import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { useNavigate } from "react-router-dom";
import { UnsavedChangesGuard } from "@/components/form/UnsavedChangesGuard";
import type { FormSectionLink } from "@/components/form/FormPage";
import { useToast } from "@/components/Toast";
import { getSaveErrors, isConcurrencyConflict } from "@/utils/apiError";
import { sectionOfField, type FormSectionDef } from "@/utils/form";

interface ShowApiErrorOptions {
  /** Prefix for keys of a request sent from a part of the form ("identity"). */
  prefix?: string;
  /** Adjusts the field keys first (e.g. remapRowErrors for repeating rows). */
  remap?: (fields: Record<string, string>) => Record<string, string>;
  /** Messages shown above the API's own, e.g. "the main record was saved, but…". */
  leadMessages?: string[];
}

interface SaveOptions<R, TForm> {
  /** Browser-side checks; return field key → message. Nothing is sent while any are returned. */
  validate?: () => Record<string, string>;
  submit: () => Promise<R>;
  /** Message when the API gives no better one. */
  fallback: string;
  remap?: (fields: Record<string, string>) => Record<string, string>;
  /** What was saved: the form now matches it, then the page moves on. */
  onSaved: (result: R) => { form: TForm; message: string; goTo: string };
}

/**
 * Everything an add/edit page needs besides its fields:
 *   - the form values, and whether they differ from what was loaded ("unsaved changes");
 *   - errors per field and for the whole form, and their count per section (for the section list);
 *   - the save flow: browser checks → request → API errors mapped onto fields → toast → next page;
 *   - "someone else saved first" (409) with a Reload button;
 *   - the unsaved-changes guard.
 *
 * A page renders `{guard}` once and spreads `pageProps` onto <FormPage>. See StudentEditPage for a
 * save with two requests (it uses the lower-level showProblems / showApiError / finishSaved).
 */
export function useRecordForm<TForm>(options: {
  sections: readonly FormSectionDef[];
  initial: TForm;
  /** "student", "parent"... used in the 409 message. */
  noun: string;
  /** Loads the record again (offered after a 409). */
  onReload?: () => void;
}) {
  const { sections, noun, onReload } = options;
  const navigate = useNavigate();
  const toast = useToast();

  const [form, setForm] = useState<TForm>(options.initial);
  const [snapshot, setSnapshot] = useState(() =>
    JSON.stringify(options.initial),
  );
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [formErrors, setFormErrors] = useState<string[]>([]);
  const [conflict, setConflict] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [leaveTo, setLeaveTo] = useState<string | null>(null);

  // Navigate only after the saved state has rendered, so the unsaved-changes guard lets us go.
  useEffect(() => {
    if (leaveTo) navigate(leaveTo);
  }, [leaveTo, navigate]);

  const isDirty = JSON.stringify(form) !== snapshot;

  /** Puts loaded (or saved) values in the form and treats them as "nothing changed". */
  const reset = useCallback((next: TForm) => {
    setForm(next);
    setSnapshot(JSON.stringify(next));
    setFieldErrors({});
    setFormErrors([]);
    setConflict(false);
  }, []);

  const patch = useCallback(
    (changes: Partial<TForm>) => setForm((f) => ({ ...f, ...changes })),
    [],
  );

  /** Browser-side problems (field key → message). */
  const showProblems = (problems: Record<string, string>) => {
    setFieldErrors(problems);
    setFormErrors([]);
    setConflict(false);
  };

  /** A failed request: fields with a known section go under the field, the rest to the banner. */
  const showApiError = (
    err: unknown,
    fallback: string,
    opts: ShowApiErrorOptions = {},
  ) => {
    if (isConcurrencyConflict(err)) {
      setConflict(true);
      setFieldErrors({});
      setFormErrors([
        ...(opts.leadMessages ?? []),
        `Someone else saved this ${noun} while you were editing. Reload to see their changes; your edits on this page will be lost.`,
      ]);
      return;
    }
    const errors = getSaveErrors(err, fallback, opts.prefix);
    const fields = opts.remap ? opts.remap(errors.fields) : errors.fields;
    const known: Record<string, string> = {};
    const rest = [...(opts.leadMessages ?? []), ...errors.form];
    for (const [key, message] of Object.entries(fields)) {
      if (sectionOfField(sections, key)) known[key] = message;
      else rest.push(message);
    }
    setConflict(false);
    setFieldErrors(known);
    setFormErrors(rest);
  };

  /** After a successful save: the form matches the saved record, a toast, then the next page. */
  const finishSaved = (saved: TForm, message: string, goTo: string) => {
    reset(saved);
    toast.success(message);
    setLeaveTo(goTo);
  };

  /** The usual save: validate → submit → finishSaved, or the errors on screen. */
  const save = async <R,>(opts: SaveOptions<R, TForm>) => {
    const problems = opts.validate?.() ?? {};
    if (Object.keys(problems).length > 0) {
      showProblems(problems);
      return;
    }
    setFieldErrors({});
    setFormErrors([]);
    setConflict(false);
    setIsSaving(true);
    try {
      const result = await opts.submit();
      const next = opts.onSaved(result);
      finishSaved(next.form, next.message, next.goTo);
    } catch (err) {
      showApiError(err, opts.fallback, { remap: opts.remap });
    } finally {
      setIsSaving(false);
    }
  };

  const sectionLinks: FormSectionLink[] = useMemo(() => {
    const counts: Record<string, number> = {};
    for (const key of Object.keys(fieldErrors)) {
      const id = sectionOfField(sections, key);
      if (id) counts[id] = (counts[id] ?? 0) + 1;
    }
    return sections.map((s) => ({
      id: s.id,
      title: s.title,
      errorCount: counts[s.id] ?? 0,
    }));
  }, [sections, fieldErrors]);

  const reloadButton: ReactNode =
    conflict && onReload ? (
      <button
        type="button"
        className="btn btn--secondary btn--sm"
        style={{ marginTop: 10 }}
        onClick={onReload}
      >
        Reload latest version
      </button>
    ) : undefined;

  return {
    form,
    setForm,
    patch,
    reset,
    isDirty,
    fieldErrors,
    isSaving,
    setIsSaving,
    save,
    showProblems,
    showApiError,
    finishSaved,
    /** Render once inside the page. */
    guard: <UnsavedChangesGuard when={isDirty && !isSaving && !leaveTo} />,
    /** Spread onto <FormPage>. */
    pageProps: {
      sections: sectionLinks,
      formErrors,
      formErrorAction: reloadButton,
      isSaving,
      isDirty,
    },
  };
}
