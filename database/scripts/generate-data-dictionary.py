"""Generates docs/data-dictionary.md from ApplicationDbContextModelSnapshot.cs (run after every migration).
Usage: python3 database/scripts/generate-data-dictionary.py <snapshot.cs> <out.md> <last-migration-name>"""
import re, sys
snap, out, last = sys.argv[1:4]
text = open(snap, encoding="utf-8-sig").read().replace("\r\n", "\n")

DESC = {
 "Users": "Login accounts: one row per person who signs in (staff now; student/parent logins are a later decision).",
 "Roles": "Roles (Admin, Supervisor, Clerk, Teacher and any custom role). Permissions are granted to roles.",
 "Permissions": "The permission catalog: one row per `Module.Action` (kept equal to the code's catalog by a test).",
 "UserRoles": "Which role a user has (many-to-many table; the service allows one role per user).",
 "RolePermissions": "Which permissions a role grants (many-to-many).",
 "AcademicYears": "School years; exactly one has IsCurrent = 1.",
 "ClassSections": "A class + section in one academic year (capacity, class teacher).",
 "Teachers": "Teaching data of a user with the Teacher role, one-to-one with a user. Name and phone live on the staff profile.",
 "Subjects": "A subject in one class section, taught by at most one teacher.",
 "Admissions": "Admission applications (Draft, Confirmed, Enrolled, Rejected).",
 "AdmissionGuardians": "Father/mother/guardian written on an application (a snapshot, matched to real parents at enrolment).",
 "Students": "The student's current record (academic history lives in StudentEnrollments).",
 "StudentEnrollments": "Academic history: one row per class/section period; one Active period per student.",
 "StudentHealth": "Health and insurance details, one row per student.",
 "StudentIdentityDocuments": "Identity numbers and passport/visa details (sensitive), one row per student.",
 "StudentPickupPersons": "People authorised to collect the child.",
 "Parents": "Parent/guardian people, one row per real person.",
 "StudentGuardians": "Links a student to a parent, with the relation and the primary-contact flag.",
 "AttendanceSessions": "One roll call: class + date (+ optional subject).",
 "AttendanceRecords": "One student's status in one attendance session.",
 "Addresses": "Postal addresses for a student, parent, application or staff member (exactly one owner).",
 "AuditLogs": "Who changed what: one row per audited change, with sensitive values masked.",
 "Countries": "Reference list of countries (seeded).",
 "CountryRegions": "States / regions of a country (seeded), used by address forms.",
 "Documents": "Uploaded files (PDF/JPG/PNG) of a student or an application, with their review status.",
 "DocumentTypes": "Reference list of document types (birth certificate, passport...), with sensitivity and expiry flags.",
 "RefreshTokens": "Hashed refresh tokens for sign-in sessions (rotated; reuse is detected).",
 "SchoolSettings": "The school's own record: one row (name, contacts, time zone, currency, working days).",
 "StaffProfiles": "The person behind a staff login: name, contact and HR details, one per user.",
}

# ---- entities
ent_re = re.compile(r'modelBuilder\.Entity\("SchoolManagement\.Domain\.Entities\.(\w+)", b =>\n\s*\{\n(.*?)\n\s{16}\}\);', re.S)
blocks = {}   # entity -> list of bodies (property block, relationship block)
for m in ent_re.finditer(text):
    blocks.setdefault(m.group(1), []).append(m.group(2))

tables = {}      # entity -> table
for e, bodies in blocks.items():
    for b in bodies:
        m = re.search(r'b\.ToTable\("(\w+)"', b)
        if m: tables[e] = m.group(1)

def chain(body, start):
    """Text of the fluent chain beginning at index start, up to the terminating ';'."""
    end = body.index(";", start)
    return body[start:end]

