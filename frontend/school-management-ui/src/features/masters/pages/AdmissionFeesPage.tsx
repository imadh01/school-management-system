import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Banknote } from "lucide-react";
import { admissionFeeService } from "../services/admissionFeeService";
import { PageHero } from "@/components/PageHero";
import { Panel } from "@/components/Panel";
import { Notice } from "@/components/Notice";
import { PageLoadError, PageLoading } from "@/components/PageState";
import { useToast } from "@/components/Toast";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCan } from "@/hooks/useCan";
import { Permission } from "@/routes/permissions";
import { getApiErrors } from "@/utils/apiError";

/** One fee per class (not per year). Saved class by class; an empty box means no fee is set. */
export function AdmissionFeesPage() {
  const can = useCan();
  const canEdit = can(Permission.SettingsEdit);
  const toast = useToast();
  const fees = useAsyncData(
    () => admissionFeeService.getAll(),
    [],
    "Could not load admission fees.",
  );
  const [values, setValues] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState<string | null>(null);
  const [rowErrors, setRowErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (fees.data)
      setValues(
        Object.fromEntries(
          fees.data.map((f) => [f.className, f.amount?.toString() ?? ""]),
        ),
      );
  }, [fees.data]);

  if (fees.isLoading && !fees.data) return <PageLoading />;
  if (fees.error || !fees.data)
    return (
      <PageLoadError
        title="Could not open admission fees"
        message={fees.error ?? "Please try again."}
        backTo="/masters"
        backLabel="Back to Masters"
      />
    );

  const save = async (className: string) => {
    const text = (values[className] ?? "").trim();
    const amount = Number(text);
    if (text !== "" && (Number.isNaN(amount) || amount < 0)) {
      setRowErrors((e) => ({ ...e, [className]: "Enter a number, 0 or more." }));
      return;
    }
    setRowErrors((e) => ({ ...e, [className]: "" }));
    setSaving(className);
    try {
      if (text === "") await admissionFeeService.clear(className);
      else await admissionFeeService.set(className, amount);
      toast.success(`Fee for ${className} saved.`);
      await fees.reload();
    } catch (error) {
      setRowErrors((e) => ({
        ...e,
        [className]: getApiErrors(error, "Could not save this fee.")[0],
      }));
    } finally {
      setSaving(null);
    }
  };

  const savedOf = (className: string) =>
    fees.data?.find((f) => f.className === className)?.amount?.toString() ?? "";

  return (
    <>
      <PageHero
        title="Admission fees"
        subtitle="One fee per class, suggested when an admission is confirmed"
        actions={
          <Link className="btn btn--secondary" to="/masters">
            Back to Masters
          </Link>
        }
      />
      <Panel
        title="Fee by class"
        subtitle={`${fees.data.length} classes`}
        icon={Banknote}
        padded
      >
        <Notice style={{ marginBottom: 14 }}>
          The clerk can still change the amount when confirming an admission. Leave a box empty
          for no standard fee.
        </Notice>
        {fees.data.length === 0 && <p>No classes exist yet.</p>}
        <div style={{ display: "grid", gap: 10, maxWidth: 520 }}>
          {fees.data.map((f) => {
            const dirty = (values[f.className] ?? "") !== savedOf(f.className);
            return (
              <div
                key={f.className}
                style={{ display: "flex", gap: 10, alignItems: "center" }}
              >
                <label
                  htmlFor={`fee-${f.className}`}
                  style={{ width: 110, fontWeight: 600 }}
                >
                  {f.className}
                </label>
                <input
                  id={`fee-${f.className}`}
                  className="input"
                  type="number"
                  min={0}
                  step="0.01"
                  value={values[f.className] ?? ""}
                  disabled={!canEdit}
                  onChange={(e) =>
                    setValues((v) => ({ ...v, [f.className]: e.target.value }))
                  }
                />
                {canEdit && (
                  <button
                    type="button"
                    className="btn btn--primary"
                    disabled={!dirty || saving === f.className}
                    onClick={() => save(f.className)}
                  >
                    {saving === f.className ? "Saving…" : "Save"}
                  </button>
                )}
                {rowErrors[f.className] && (
                  <span role="alert" className="field__error">
                    {rowErrors[f.className]}
                  </span>
                )}
              </div>
            );
          })}
        </div>
      </Panel>
    </>
  );
}
