import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { KeyRound, LockOpen, UserCheck } from "lucide-react";
import { userService } from "../services/userService";
import type { UserResponse } from "../types/user.types";
import {
  ChangeStatusDialog,
  DeleteUserDialog,
  OwnAccountNotice,
  ResetPasswordDialog,
  type AccountTarget,
} from "../components/AccountActionDialogs";
import { DetailHeader } from "@/components/detail/DetailHeader";
import { DetailCard } from "@/components/detail/DetailCard";
import { AddressList } from "@/components/detail/AddressList";
import { DeleteButton, EditButton } from "@/components/detail/HeaderButtons";
import { KeyValueGrid } from "@/components/detail/KeyValueGrid";
import { Badge, StatusBadge } from "@/components/Badge";
import { Notice } from "@/components/Notice";
import { PageLoadError, PageLoading } from "@/components/PageState";
import { useToast } from "@/components/Toast";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCan } from "@/hooks/useCan";
import { useConfirmAction } from "@/hooks/useConfirmAction";
import { PAGE_PERMISSIONS, Permission } from "@/routes/permissions";
import { useAuth } from "@/store/AuthContext";
import { formatDate, formatUtcDateTime } from "@/utils/format";

type Action = "status" | "reset" | "delete";

/**
 * E3a: one staff account (/users-staff/:id): login, profile, addresses, and the account actions.
 * Status, password and delete sign the user out everywhere; each is a pop-up that keeps the API's
 * reason on screen when it refuses (last active Admin, own account, teacher who still teaches).
 */