class Col: pass
model = {}
ALLFKS = []
for e, bodies in blocks.items():
    main = next((b for b in bodies if "b.Property<" in b), "")
    rel = [b for b in bodies if "b.HasOne(" in b]
    cols, order = {}, []
    for m in re.finditer(r'b\.Property<([\w?.\[\]]+)>\("(\w+)"\)', main):
        if main[max(0, m.start()-40):m.start()].strip().endswith("UseIdentityColumn("): continue
        c = Col(); c.name = m.group(2); c.clr = m.group(1); ch = chain(main, m.start())
        t = re.search(r'HasColumnType\("([^"]+)"\)', ch); c.type = t.group(1) if t else c.clr
        c.null = c.clr.endswith("?") or (c.clr == "string" and ".IsRequired()" not in ch) or (c.clr == "byte[]" and ".IsRequired()" not in ch)
        notes = []
        d = re.search(r'HasDefaultValue\(([^)]*)\)', ch)
        if d: notes.append("default " + d.group(1).strip('"').lower() if d.group(1) in ("true","false") else "default " + d.group(1))
        ds = re.search(r'HasDefaultValueSql\("([^"]*)"\)', ch)
        if ds: notes.append(f"default `{ds.group(1)}`")
        cs = re.search(r'HasComputedColumnSql\("([^"]*)"', ch)
        if cs: notes.append(f"computed `{cs.group(1)}`")
        if "IsConcurrencyToken" in ch: notes.append("concurrency token (rowVersion)")
        if "ValueGeneratedOnAddOrUpdate" in ch and "rowversion" in c.type: notes.append("rowversion")
        c.notes = notes; c.keys = []
        cols[c.name] = c; order.append(c.name)
    # identity
    for m in re.finditer(r'UseIdentityColumn\(b\.Property<[\w?]+>\("(\w+)"\)\)', main):
        if m.group(1) in cols: cols[m.group(1)].notes.insert(0, "identity")
    pk = re.search(r'b\.HasKey\(([^)]*)\)', main)
    pkcols = re.findall(r'"(\w+)"', pk.group(1)) if pk else []
    for p in pkcols:
        if p in cols: cols[p].keys.append("PK" if len(pkcols) == 1 else "PK (composite)")
    idx = []
    for m in re.finditer(r'b\.HasIndex\(([^)]*)\)', main):
        ch = chain(main, m.start()); ic = re.findall(r'"(\w+)"', m.group(1))
        f = re.search(r'HasFilter\("([^"]*)"\)', ch); n = re.search(r'HasDatabaseName\("([^"]*)"\)', ch)
        idx.append((ic, "IsUnique" in ch, f.group(1) if f else "", n.group(1) if n else ""))
        if "IsUnique" in ch and len(ic) == 1 and ic[0] in cols:
            cols[ic[0]].keys.append("UQ" + (" (filtered)" if f else ""))
    checks = re.findall(r't\.HasCheckConstraint\("([^"]+)", "((?:[^"\\]|\\.)*)"\)', main)
    # relationships (may live in a separate later block)
    for r in rel:
        for m in re.finditer(r'b\.HasOne\("SchoolManagement\.Domain\.Entities\.(\w+)", (?:"\w+"|null)\)', r):
            ch = chain(r, m.start())
            fk = re.search(r'HasForeignKey\(([^)]*)\)', ch)
            od = re.search(r'DeleteBehavior\.(\w+)', ch)
            if not fk: continue
            names = re.findall(r'"([\w.]+)"', fk.group(1))
            dep = e
            if names and names[0].startswith("SchoolManagement.Domain.Entities."):
                dep = names[0].rsplit(".", 1)[1]; names = names[1:]
            principal = m.group(1) if dep == e else e
            ALLFKS.append((dep, names, principal, od.group(1) if od else "?"))
    model[e] = dict(cols=cols, order=order, idx=idx, checks=checks, table=tables.get(e, e))

for dep, cols_, principal, od in ALLFKS:
    for col in cols_:
        c = model[dep]["cols"].get(col)
        if c: c.keys.append(f"FK → {{{principal}}}" + (f" with {', '.join(cols_)}" if len(cols_) > 1 else "") + f" ({od})")
