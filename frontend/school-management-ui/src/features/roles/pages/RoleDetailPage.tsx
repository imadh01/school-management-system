import { useState } from "react";
import { useParams } from "react-router-dom";
import { ShieldAlert, ShieldCheck, ShieldOff } from "lucide-react";
import { roleService } from "../services/roleService";
import { RoleStatusDialog } from "../components/RoleStatusDialog";
import { PermissionSummary } from "../components/PermissionSummary";
import { DetailHeader } from "@/components/detail/DetailHeader";
import { DetailCard } from "@/components/detail/DetailCard";
import { KeyValueGrid } from "@/components/detail/KeyValueGrid";
import { EditButton } from "@/components/detail/HeaderButtons";
import { Badge } from "@/components/Badge";
import { Notice } from "@/components/Notice";
import { PageLoadError, PageLoading } from "@/components/PageState";
import { useToast } from "@/components/Toast";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCan } from "@/hooks/useCan";
import { Permission } from "@/routes/permissions";
import { useAuth } from "@/store/AuthContext";

/**
 * E3c: one role (/roles-permissions/:id): its details and the permissions it grants, by module.
 * Edit and Activate/Deactivate are hidden for the Admin role (it always has everything and stays
 * active) and for a role you hold yourself (the API refuses both).
 */
export function RoleDetailPage() {
  const roleId = Number(useParams().id);
  const toast = useToast();
  const canEdit = useCan()(Permission.RolesEdit);
  const { user: me } = useAuth();

  const loaded = useAsyncData(() => roleService.getById(roleId), [roleId], "Could not load this role.");
  const catalog = useAsyncData(() => roleService.getCatalog(), [], "Could not load the permission list.");
  const [changingStatus, setChangingStatus] = useState(false);

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
  const role = loaded.data;
  if (!role) return <PageLoading />;

  const isMine = me?.roles.includes(role.name) ?? false;

  return (
    <div>
      <DetailHeader
        back={{ to: "/roles-permissions", label: "All roles" }}
        title={role.name}
        meta={role.description ? [role.description] : []}
        badges={
          <>
            <Badge tone={role.isActive ? "ok" : "neutral"}>{role.isActive ? "Active" : "Inactive"}</Badge>
            {role.isSystem && <Badge tone="info">System role</Badge>}
            {isMine && <Badge tone="purple">Your role</Badge>}
          </>
        }
        actions={
          canEdit && !isMine ? (
            <>
              {!role.hasAllPermissions && <EditButton primary to={`/roles-permissions/${role.id}/edit`} />}
              {!role.hasAllPermissions && (
                <button type="button" className="btn btn--secondary" onClick={() => setChangingStatus(true)}>
                  {role.isActive ? <ShieldOff size={16} aria-hidden="true" /> : <ShieldCheck size={16} aria-hidden="true" />}
                  {role.isActive ? "Deactivate" : "Activate"}
                </button>
              )}
            </>
          ) : undefined
        }
      />

      {isMine && (
        <Notice tone="info" icon={ShieldAlert} style={{ marginBottom: 16 }}>
          You hold this role, so it can only be changed by another administrator. That way nobody can raise
          their own access or lock themselves out.
        </Notice>
      )}

      <div className="detail-cards">
        <DetailCard title="Role">
          <KeyValueGrid
            items={[
              { label: "Name", value: role.name },
              { label: "Description", value: role.description },
              { label: "Type", value: role.isSystem ? "System role (cannot be renamed)" : "Custom role" },
              { label: "Status", value: role.isActive ? "Active" : "Inactive" },
              { label: "Permissions", value: role.hasAllPermissions ? "All" : role.permissions.length },
            ]}
          />
        </DetailCard>

        <DetailCard title="Permissions" wide>
          {role.hasAllPermissions ? (
            <p style={{ margin: 0 }}>
              The Admin role always holds every permission, including any added later. Its permissions
              cannot be edited.
            </p>
          ) : catalog.data ? (
            <PermissionSummary catalog={catalog.data} granted={role.permissions} />
          ) : (
            <p className="cell-muted" style={{ margin: 0 }}>
              {catalog.error ?? "Loading permissions…"}
            </p>
          )}
        </DetailCard>
      </div>

      {changingStatus && (
        <RoleStatusDialog
          role={role}
          onClose={() => setChangingStatus(false)}
          onChanged={(updated) => {
            toast.success(`${updated.name} is now ${updated.isActive ? "active" : "inactive"}.`);
            loaded.setData(updated);
          }}
        />
      )}
    </div>
  );
}