export function UserDetailPage() {
  const userId = Number(useParams().id);
  const navigate = useNavigate();
  const toast = useToast();
  const can = useCan();
  const { user: me } = useAuth();
  const isMe = userId === me?.userId;
  const canEdit = can(Permission.UsersEdit);
  const canDelete = can(Permission.UsersDelete) && !isMe;
  const canStatus = can(Permission.UsersChangeStatus);
  const canReset = can(Permission.UsersResetPassword) && !isMe;
  const canOpenTeachers = can(...PAGE_PERMISSIONS["/teachers"]);

  const loaded = useAsyncData(
    () => userService.getById(userId),
    [userId],
    "Could not load this user.",
  );
  const [action, setAction] = useState<Action | null>(null);

  const unlock = useConfirmAction<UserResponse>({
    title: "Unlock account",
    message: (u) =>
      `Let ${u.profile?.fullName ?? u.username} sign in again now? The account was locked after too many wrong passwords.`,
    confirmLabel: "Unlock",
    danger: false,
    run: async (u) => {
      await userService.unlock(u.id);
      await loaded.reload();
    },
    success: (u) => `${u.profile?.fullName ?? u.username} can sign in again.`,
    failure: "Could not unlock this account.",
  });

  if (loaded.error) {
    return (
      <PageLoadError
        title="This user could not be opened"
        message={loaded.error}
        backTo="/users-staff"
        backLabel="Back to users"
      />
    );
  }
  const user = loaded.data;
  if (!user) return <PageLoading />;

  const p = user.profile;
  const name = p?.fullName ?? user.username;
  const role = user.roles[0] ?? null;
  const target: AccountTarget = {
    id: user.id,
    name,
    status: user.status,
    rowVersion: user.rowVersion,
  };
  const reloadWith = (message: string) => {
    toast.success(message);
    void loaded.reload();
  };

  return (
    <div>
      <DetailHeader
        back={{ to: "/users-staff", label: "All users" }}
        title={name}
        meta={[
          `@${user.username}`,
          user.email,
          ...(p?.mobile ? [p.mobile] : []),
        ]}
        badges={
          <>
            <StatusBadge status={user.status} />
            {role && <Badge tone="purple">{role}</Badge>}
            {user.isLocked && <Badge tone="bad">Locked</Badge>}
            {user.mustChangePassword && (
              <Badge tone="info">Must change password</Badge>
            )}
            {p?.needsName && <Badge tone="warn">Needs name</Badge>}
          </>
        }
        actions={
          <>
            {canEdit && (
              <EditButton primary to={`/users-staff/${user.id}/edit`} />
            )}
            {canStatus && !isMe && (
              <button
                type="button"
                className="btn btn--secondary"
                onClick={() => setAction("status")}
              >
                <UserCheck size={16} aria-hidden="true" />
                Change status
              </button>
            )}
            {canReset && (
              <button
                type="button"
                className="btn btn--secondary"
                onClick={() => setAction("reset")}
              >
                <KeyRound size={16} aria-hidden="true" />
                Reset password
              </button>
            )}
            {canStatus && user.isLocked && (
              <button
                type="button"
                className="btn btn--secondary"
                onClick={() => unlock.ask(user)}
              >
                <LockOpen size={16} aria-hidden="true" />
                Unlock
              </button>
            )}
            {canDelete && <DeleteButton onClick={() => setAction("delete")} />}
          </>
        }
      />

      {isMe && <OwnAccountNotice />}
      {p?.needsName && (
        <Notice tone="warn" style={{ marginBottom: 16 }}>
          The name is still the username, copied when staff profiles were
          introduced. {canEdit ? "Edit the user to enter the real name." : ""}
        </Notice>
      )}

      <div className="detail-cards">
        <DetailCard title="Login account">
          <KeyValueGrid
            items={[
              { label: "Username", value: user.username },
              {
                label: "Email",
                value: <a href={`mailto:${user.email}`}>{user.email}</a>,
              },
              { label: "Role", value: role },
              { label: "Status", value: user.status },
              {
                label: "Last login",
                value: formatUtcDateTime(user.lastLoginAt) ?? "Never",
              },
              { label: "Account created", value: formatUtcDateTime(user.createdAt) },
              {
                label: "Teacher record",
                value:
                  user.teacherId === null ? null : canOpenTeachers ? (
                    <Link to={`/teachers/${user.teacherId}`}>
                      Open on the Teachers page
                    </Link>
                  ) : (
                    "Yes"
                  ),
              },
            ]}
          />
        </DetailCard>

        <DetailCard title="Profile">
          {p ? (
            <KeyValueGrid
              items={[
                { label: "Full name", value: p.fullName },
                { label: "Gender", value: p.gender },
                { label: "Date of birth", value: formatDate(p.dateOfBirth) },
                {
                  label: "Mobile",
                  value: p.mobile && <a href={`tel:${p.mobile}`}>{p.mobile}</a>,
                },
                { label: "Joining date", value: formatDate(p.joiningDate) },
                { label: "Employee no.", value: p.employeeNumber },
                { label: "Qualification", value: p.qualification },
                { label: "Blood group", value: p.bloodGroup },
                {
                  label: "Emergency contact",
                  value: [p.emergencyContactName, p.emergencyContactPhone]
                    .filter(Boolean)
                    .join(" · "),
                },
              ]}
            />
          ) : (
            <p className="cell-muted" style={{ margin: 0 }}>
              This account has no staff profile.
            </p>
          )}
        </DetailCard>

        <DetailCard title="Addresses">
          <AddressList addresses={user.addresses} />
        </DetailCard>
      </div>

      {action === "status" && (
        <ChangeStatusDialog
          user={target}
          onClose={() => setAction(null)}
          onChanged={(status) =>
            reloadWith(`${name} is now ${status.toLowerCase()} and was signed out everywhere.`)
          }
        />
      )}
      {action === "reset" && (
        <ResetPasswordDialog
          user={target}
          onClose={() => setAction(null)}
          onReset={() =>
            reloadWith(
              `A temporary password was set for ${name}. They were signed out everywhere and must choose their own password at the next sign-in.`,
            )
          }
        />
      )}
      {action === "delete" && (
        <DeleteUserDialog
          user={target}
          isTeacher={user.teacherId !== null}
          onClose={() => setAction(null)}
          onDeleted={() => {
            toast.success(`${name} was deleted.`);
            navigate("/users-staff");
          }}
        />
      )}
      {unlock.dialog}
    </div>
  );
}