# resolve FK principal names to table names
for e, m in model.items():
    for c in m["cols"].values():
        c.keys = [re.sub(r"\{(\w+)\}", lambda x: tables.get(x.group(1), x.group(1)), k) for k in c.keys]

def sort_cols(m):
    names = m["order"]
    first = [n for n in names if n == "Id"]
    audit = ["CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "IsDeleted", "DeletedAt", "DeletedBy"]
    mid = [n for n in names if n != "Id" and n not in audit]
    return first + mid + [n for n in audit if n in names]

order = ["Users","Roles","Permissions","UserRoles","RolePermissions","StaffProfiles","SchoolSettings","RefreshTokens","AuditLogs",
         "AcademicYears","ClassSections","Teachers","Subjects","Admissions","AdmissionGuardians","Students","StudentEnrollments",
         "StudentHealth","StudentIdentityDocuments","StudentPickupPersons","Parents","StudentGuardians","Addresses","Countries",
         "CountryRegions","DocumentTypes","Documents","AttendanceSessions","AttendanceRecords"]
by_table = {m["table"]: (e, m) for e, m in model.items()}
missing = [t for t in by_table if t not in order]
order += sorted(missing)
order = [t for t in order if t in by_table]
total_cols = sum(len(by_table[t][1]["cols"]) for t in order)

o = []
o.append("# Data Dictionary (every column)\n")
o.append(f"Generated from `ApplicationDbContextModelSnapshot.cs` (the EF model after migration `{last}`), so it matches the real database.")
o.append("Regenerate it after every migration: `python3 database/scripts/generate-data-dictionary.py backend/SchoolManagement.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs docs/data-dictionary.md <last-migration-name>`.\n")
o.append("Legend: **PK** primary key · **FK** foreign key (→ table, delete behaviour) · **UQ** single-column unique · **NULL** nullable · Default shown when the model sets one.")
o.append(f"Total: **{len(order)} tables, {total_cols} columns**.\n")
o.append("Audit columns (`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`) and soft-delete columns (`IsDeleted`, `DeletedAt`, `DeletedBy`) repeat on many tables and are listed last in each table. Sensitive columns are masked in `AuditLogs`.\n")
o.append("## Contents\n")
for i, t in enumerate(order, 1):
    o.append(f"{i}. [{t}](#{t.lower()}) — {DESC.get(t, '')}")
o.append("")
for t in order:
    e, m = by_table[t]
    o.append(f"\n## {t}\n")
    o.append(f"{DESC.get(t, '')}  ")
    o.append(f"Entity: `{e}` · Columns: **{len(m['cols'])}**\n")
    o.append("| # | Column | SQL type | Null | Default / notes | Key |")
    o.append("|---|---|---|---|---|---|")
    for i, n in enumerate(sort_cols(m), 1):
        c = m["cols"][n]
        o.append(f"| {i} | `{n}` | {c.type} | {'NULL' if c.null else 'NOT NULL'} | {'; '.join(c.notes)} | {', '.join(c.keys)} |")
    if m["idx"]:
        o.append("\n**Indexes**\n")
        o.append("| Columns | Unique | Filter | Name |")
        o.append("|---|---|---|---|")
        for ic, uq, f, n in m["idx"]:
            o.append(f"| {', '.join(ic)} | {'yes' if uq else ''} | {('`'+f+'`') if f else ''} | {n} |")
    if m["checks"]:
        o.append("\n**Check constraints**\n")
        o.append("| Name | Rule |")
        o.append("|---|---|")
        for n, r in m["checks"]:
            o.append(f"| {n} | `{r.replace('|', chr(92)+'|')}` |")
open(out, "w", encoding="utf-8", newline="\n").write("\n".join(o) + "\n")
print(len(order), "tables,", total_cols, "columns")
