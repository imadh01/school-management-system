import { useEffect, useRef } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { roleService } from "../services/roleService";
import { PermissionPicker } from "../components/PermissionPicker";
import type { Role } from "../types/role.types";
import { FormPage } from "@/components/form/FormPage";
import { FormSection } from "@/components/form/FormSection";
import { TextAreaField, TextField } from "@/components/form/Inputs";
import { Notice } from "@/components/Notice";
import { PageLoadError, PageLoading } from "@/components/PageState";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCan } from "@/hooks/useCan";
import { useRecordForm } from "@/hooks/useRecordForm";
import { blank } from "@/features/users/utils/profileForm";
import { useAuth } from "@/store/AuthContext";
import { fieldBinder, type FormSectionDef } from "@/utils/form";

interface FormState {
  name: string;
  description: string;
  permissions: string[];
}

const SECTIONS: readonly FormSectionDef[] = [
  { id: "details", title: "Role", fields: ["name", "description"] },
  { id: "permissions", title: "Permissions", fields: ["permissions"] },
];

const EMPTY_FORM: FormState = { name: "", description: "", permissions: [] };

const formFrom = (r: Role): FormState => ({
  name: r.name,
  description: r.description ?? "",
  permissions: [...r.permissions].sort(),
});

/**
 * E3c: add a role (/roles-permissions/new) and edit one (/roles-permissions/:id/edit).
 * Saving an existing role may need two requests (name/description, then the permission list); the
 * second one uses the row version the first returned, so a half-finished save can simply be retried.
 * The API stays the authority on every rule (own role, system roles, no escalation, dependencies).
 */
export function RoleFormPage() {
  const { id } = useParams();
  const isNew = id === undefined;
  const roleId = Number(id);
  const navigate = useNavigate();
  const can = useCan();
  const { user: me } = useAuth();

  const loaded = useAsyncData(
    () => (isNew ? Promise.resolve(null) : roleService.getById(roleId)),
    [isNew, roleId],
    "Could not load this role.",
  );
  const catalog = useAsyncData(() => roleService.getCatalog(), [], "Could not load the permission list.");
  const record = useRecordForm<FormState>({
    sections: SECTIONS,
    initial: EMPTY_FORM,
    noun: "role",
    onReload: () => void loaded.reload(),
  });
  const { reset } = record;

  const role = loaded.data ?? null;
  // The newest row version we know: starts as the loaded one, moves on after each request.
  const rowVersion = useRef("");
  useEffect(() => {
    if (role) {
      reset(formFrom(role));
      rowVersion.current = role.rowVersion;
    }
  }, [role, reset]);

  if (loaded.error) {
    return (
      <PageLoadError
        title="This role could not be opened"
        message={loaded.error}
        backTo="/roles-permissions"
        backLabel="Back to roles"
      />
    );
  }
  if (loaded.isLoading || (role && record.form === EMPTY_FORM)) return <PageLoading />;

  const form = record.form;
  const bind = fieldBinder(form, record.patch, record.fieldErrors, "role");
  const backTo = role ? `/roles-permissions/${role.id}` : "/roles-permissions";
  const isMine = !!role && (me?.roles.includes(role.name) ?? false);
  const original = role ? formFrom(role) : EMPTY_FORM;
  // Admin holds every permission in code and your own role cannot be changed: shown, not editable.
  const permissionsLocked = !!role && (role.hasAllPermissions || isMine);
  const shownPermissions =
    role?.hasAllPermissions && catalog.data
      ? catalog.data.flatMap((m) => m.permissions.map((p) => p.name))
      : form.permissions;
  const origSet = new Set(original.permissions);
  const nowSet = new Set(form.permissions);
  const changeCount =
    (form.name.trim() !== original.name ? 1 : 0) +
    (form.description.trim() !== original.description.trim() ? 1 : 0) +
    form.permissions.filter((p) => !origSet.has(p)).length +
    original.permissions.filter((p) => !nowSet.has(p)).length;

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.name.trim()) e.name = "Role name is required.";
    return e;
  };

  const save = () =>
    void record.save({
      validate,
      submit: async (): Promise<Role> => {
        const name = form.name.trim().replace(/\s+/g, " ");
        if (!role) {
          return roleService.create({
            name,
            description: blank(form.description),
            permissions: form.permissions,
          });
        }
        let latest: Role | null = null;
        if (name !== original.name || form.description.trim() !== original.description.trim()) {
          latest = await roleService.update(role.id, {
            name,
            description: blank(form.description),
            rowVersion: rowVersion.current,
          });
          rowVersion.current = latest.rowVersion;
        }
        if (JSON.stringify([...form.permissions].sort()) !== JSON.stringify(original.permissions)) {
          latest = await roleService.setPermissions(role.id, form.permissions, rowVersion.current);
          rowVersion.current = latest.rowVersion;
        }
        return latest ?? roleService.getById(role.id);
      },
      fallback: "Could not save this role.",
      onSaved: (saved) => ({
        form: formFrom(saved),
        message: role ? `${saved.name} was saved.` : `${saved.name} was added.`,
        goTo: `/roles-permissions/${saved.id}`,
      }),
    });

  return (
    <>
      {record.guard}
      <FormPage
        {...record.pageProps}
        changeCount={changeCount}
        title={role ? `Edit ${role.name}` : "Add role"}
        subtitle={
          role
            ? "Changes apply to everyone holding this role at their next request."
            : "A custom role: pick what it may do, then give it to users on the Users page."
        }
        back={{ to: backTo, label: role ? "Back to role" : "Back to roles" }}
        onSubmit={save}
        saveLabel={role ? "Save changes" : "Save role"}
        onCancel={() => navigate(backTo)}
      >
        {isMine && (
          <Notice tone="warn" style={{ marginBottom: 16 }}>
            You hold this role, so the API will refuse changes to it. Ask another administrator.
          </Notice>
        )}
        <FormSection id="details" title="Role">
          <TextField
            {...bind("name")}
            label="Name"
            required
            maxLength={50}
            disabled={role?.isSystem}
            hint={role?.isSystem ? "A system role cannot be renamed." : undefined}
          />
          <TextAreaField {...bind("description")} label="Description" rows={2} span="full" />
        </FormSection>

        <FormSection
          id="permissions"
          title="Permissions"
          description="You can only hand out permissions you hold yourself."
          grid={false}
        >
          {catalog.data ? (
            <PermissionPicker
              idPrefix="role"
              catalog={catalog.data}
              selected={shownPermissions}
              onChange={(permissions) => record.patch({ permissions })}
              canGrant={(p) => can(p)}
              readOnly={permissionsLocked}
            />
          ) : (
            <p className="cell-muted">{catalog.error ?? "Loading the permission list…"}</p>
          )}
          {record.fieldErrors.permissions && (
            <p className="field__error">{record.fieldErrors.permissions}</p>
          )}
        </FormSection>
      </FormPage>
    </>
  );
}
