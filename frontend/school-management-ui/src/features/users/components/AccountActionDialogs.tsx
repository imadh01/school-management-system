import { useState } from "react";
import { KeyRound, ShieldAlert, Trash, UserCheck } from "lucide-react";
import { userService } from "../services/userService";
import { USER_STATUSES, type UserStatus } from "../types/user.types";
import { Dialog } from "@/components/Dialog";
import { DialogFooter } from "@/components/DialogFooter";
import { Notice } from "@/components/Notice";
import { TextField } from "@/components/form/Inputs";
import { PasswordRules } from "@/components/form/PasswordRules";
import { useDialogSubmit } from "@/hooks/useDialogSubmit";
import { meetsPasswordRules } from "@/utils/password";

/**
 * E3a: the account-security actions of the Users page. Each one is a pop-up that stays open when
 * the API refuses (your own account, the last active Admin, a teacher who still teaches, someone
 * else saved first), with the API's reason shown at the top of the pop-up, so the refusal cannot be
 * missed the way a toast in the corner can.
 */

/** The one sentence every one of these pop-ups says (approved wording). */
export const SIGNS_OUT_EVERYWHERE =
  "This signs the user out everywhere: every device they are signed in on is signed out at once.";

/** Who the action is about. */
export interface AccountTarget {
  id: number;
  name: string;
  status: string;
  rowVersion: string;
}

const STATUS_HELP: Record<UserStatus, string> = {
  Active: "Can sign in.",
  Inactive: "Cannot sign in (e.g. left the school).",
  Suspended: "Cannot sign in until made active again.",
};

export function ChangeStatusDialog({
  user,
  onClose,
  onChanged,
}: {
  user: AccountTarget;
  onClose: () => void;
  /** After the save; the caller reloads and shows a toast. */
  onChanged: (status: UserStatus) => void;
}) {
  const options = USER_STATUSES.filter((s) => s !== user.status);
  const [status, setStatus] = useState<UserStatus>(options[0]);
  const { isSubmitting, errors, submit } = useDialogSubmit(onClose);

  return (
    <Dialog
      title="Change status"
      icon={UserCheck}
      subtitle={`${user.name} · now ${user.status}`}
      errors={errors}
      closable={!isSubmitting}
      onClose={onClose}
      footer={
        <DialogFooter
          onCancel={onClose}
          onSubmit={() =>
            void submit(async () => {
              await userService.changeStatus(user.id, status, user.rowVersion);
              onChanged(status);
            }, "Could not change the status.")
          }
          label={`Make ${status.toLowerCase()}`}
          busyLabel="Saving…"
          isSubmitting={isSubmitting}
          danger={status !== "Active"}
        />
      }
    >
      <fieldset className="field field--plain">
        <legend>New status</legend>
        {options.map((s) => (
          <label key={s} className="checkbox-row">
            <input
              type="radio"
              name="user-new-status"
              checked={status === s}
              onChange={() => setStatus(s)}
            />
            <span>
              <strong>{s}</strong> — {STATUS_HELP[s]}
            </span>
          </label>
        ))}
      </fieldset>
      <Notice tone="warn" style={{ marginTop: 12 }}>
        {SIGNS_OUT_EVERYWHERE}
      </Notice>
    </Dialog>
  );
}

export function ResetPasswordDialog({
  user,
  onClose,
  onReset,
}: {
  user: AccountTarget;
  onClose: () => void;
  onReset: () => void;
}) {
  // Lives only in this pop-up: it is never shown again, stored, or put in a message.
  const [password, setPassword] = useState("");
  const [problem, setProblem] = useState<string | undefined>();
  const { isSubmitting, errors, submit } = useDialogSubmit(onClose);

  const save = () => {
    if (!meetsPasswordRules(password)) {
      setProblem("The password does not meet all the rules below.");
      return;
    }
    setProblem(undefined);
    void submit(async () => {
      await userService.resetPassword(user.id, password);
      onReset();
    }, "Could not set the temporary password.");
  };

  return (
    <Dialog
      title="Reset password"
      icon={KeyRound}
      subtitle={user.name}
      errors={errors}
      closable={!isSubmitting}
      onClose={onClose}
      footer={
        <DialogFooter
          onCancel={onClose}
          onSubmit={save}
          label="Set temporary password"
          busyLabel="Saving…"
          isSubmitting={isSubmitting}
          danger
        />
      }
    >
      <p style={{ marginTop: 0 }}>
        Type a temporary password and give it to the user in person. They must
        choose their own password the next time they sign in.
      </p>
      <TextField
        id="user-reset-password"
        label="Temporary password"
        type="password"
        required
        autoComplete="new-password"
        value={password}
        onChange={setPassword}
        error={problem}
      />
      <PasswordRules password={password} />
      <Notice tone="warn" style={{ marginTop: 12 }}>
        {SIGNS_OUT_EVERYWHERE}
      </Notice>
    </Dialog>
  );
}

export function DeleteUserDialog({
  user,
  isTeacher,
  onClose,
  onDeleted,
}: {
  user: AccountTarget;
  isTeacher: boolean;
  onClose: () => void;
  onDeleted: () => void;
}) {
  const { isSubmitting, errors, submit } = useDialogSubmit(onClose);

  return (
    <Dialog
      title="Delete user"
      icon={Trash}
      subtitle={user.name}
      errors={errors}
      closable={!isSubmitting}
      onClose={onClose}
      footer={
        <DialogFooter
          onCancel={onClose}
          onSubmit={() =>
            void submit(async () => {
              await userService.delete(user.id);
              onDeleted();
            }, "Could not delete this user.")
          }
          label="Delete"
          busyLabel="Deleting…"
          isSubmitting={isSubmitting}
          danger
        />
      }
    >
      <p style={{ marginTop: 0 }}>
        Delete {user.name}? The login and the staff profile are removed
        {isTeacher
          ? ", and so is the teacher record (only when they no longer teach any subject or class)"
          : ""}
        . This cannot be undone from this page.
      </p>
      <Notice tone="warn">{SIGNS_OUT_EVERYWHERE}</Notice>
    </Dialog>
  );
}

/** Shown on your own account instead of the actions the API refuses for yourself. */
export function OwnAccountNotice() {
  return (
    <Notice tone="info" icon={ShieldAlert} style={{ marginBottom: 16 }}>
      This is your own account. Its status, role and password can only be
      changed by another administrator, so the school is never locked out by
      mistake.
    </Notice>
  );
}
