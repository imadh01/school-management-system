import { useEffect, useMemo, useState } from "react";
import {
  CircleCheck,
  CircleMinus,
  Download,
  Eye,
  GraduationCap,
  HeartPulse,
  History,
  LogOut,
  Pencil,
  Printer,
  Search,
  Trash,
  Users,
} from "lucide-react";
import { studentService } from "../services/studentService";
import { classSectionService } from "@/features/class-sections/services/classSectionService";
import type {
  StudentResponse,
  StudentSummaryResponse,
  StudentStatus,
  CreateStudentRequest,
  UpdateStudentIdentityRequest,
} from "../types/student.types";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";
import { StudentHistoryModal } from "../components/StudentHistoryModal";
import { StudentModal } from "../components/StudentModal";
import { LinkedParentsViewModal } from "../components/LinkedParentsViewModal";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { ViewDetailsModal } from "@/components/ViewDetailsModal";
import { ValidationModal } from "@/components/ValidationModal";
import { PageHero } from "@/components/PageHero";
import { StatCard, StatGrid } from "@/components/StatCard";
import { Panel } from "@/components/Panel";
import { FilterBar } from "@/components/FilterBar";
import { DataTable, type Column } from "@/components/DataTable";
import { StatusBadge } from "@/components/Badge";
import { IconButton } from "@/components/IconButton";
import { EmptyState } from "@/components/EmptyState";
import { useToast } from "@/components/Toast";
import {
  getApiErrors,
  getReadErrors,
  isConcurrencyConflict,
} from "@/utils/apiError";
import { useAuth } from "@/store/AuthContext";
import { Permission } from "@/routes/permissions";
import { initialsOf } from "@/utils/initials";

function unique(values: (string | null | undefined)[]): string[] {
  return [...new Set(values.filter((v): v is string => !!v))].sort();
}

function fullName(s: { firstName: string; lastName: string }) {
  return `${s.firstName} ${s.lastName}`;
}

function toCsv(rows: StudentSummaryResponse[]): string {
  const headers = [
    "Adm #",
    "Student",
    "Class",
    "Gender",
    "House",
    "Nationality",
    "Curriculum",
    "EAL",
    "Status",
    "Allergies",
  ];
  const lines = rows.map((s) =>
    [
      s.admNo,
      fullName(s),
      s.classSectionName,
      s.gender,
      s.house ?? "",
      s.nationality ?? "",
      s.curriculumTrack ?? "",
      s.ealCode ?? "",
      s.status,
      // The text only for Students.ViewSensitive holders; everyone else exports the flag.
      s.allergies ?? (s.hasAllergies ? "Yes" : ""),
    ]
      .map((v) => `"${String(v).replace(/"/g, '""')}"`)
      .join(","),
  );
  return [headers.join(","), ...lines].join("\n");
}

/** The allergy flag (U1): an icon for everyone; the text, as a tooltip, only for ViewSensitive holders. */
function AllergyFlag({ student }: { student: StudentSummaryResponse }) {
  if (!student.hasAllergies) {
    return (
      <span className="cell-muted">
        <span aria-hidden="true">—</span>
        <span className="visually-hidden">No allergies recorded</span>
      </span>
    );
  }
  const label = student.allergies
    ? `Allergies: ${student.allergies}`
    : "Has allergies";
  return (
    <span className="flag" title={label}>
      <HeartPulse size={16} aria-hidden="true" />
      <span className="visually-hidden">{label}</span>
    </span>
  );
}

