import { ShieldCheck } from "lucide-react";
import { roleService } from "../services/roleService";
import type { Role } from "../types/role.types";
import { Dialog } from "@/components/Dialog";
import { DialogFooter } from "@/components/DialogFooter";
import { Notice } from "@/components/Notice";
import { useDialogSubmit } from "@/hooks/useDialogSubmit";

/**
 * E3c: activate or deactivate a role (roles are never deleted). The API's reasons for refusing
 * (your own role, the Admin role, a permission you do not hold, someone else saved first) stay
 * inside the pop-up.
 */
export function RoleStatusDialog({
  role,
  onClose,
  onChanged,
}: {
  role: Role;
  onClose: () => void;
  onChanged: (role: Role) => void;
}) {
  const activate = !role.isActive;
  const { isSubmitting, errors, submit } = useDialogSubmit(onClose);

  return (
    <Dialog
      title={activate ? "Activate role" : "Deactivate role"}
      icon={ShieldCheck}
      subtitle={role.name}
      errors={errors}
      closable={!isSubmitting}
      onClose={onClose}
      footer={
        <DialogFooter
          onCancel={onClose}
          onSubmit={() =>
            void submit(async () => {
              onChanged(await roleService.changeStatus(role.id, activate, role.rowVersion));
            }, "Could not change the role's status.")
          }
          label={activate ? "Activate" : "Deactivate"}
          busyLabel="Saving…"
          isSubmitting={isSubmitting}
          danger={!activate}
        />
      }
    >
      <p style={{ marginTop: 0 }}>
        {activate
          ? `Make ${role.name} available again? It can then be given to users.`
          : `Deactivate ${role.name}? It can no longer be given to users. People who already hold it keep it until their role is changed.`}
      </p>
      <Notice tone="warn">
        This changes what everyone holding this role can do. It applies at their next request, with no
        sign-out needed.
      </Notice>
    </Dialog>
  );
}
