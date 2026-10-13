/*
 * F1: which permission opens which page. Used by the sidebar (hide what you
 * cannot open) and by RequirePermission (guard the URL itself).
 *
 * This is cosmetic: the API enforces every permission on its own. A page lists
 * "any one of" its permissions, so a teacher holding only Students.ViewScoped
 * still sees Students (filtered to their classes by the API).
 *
 * Names must match backend Domain/Constants/Permissions.cs.
 */

export const Permission = {
  UsersView: "Users.View",
  SettingsView: "Settings.View",

  ClassSectionsView: "ClassSections.View",
  ClassSectionsCreate: "ClassSections.Create",
  ClassSectionsEdit: "ClassSections.Edit",
  ClassSectionsDelete: "ClassSections.Delete",

  SubjectsView: "Subjects.View",
  SubjectsCreate: "Subjects.Create",
  SubjectsEdit: "Subjects.Edit",
  SubjectsDelete: "Subjects.Delete",

  TeachersView: "Teachers.View",
  TeachersCreate: "Teachers.Create",
  TeachersEdit: "Teachers.Edit",
  TeachersDelete: "Teachers.Delete",
  TeachersAssign: "Teachers.Assign",

  AdmissionsView: "Admissions.View",
  AdmissionsCreate: "Admissions.Create",
  AdmissionsEdit: "Admissions.Edit",
  AdmissionsEnroll: "Admissions.Enroll",
  AdmissionsDelete: "Admissions.Delete",

  StudentsView: "Students.View",
  StudentsViewScoped: "Students.ViewScoped",
  StudentsEdit: "Students.Edit",
  StudentsDelete: "Students.Delete",

  ParentsView: "Parents.View",
  ParentsViewScoped: "Parents.ViewScoped",
  ParentsCreate: "Parents.Create",
  ParentsEdit: "Parents.Edit",
  ParentsDelete: "Parents.Delete",

  AttendanceView: "Attendance.View",
  AttendanceViewScoped: "Attendance.ViewScoped",
} as const;

/**
 * U1: modules from the prototype that are not built yet ("Soon" in the sidebar) have no
 * permissions of their own. Until they do, only administrators (Users.View) see them, as a
 * roadmap; everyone else sees only pages that work. Each module gets real permissions when
 * it is built, and this rule stops applying to it.
 */
export const UNBUILT_MODULES_PERMISSIONS: readonly string[] = [
  Permission.UsersView,
];

/** Path → any one of these permissions opens it. Paths not listed are open to every signed-in user. */
export const PAGE_PERMISSIONS: Readonly<Record<string, readonly string[]>> = {
  "/admissions": [Permission.AdmissionsView],
  "/students": [Permission.StudentsView, Permission.StudentsViewScoped],
  "/parents": [Permission.ParentsView, Permission.ParentsViewScoped],
  "/classes-sections": [Permission.ClassSectionsView],
  "/subjects": [Permission.SubjectsView],
  "/teachers": [Permission.TeachersView],
  "/attendance": [Permission.AttendanceView, Permission.AttendanceViewScoped],
  "/users-staff": [Permission.UsersView],
  "/settings": [Permission.SettingsView],
};
