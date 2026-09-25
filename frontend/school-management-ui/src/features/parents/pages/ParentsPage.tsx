import { useEffect, useMemo, useState } from "react";
import { parentService } from "../services/parentService";
import type { ParentResponse } from "../types/parent.types";
import { ParentModal } from "../components/ParentModal";

export function ParentsPage() {
  const [parents, setParents] = useState<ParentResponse[]>([]);
  const [search, setSearch] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);

  const loadData = async () => {
    setIsLoading(true);
    setParents(await parentService.getAll());
    setIsLoading(false);
  };

  useEffect(() => {
    loadData();
  }, []);

  const filtered = useMemo(() => {
    if (!search) return parents;
    const q = search.toLowerCase();
    return parents.filter((p) =>
      `${p.name} ${p.mobile} ${p.email ?? ""}`.toLowerCase().includes(q),
    );
  }, [parents, search]);

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
        <button
          className="btn btn--primary"
          onClick={() => setIsModalOpen(true)}
        >
          + Add Parent
        </button>
      </div>

      <div className="filters">
        <div className="field">
          <label>Search</label>
          <input
            placeholder="Name, mobile, email..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <button
          className="btn btn--secondary filters__clear"
          onClick={() => setSearch("")}
        >
          Clear
        </button>
      </div>

      {isLoading ? (
        <div className="empty-state">Loading…</div>
      ) : filtered.length === 0 ? (
        <div className="empty-state">No parents found.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Mobile</th>
              <th>Email</th>
              <th>Occupation</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((p) => (
              <tr key={p.id}>
                <td>{p.name}</td>
                <td>{p.mobile}</td>
                <td>{p.email || "—"}</td>
                <td>{p.occupation || "—"}</td>
                <td>
                  <span
                    className={`badge ${p.status === "Active" ? "badge--active" : "badge--inactive"}`}
                  >
                    {p.status}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <ParentModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSubmit={async (data) => {
          await parentService.create(data);
          await loadData();
        }}
      />
    </div>
  );
}