export function StudentsPage() {
  // F1: hide what this role cannot do (the API refuses it anyway). A teacher
  // holding only Students.ViewScoped sees their own students, read-only.
  const { hasAnyPermission } = useAuth();
  const canEdit = hasAnyPermission([Permission.StudentsEdit]);
  const canDelete = hasAnyPermission([Permission.StudentsDelete]);
  const toast = useToast();

  const [students, setStudents] = useState<StudentSummaryResponse[]>([]);
  const [classSections, setClassSections] = useState<ClassSectionResponse[]>(
    [],
  );
  const [isLoading, setIsLoading] = useState(true);

  const [statusFilter, setStatusFilter] = useState<StudentStatus | "">("");
  const [search, setSearch] = useState("");
  const [classFilter, setClassFilter] = useState("");
  const [genderFilter, setGenderFilter] = useState("");
  const [curriculumFilter, setCurriculumFilter] = useState("");
  const [ealFilter, setEalFilter] = useState("");
  const [houseFilter, setHouseFilter] = useState("");
  const [allergiesOnly, setAllergiesOnly] = useState(false);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingStudent, setEditingStudent] = useState<StudentResponse | null>(
    null,
  );
  const [linkedParentsTarget, setLinkedParentsTarget] =
    useState<StudentSummaryResponse | null>(null);
  const [historyTarget, setHistoryTarget] =
    useState<StudentSummaryResponse | null>(null);
  const [deleteTarget, setDeleteTarget] =
    useState<StudentSummaryResponse | null>(null);
  const [detailsTarget, setDetailsTarget] = useState<StudentResponse | null>(
    null,
  );
  const [errors, setErrors] = useState<string[] | null>(null);

  const loadData = async () => {
    setIsLoading(true);
    try {
      const [studentsData, classSectionsData] = await Promise.all([
        studentService.getAll(),
        classSectionService.getAll(),
      ]);
      setStudents(studentsData);
      setClassSections(classSectionsData);
    } catch (err) {
      setErrors(getReadErrors(err, "Could not load students."));
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    void loadData();
  }, []);

  const classNames = useMemo(
    () => unique(students.map((s) => s.classSectionName)),
    [students],
  );
  const curricula = useMemo(
    () => unique(students.map((s) => s.curriculumTrack)),
    [students],
  );
  const ealLevels = useMemo(
    () => unique(students.map((s) => s.ealCode)),
    [students],
  );
  const houses = useMemo(
    () => unique(students.map((s) => s.house)),
    [students],
  );

  const counts = useMemo(() => {
    const c = { Active: 0, Inactive: 0, Left: 0 };
    students.forEach((s) => {
      if (s.status in c) c[s.status as keyof typeof c] += 1;
    });
    return c;
  }, [students]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return students.filter((s) => {
      if (statusFilter && s.status !== statusFilter) return false;
      if (classFilter && s.classSectionName !== classFilter) return false;
      if (genderFilter && s.gender !== genderFilter) return false;
      if (curriculumFilter && s.curriculumTrack !== curriculumFilter)
        return false;
      if (ealFilter && s.ealCode !== ealFilter) return false;
      if (houseFilter && s.house !== houseFilter) return false;
      if (allergiesOnly && !s.hasAllergies) return false;
      if (q) {
        const haystack =
          `${s.firstName} ${s.lastName} ${s.admNo} ${s.rollNumber} ${s.classSectionName}`.toLowerCase();
        if (!haystack.includes(q)) return false;
      }
      return true;
    });
  }, [
    students,
    statusFilter,
    search,
    classFilter,
    genderFilter,
    curriculumFilter,
    ealFilter,
    houseFilter,
    allergiesOnly,
  ]);

  const filterKey = [
    statusFilter,
    search,
    classFilter,
    genderFilter,
    curriculumFilter,
    ealFilter,
    houseFilter,
    allergiesOnly,
  ].join("|");
  const anyFilter =
    !!(
      statusFilter ||
      search ||
      classFilter ||
      genderFilter ||
      curriculumFilter ||
      ealFilter ||
      houseFilter
    ) || allergiesOnly;

  const clearFilters = () => {
    setSearch("");
    setClassFilter("");
    setGenderFilter("");
    setCurriculumFilter("");
    setEalFilter("");
    setHouseFilter("");
    setAllergiesOnly(false);
    setStatusFilter("");
  };

  // The list carries summary fields only, so the full record is fetched when opened.
  const openEdit = async (s: StudentSummaryResponse) => {
    try {
      setEditingStudent(await studentService.getById(s.id));
      setIsModalOpen(true);
    } catch (err) {
      setErrors(getReadErrors(err, "Could not load this student."));
    }
  };

  const openDetails = async (s: StudentSummaryResponse) => {
    try {
      setDetailsTarget(await studentService.getById(s.id));
    } catch (err) {
      setErrors(getReadErrors(err, "Could not load this student."));
    }
  };

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    const target = deleteTarget;
    setDeleteTarget(null);
    try {
      await studentService.delete(target.id);
      toast.success(`${fullName(target)} was deleted.`);
      await loadData();
    } catch (err) {
      setErrors(getApiErrors(err, "Could not delete this student."));
    }
  };

  const handleExportCsv = () => {
    const blob = new Blob([toCsv(filtered)], {
      type: "text/csv;charset=utf-8;",
    });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = "students.csv";
    link.click();
    URL.revokeObjectURL(url);
  };

  const columns: Column<StudentSummaryResponse>[] = [
    {
      key: "admNo",
      header: "Adm #",
      sortValue: (s) => s.admNo,
      render: (s) => (
        <span
          className="cell-strong"
          title={
            s.admissionRegNo ? `From admission ${s.admissionRegNo}` : undefined
          }
        >
          {s.admNo}
        </span>
      ),
    },
    {
      key: "name",
      header: "Student",
      sortValue: (s) => fullName(s),
      render: (s) => (
        <div className="person">
          <span className="avatar" aria-hidden="true">
            {initialsOf(fullName(s))}
          </span>
          <div>
            <div className="person__name">{fullName(s)}</div>
            <div className="person__sub">Roll {s.rollNumber}</div>
          </div>
        </div>
      ),
    },
    {
      key: "class",
      header: "Class",
      sortValue: (s) => s.classSectionName,
      render: (s) => s.classSectionName,
    },
    {
      key: "gender",
      header: "Gender",
      hideBelow: "md",
      sortValue: (s) => s.gender,
      render: (s) => s.gender,
    },
    {
      key: "house",
      header: "House",
      hideBelow: "md",
      sortValue: (s) => s.house,
      render: (s) => s.house ?? <span className="cell-muted">—</span>,
    },
    {
      key: "allergies",
      header: "Allergies",
      render: (s) => <AllergyFlag student={s} />,
    },
    {
      key: "status",
      header: "Status",
      sortValue: (s) => s.status,
      render: (s) => <StatusBadge status={s.status} />,
    },
    {
      key: "actions",
      header: "Actions",
      render: (s) => (
        <div className="row-actions">
          <IconButton
            icon={Eye}
            label={`View ${fullName(s)}`}
            onClick={() => openDetails(s)}
          />
          <IconButton
            icon={Users}
            label={`Parents of ${fullName(s)}`}
            onClick={() => setLinkedParentsTarget(s)}
          />
          <IconButton
            icon={History}
            label={`Academic history of ${fullName(s)}`}
            onClick={() => setHistoryTarget(s)}
          />
          {canEdit && (
            <IconButton
              icon={Pencil}
              tone="info"
              label={`Edit ${fullName(s)}`}
              onClick={() => openEdit(s)}
            />
          )}
          {canDelete && (
            <IconButton
              icon={Trash}
              tone="bad"
              label={`Delete ${fullName(s)}`}
              onClick={() => setDeleteTarget(s)}
            />
          )}
        </div>
      ),
    },
  ];

  return (
    <div>
      <PageHero
        title="Students"
        subtitle="Enrolled students, their classes, guardians and academic history."
        actions={
          <button
            type="button"
            className="btn btn--secondary"
            onClick={() => window.print()}
          >
            <Printer size={16} aria-hidden="true" />
            Print
          </button>
        }
        stats={
          <StatGrid>
            <StatCard
              label="Total"
              value={students.length}
              icon={GraduationCap}
              tone="info"
            />
            <StatCard
              label="Active"
              value={counts.Active}
              icon={CircleCheck}
              tone="ok"
            />
            <StatCard
              label="Inactive"
              value={counts.Inactive}
              icon={CircleMinus}
              tone="neutral"
            />
            <StatCard
              label="Left"
              value={counts.Left}
              icon={LogOut}
              tone="purple"
            />
          </StatGrid>
        }
      />

      <Panel
        title="All students"
        subtitle={`${students.length} students · ${filtered.length} shown`}
        icon={GraduationCap}
        actions={
          <button
            type="button"
            className="btn btn--secondary"
            onClick={handleExportCsv}
            disabled={filtered.length === 0}
          >
            <Download size={16} aria-hidden="true" />
            Export CSV
          </button>
        }
      >
        <FilterBar>
          <label>
            <span className="visually-hidden">Class</span>
            <select
              className="select"
              value={classFilter}
              onChange={(e) => setClassFilter(e.target.value)}
            >
              <option value="">All classes</option>
              {classNames.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
          </label>
          <label>
            <span className="visually-hidden">Gender</span>
            <select
              className="select"
              value={genderFilter}
              onChange={(e) => setGenderFilter(e.target.value)}
            >
              <option value="">All genders</option>
              <option>Male</option>
              <option>Female</option>
            </select>
          </label>
          <label>
            <span className="visually-hidden">House</span>
            <select
              className="select"
              value={houseFilter}
              onChange={(e) => setHouseFilter(e.target.value)}
            >
              <option value="">All houses</option>
              {houses.map((h) => (
                <option key={h} value={h}>
                  {h}
                </option>
              ))}
            </select>
          </label>
          <label>
            <span className="visually-hidden">Status</span>
            <select
              className="select"
              value={statusFilter}
              onChange={(e) =>
                setStatusFilter(e.target.value as StudentStatus | "")
              }
            >
              <option value="">All statuses</option>
              <option>Active</option>
              <option>Inactive</option>
              <option>Left</option>
            </select>
          </label>
          {curricula.length > 0 && (
            <label>
              <span className="visually-hidden">Curriculum</span>
              <select
                className="select"
                value={curriculumFilter}
                onChange={(e) => setCurriculumFilter(e.target.value)}
              >
                <option value="">All curricula</option>
                {curricula.map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
              </select>
            </label>
          )}
          {ealLevels.length > 0 && (
            <label>
              <span className="visually-hidden">English level</span>
              <select
                className="select"
                value={ealFilter}
                onChange={(e) => setEalFilter(e.target.value)}
              >
                <option value="">All English levels</option>
                {ealLevels.map((l) => (
                  <option key={l} value={l}>
                    {l}
                  </option>
                ))}
              </select>
            </label>
          )}
          <label className="checkbox-row" style={{ marginTop: 0 }}>
            <input
              type="checkbox"
              checked={allergiesOnly}
              onChange={(e) => setAllergiesOnly(e.target.checked)}
            />
            Has allergies
          </label>
          <label
            className="search-box"
            style={{
              display: "flex",
              alignItems: "center",
              gap: 8,
              marginLeft: "auto",
            }}
          >
            <Search size={16} aria-hidden="true" />
            <span className="visually-hidden">Search students</span>
            <input
              type="search"
              placeholder="Name, admission no., roll"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              style={{
                border: 0,
                outline: "none",
                background: "transparent",
                color: "inherit",
                font: "inherit",
                width: "100%",
              }}
            />
          </label>
          {anyFilter && (
            <button
              type="button"
              className="btn btn--secondary btn--sm"
              onClick={clearFilters}
            >
              Clear
            </button>
          )}
        </FilterBar>

        <DataTable
          caption="Students"
          columns={columns}
          rows={filtered}
          rowKey={(s) => s.id}
          loading={isLoading}
          resetPageKey={filterKey}
          empty={
            <EmptyState
              icon={GraduationCap}
              title={
                students.length === 0
                  ? "No students yet"
                  : "No students match these filters"
              }
              message={
                students.length === 0
                  ? "Students appear here once they are enrolled from Admissions."
                  : undefined
              }
              action={
                students.length > 0 ? (
                  <button
                    type="button"
                    className="btn btn--secondary"
                    onClick={clearFilters}
                  >
                    Clear filters
                  </button>
                ) : undefined
              }
            />
          }
        />
      </Panel>

      <StudentModal
        isOpen={isModalOpen}
        editingStudent={editingStudent}
        classSections={classSections}
        onClose={() => setIsModalOpen(false)}
        onSubmit={async (
          data: CreateStudentRequest,
          status: StudentStatus,
          identity: UpdateStudentIdentityRequest | null,
        ) => {
          if (editingStudent) {
            const {
              admNo: _admNo,
              admissionDate: _admissionDate,
              ...rest
            } = data;
            try {
              await studentService.update(editingStudent.id, {
                ...rest,
                status,
                rowVersion: editingStudent.rowVersion,
              });
            } catch (err) {
              // Someone else saved this student first: refresh the list so the next try starts fresh.
              if (isConcurrencyConflict(err)) await loadData();
              throw err;
            }
            // Identity numbers have their own endpoint and permission.
            if (identity) {
              await studentService.updateIdentity(editingStudent.id, identity);
            }
            toast.success(`${fullName(data)} was saved.`);
          } else {
            await studentService.create(data);
            toast.success(`${fullName(data)} was added.`);
          }
          await loadData();
        }}
      />

      <LinkedParentsViewModal
        student={linkedParentsTarget}
        onClose={() => setLinkedParentsTarget(null)}
      />
      <StudentHistoryModal
        student={historyTarget}
        onClose={() => setHistoryTarget(null)}
      />
      <ConfirmDialog
        isOpen={deleteTarget !== null}
        title="Delete student"
        message={
          deleteTarget
            ? `Delete ${fullName(deleteTarget)} (${deleteTarget.admNo})? This cannot be undone.`
            : ""
        }
        confirmLabel="Delete"
        onConfirm={confirmDelete}
        onCancel={() => setDeleteTarget(null)}
      />

      <ValidationModal errors={errors} onClose={() => setErrors(null)} />

      <ViewDetailsModal
        isOpen={detailsTarget !== null}
        title={detailsTarget ? fullName(detailsTarget) : ""}
        subtitle={detailsTarget?.admNo}
        onClose={() => setDetailsTarget(null)}
        sections={
          detailsTarget
            ? [
                {
                  heading: "Basics",
                  rows: [
                    { label: "Gender", value: detailsTarget.gender },
                    {
                      label: "Date of Birth",
                      value: detailsTarget.dateOfBirth,
                    },
                    { label: "Nationality", value: detailsTarget.nationality },
                    {
                      label: "Blood Group",
                      value: detailsTarget.health?.bloodGroup,
                    },
                  ],
                },
                {
                  heading: "Academic",
                  rows: [
                    { label: "Class", value: detailsTarget.classSectionName },
                    { label: "Roll Number", value: detailsTarget.rollNumber },
                    {
                      label: "Curriculum",
                      value: detailsTarget.curriculumTrack,
                    },
                    { label: "EAL", value: detailsTarget.ealCode },
                    { label: "House", value: detailsTarget.house },
                    { label: "Status", value: detailsTarget.status },
                  ],
                },
                {
                  heading: "Health",
                  rows: [
                    {
                      label: "Allergies",
                      value:
                        detailsTarget.health?.allergies ??
                        (detailsTarget.hasAllergies
                          ? "Yes (details need sensitive-data access)"
                          : "None recorded"),
                    },
                    {
                      label: "Dietary Requirements",
                      value: detailsTarget.health?.dietaryRequirements,
                    },
                    {
                      label: "Medical Notes",
                      value: detailsTarget.health?.medicalNotes,
                    },
                  ],
                },
              ]
            : []
        }
      />
    </div>
  );
}
