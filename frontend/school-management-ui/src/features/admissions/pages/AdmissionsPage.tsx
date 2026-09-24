import { useEffect, useMemo, useState } from "react";
import { admissionService } from "../services/admissionService";
import { classSectionService } from "@/features/class-sections/services/classSectionService";
import type {
  AdmissionResponse,
  AdmissionStatus,
} from "../types/admission.types";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";
import { RegistrationModal } from "../components/RegistrationModal";
import { ConfirmAdmissionModal } from "../components/ConfirmAdmissionModal";
import { EnrollModal } from "../components/EnrollModal";

// Matches the prototype's PIPELINE_CLASS / STATUS_BADGE_CLASS exactly.
const PIPELINE_STEPS: {
  key: AdmissionStatus | "All";
  label: string;
  activeClass: string;
}[] = [
  { key: "All", label: "All", activeClass: "pipeline__step--dark" },
  {
    key: "Registered",
    label: "Registered",
    activeClass: "pipeline__step--active",
  },
  { key: "Admitted", label: "Admitted", activeClass: "pipeline__step--amber" },
  { key: "Enrolled", label: "Enrolled", activeClass: "pipeline__step--green" },
  { key: "Rejected", label: "Rejected", activeClass: "" },
];

const STATUS_BADGE_CLASS: Record<AdmissionStatus, string> = {
  Registered: "badge--info",
  Admitted: "badge--warn",
  Enrolled: "badge--active",
  Rejected: "badge--inactive",
};

export function AdmissionsPage() {
  const [admissions, setAdmissions] = useState<AdmissionResponse[]>([]);
  const [classSections, setClassSections] = useState<ClassSectionResponse[]>(
    [],
  );
  const [statusFilter, setStatusFilter] = useState<AdmissionStatus | "All">(
    "All",
  );
  const [search, setSearch] = useState("");
  const [isLoading, setIsLoading] = useState(true);

  const [isRegistrationOpen, setIsRegistrationOpen] = useState(false);
  const [editingAdmission, setEditingAdmission] =
    useState<AdmissionResponse | null>(null);
  const [confirmTarget, setConfirmTarget] = useState<AdmissionResponse | null>(
    null,
  );
  const [enrollTarget, setEnrollTarget] = useState<AdmissionResponse | null>(
    null,
  );

  const loadData = async () => {
    setIsLoading(true);
    const [admissionsData, classSectionsData] = await Promise.all([
      admissionService.getAll(),
      classSectionService.getAll(),
    ]);
    setAdmissions(admissionsData);
    setClassSections(classSectionsData);
    setIsLoading(false);
  };

  useEffect(() => {
    loadData();
  }, []);

  const counts = useMemo(() => {
    const c: Record<string, number> = {
      All: admissions.length,
      Registered: 0,
      Admitted: 0,
      Enrolled: 0,
      Rejected: 0,
    };
    admissions.forEach((a) => {
      c[a.status] = (c[a.status] ?? 0) + 1;
    });
    return c;
  }, [admissions]);

  const filtered = useMemo(() => {
    return admissions.filter((a) => {
      if (statusFilter !== "All" && a.status !== statusFilter) return false;
      if (!search) return true;
      const haystack =
        `${a.firstName} ${a.lastName} ${a.regNo} ${a.email ?? ""} ${a.phone}`.toLowerCase();
      return haystack.includes(search.toLowerCase());
    });
  }, [admissions, statusFilter, search]);

  const openCreate = () => {
    setEditingAdmission(null);
    setIsRegistrationOpen(true);
  };

  const openEdit = (a: AdmissionResponse) => {
    setEditingAdmission(a);
    setIsRegistrationOpen(true);
  };

  const handleReject = async (a: AdmissionResponse) => {
    const reason = window.prompt(
      `Reason for rejecting ${a.firstName} ${a.lastName} (optional):`,
      "",
    );
    if (reason === null) return; // cancelled
    await admissionService.reject(a.id, { rejectionReason: reason.trim() });
    await loadData();
  };

  return (
    <div>
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          marginBottom: 16,
        }}
      >
        <div />
        <button className="btn btn--primary" onClick={openCreate}>
          + New Registration
        </button>
      </div>

      <div className="pipeline">
        {PIPELINE_STEPS.map((step) => (
          <button
            key={step.key}
            className={`pipeline__step ${statusFilter === step.key ? step.activeClass : ""}`}
            onClick={() => setStatusFilter(step.key)}
          >
            {step.label} ({counts[step.key] ?? 0})
          </button>
        ))}
      </div>

      <div className="filters">
        <div className="field">
          <label>Search</label>
          <input
            placeholder="Name, reg. no, email, phone..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <button
          className="btn btn--secondary filters__clear"
          onClick={() => {
            setSearch("");
            setStatusFilter("All");
          }}
        >
          Clear
        </button>
      </div>

      {isLoading ? (
        <div className="empty-state">Loading…</div>
      ) : filtered.length === 0 ? (
        <div className="empty-state">No admissions match this filter.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Reg. No.</th>
              <th>Applicant</th>
              <th>Gender</th>
              <th>DOB</th>
              <th>Applied For</th>
              <th>Type</th>
              <th>Contact</th>
              <th>Reg. Date</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((a) => (
              <tr key={a.id}>
                <td>{a.regNo}</td>
                <td>
                  {a.firstName} {a.lastName}
                </td>
                <td>{a.gender}</td>
                <td>{a.dateOfBirth}</td>
                <td>{a.appliedForClassSectionName}</td>
                <td>{a.admissionType}</td>
                <td>{a.phone}</td>
                <td>{a.registrationDate}</td>
                <td>
                  <span className={`badge ${STATUS_BADGE_CLASS[a.status]}`}>
                    {a.status}
                  </span>
                </td>
                <td>
                  <button
                    className="btn--icon"
                    title="Edit"
                    onClick={() => openEdit(a)}
                  >
                    ✎
                  </button>
                  {a.status === "Registered" && (
                    <button
                      className="btn--icon"
                      title="Confirm Admission"
                      onClick={() => setConfirmTarget(a)}
                    >
                      ✓
                    </button>
                  )}
                  {a.status === "Admitted" && (
                    <button
                      className="btn--icon"
                      title="Enroll to Class"
                      onClick={() => setEnrollTarget(a)}
                    >
                      🎓
                    </button>
                  )}
                  {(a.status === "Registered" || a.status === "Admitted") && (
                    <button
                      className="btn--icon danger"
                      title="Reject"
                      onClick={() => handleReject(a)}
                    >
                      ⊘
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <RegistrationModal
        isOpen={isRegistrationOpen}
        editingAdmission={editingAdmission}
        classSections={classSections}
        onClose={() => setIsRegistrationOpen(false)}
        onSubmit={async (data) => {
          if (editingAdmission) {
            await admissionService.update(editingAdmission.id, data);
          } else {
            await admissionService.create(data);
          }
          await loadData();
        }}
      />

      <ConfirmAdmissionModal
        admission={confirmTarget}
        onClose={() => setConfirmTarget(null)}
        onSubmit={async (id, data) => {
          await admissionService.confirmAdmission(id, data);
          await loadData();
        }}
      />

      <EnrollModal
        admission={enrollTarget}
        classSections={classSections}
        onClose={() => setEnrollTarget(null)}
        onSubmit={async (id, data) => {
          await admissionService.enroll(id, data);
          await loadData();
        }}
      />
    </div>
  );
}
