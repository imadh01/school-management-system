# Fee Structure: design (not built)

Status: design only. **Money is involved: nothing here is to be coded until you have answered the decisions.** Written 19 Oct 2026.
First part of the chain **Fee Structure -> Fees Collection -> Daily Accounts**. Section 7 ("Money rules") applies to the whole chain and is the contract for the next two designs.

## 1. What the prototype does

**Fee Structure screen** (the catalog)

- One row = one fee item: class section, category (Tuition, Admission, Transport, Exam, Annual), amount, frequency (Monthly, Quarterly, Annual, One Time), year, due day, late fee per day, active, allow installments + number of installments, tax, description.
- Duplicate rule: one item per (class, category, year).
- Filters: search (class, category), class, category, frequency, pipeline All / Active / Inactive. Add, Edit, Delete (hard).
- Sample data deliberately leaves Class 2 A mostly empty ("No fee items for this class").

**Fees Collection screen** (what the catalog feeds)

- Receipt list: receipt no. `RCP-2024-00000005`, date, student, category + billing period (free text, "Sept 2024"), paid and due, status (Paid / Partial / Pending / Failed / Refunded), mode (Cash, Cheque, Online, UPI, Card; sample data also has "Bank Transfer"). Stats: collected, pending, receipts.
- **Record Payment, Single Student:** student, fee item (only that class's active items), billing period, date, amount paid, late fee, discount, mode, reference, optional status override. Live computation: expected = fee + late - discount; due = max(0, expected - paid); status auto = Pending / Partial / Paid.
- **By Parent (Multi-Child):** pick a parent; one block per child (fee item, billing, paid, late, discount); creates one receipt per child with a paid amount.
- Row actions: View, Print, Email receipt, **Edit**, **Reverse Payment** (sets paid to 0 and status Pending), **Delete** (hard).

## 2. Where this conflicts with what we built and with sound money handling

1. **Currency:** the symbol is hard-coded as the rupee sign. The school is in Abu Dhabi and `SchoolSettings.CurrencyCode` exists ("AED", "INR"). Amounts are shown from that setting.
2. **Fee per class section:** the prototype repeats the same fee on 1 A, 1 B, 2 A. Our `AdmissionFees` master is per class name (all sections and years). Decision 1.
3. **Admission fee exists twice:** the "Admission" category here and the `AdmissionFees` master we already built (plus `Admissions.AdmissionFee`, `AdmissionFeeReference`). Decision 2.
4. **No demand/ledger:** the prototype never records what a student *owes*. A receipt with paid 0 and status Pending is the only way a due appears. A student who has not paid anything is invisible. Real dues need charges generated from the structure (decision 3).
5. **Payments can be edited, reversed without trace, and deleted.** Not acceptable for money. Replaced by void (section 7).
6. **Overpayment disappears:** paid above expected gives due 0 and the excess vanishes. We reject it (decision 8).
7. **Late fee is typed per payment,** not computed from `lateFeePerDay` and the due day. Decision 5.
8. **Tax is stored and ignored** (not in the expected total). Installments are stored and ignored. Decisions 6, 7.
9. **Due day is under-specified:** a Quarterly item due on the 15th of which months? An Annual item due on "30th" of which month? Decision 4.
10. **Billing period is free text** ("Sept 2024", "May 2026", or blank). It cannot be validated, summed, or checked for duplicates. Needs a structure (decision 4).
11. **Payment mode "Bank Transfer"** appears in data but not in the list; **UPI** is Indian. Decision 9.
12. **By Parent sets mode to Cash and the date to today,** ignoring the mode used. Fixed in Fees Collection design.
13. **Transport** has a flat amount per class, but real transport cost depends on route/zone and on whether the student uses transport at all. No assignment exists in the prototype. Decision 10.
14. **Category lists are hard-coded** in the script (decision 11).
15. **Students are matched by name/id of the current class.** We tie money to the **enrollment** (year-correct class), see section 4.

## 3. Business rules (Fee Structure module)

1. A fee item is defined for an **academic year**, a **class** (grade or section, decision 1) and a **category**. At most one live item per combination.
2. `Amount >= 0` (zero allowed for "free but tracked" items, decision 12); two decimals; currency is the school's single currency.
3. Frequency: Monthly, Quarterly, Annual, One Time. Frequency + due rule defines how many charges a year generates (section 8, decision 4).
4. `LateFeePerDay >= 0`, `Tax % between 0 and 100` (if kept), `Installments >= 1` (if kept).
5. **Edit:** allowed. Changing the amount affects only charges generated **after** the change; existing charges and payments keep their own copied amounts (they never read the catalog again).
6. **Deactivate** instead of delete once any charge or payment refers to the item. Inactive items cannot be used for new charges or payments but stay visible in history.
7. **Delete** (soft) only for an item never used by any charge/payment.
8. An item cannot be created for a Closed academic year, nor edited in one.
9. Every create/edit/deactivate/delete writes an audit row (existing interceptor), including old and new values of the amount.
10. **Copy from previous year:** admin action that copies all items of one year to another (target must have none), optionally with a percentage increase rounded to 2 decimals; returns the count (decision 13).

## 4. Database

`FeeCategories` (reference; only if decision 11 says so, otherwise a CHECK list)

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | |
| `Name` | nvarchar(50) | unique among live rows |
| `IsActive` | bit | |
| audit, `RowVersion`, `IsDeleted` | | |

`FeeStructureItems`

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | |
| `AcademicYearId` | int FK | Restrict |
| `ClassName` or `ClassSectionId` | nvarchar(50) / int FK | decision 1; same convention as `AdmissionFees.ClassName` if per grade |
| `FeeCategoryId` | int FK | Restrict |
| `Amount` | decimal(12,2) | CHECK >= 0 |
| `CurrencyCode` | char(3) | copy of the school currency at creation; see 7.2 |
| `Frequency` | nvarchar(20) | CHECK in (Monthly, Quarterly, Annual, OneTime) |
| `DueDay` | tinyint | CHECK 1-31 (clamped to month end) |
| `FirstDueMonth` | tinyint null | 1-12; needed for Quarterly/Annual (decision 4) |
| `LateFeePerDay` | decimal(12,2) | CHECK >= 0, default 0 |
| `TaxPercent` | decimal(5,2) | CHECK 0-100, default 0 (decision 6) |
| `AllowInstallments`, `InstallmentCount` | bit, tinyint | (decision 7) |
| `IsActive` | bit | |
| `Description` | nvarchar(300) null | |
| audit, `RowVersion`, `IsDeleted/DeletedAt/DeletedBy` | | |

Unique `(AcademicYearId, ClassName|ClassSectionId, FeeCategoryId)` filtered to `IsDeleted = 0`. Index `(AcademicYearId, ClassName)` for "fees of a class". Decimal precision: 12,2 holds up to 9,999,999,999.99, far more than needed and still exact.

**Not created in this module (designed with Fees Collection):** `FeeCharges`, `Payments`, `PaymentAllocations`, `NumberSequences` rows for receipts. Sketched in section 7 so the catalog's columns are right.

Migration: new tables only; no pre-check. If decision 2 merges `AdmissionFees` into this catalog, that is a **separate** data migration with a pre-check (row counts per class, amounts copied, `Down` restores) and is not started now.

## 5. Endpoints

| Method and path | Purpose |
|---|---|
| `GET /api/fee-structure/items` | paged; `academicYearId`, `className`/`classSectionId`, `categoryId`, `frequency`, `isActive`, `search` |
| `GET /api/fee-structure/items/{id}` | one item |
| `POST /api/fee-structure/items` | create; 409 duplicate; 422 closed year |
| `PUT /api/fee-structure/items/{id}` | edit; `rowVersion` |
| `POST /api/fee-structure/items/{id}/deactivate`, `/activate` | status |
| `DELETE /api/fee-structure/items/{id}` | soft delete; 409 if used |
| `POST /api/fee-structure/copy?fromYearId=&toYearId=&increasePercent=` | copy |
| `GET /api/fee-structure/categories` | lookup (if reference table) |

Amounts travel as JSON numbers with two decimals; the server is the only place totals are computed (the screen may show a preview, never save a total).

## 6. Permissions

Module `FeeStructure`: `View`, `Create`, `Edit`, `Delete`, `Copy`.

- Admin: all. Supervisor: View. Clerk: View (needed to quote fees). Teacher, Student, Parent: none. A parent sees only the fees of their own child later, through Fees Collection.
- The school has no accountant role today. Decision 14.
- Needs catalog, role seed, `ExpectedRoleMatrix`, `AccessMatrixTests` rows, catalog-sync migration.

## 7. Money rules (apply to Fee Structure, Fees Collection, Daily Accounts)

1. **Type:** every amount is C# `decimal` and SQL `decimal(12,2)`. Never `float`/`double`, never JavaScript arithmetic for anything stored. Rounding: `MidpointRounding.AwayFromZero` to 2 decimals, applied per line (late fee, tax, discount), then summed (decision 15).
2. **Currency:** one school currency, `SchoolSettings.CurrencyCode`. Every money row stores its own `CurrencyCode` copy so history cannot be reinterpreted. Changing the setting once any money row exists is refused with 409 (this is a behaviour change to Settings, decision 16). The UI formats with `Intl.NumberFormat` from the code, never a hard-coded symbol.
3. **Non-negative checks (database CHECKs, not only validators):** catalog amount >= 0; late fee >= 0; tax 0-100; payment amount > 0; discount >= 0 and <= fee + late fee; charge amount >= 0; allocated <= payment amount.
4. **Payments are never edited and never deleted.** There is no PUT and no DELETE on payments; the table has no soft-delete flag to flip. To correct a mistake: **void** and record a new payment.
5. **Void:** `POST /api/payments/{id}/void` with a mandatory reason (>= 10 characters). Sets `Status = Voided`, `VoidedAt`, `VoidedBy`, `VoidReason`. The amounts on the row never change. Voided payments are excluded from collected/pending totals and from Daily Accounts, but are listed (greyed) and keep their receipt number. Void twice -> 409. No un-void.
6. **Refund** is a **separate row** (`Kind = Refund`, positive amount, references the original payment, own number series and reason), limited to the original's remaining amount. "Failed" payment attempts (online) are `Status = Failed` and carry no amount toward totals. The prototype's "Refunded" status becomes a derived label (decision 17).
7. **Receipt numbers:** sequential from the existing counter mechanism (the one used for admission numbers), allocated inside the payment transaction under `sp_getapplock('Receipt:{year}')`. A voided receipt's number is never reused. Format `RCP-{year}-{8 digits}` as the prototype; year = calendar year of the payment date in the school time zone (decision 18).
8. **Audit row for every change:** the payments, allocations, charges, fee items and the fee categories are audited with the existing `AuditableEntitySaveChangesInterceptor` and **none carries `[AuditIgnore]`**. A test fails if a money entity is not audited. Void writes the reason into the audit row. No endpoint can delete or edit `AuditLogs`. Each audit row stores user id and UTC time; the screen shows school-time.
9. **Atomicity:** recording a payment (number, payment row, allocations, charge balance updates) is one transaction under `ExecuteInTransactionAsync`. Two cashiers paying the same charge cannot both succeed beyond its balance: the balance is re-read under the lock `FeeCharge:{id}` and `rowversion` on the charge.
10. **Overpayment:** rejected with 422 (decision 8).
11. **Dates:** payment date cannot be in the future (school-time-zone "today") and cannot be older than N days unless Admin (decision 19). Totals by day use the school time zone, not UTC (the attendance UTC problem must not be repeated).
12. **Who:** separate permissions `Payments.Record`, `Payments.Void`, `Payments.Refund`, `Fees.Discount` (discount/waiver above a limit needs Admin) (decision 14).
13. **Identity:** money rows reference `StudentEnrollmentId` (year-correct class) as well as `StudentId`. A student's fees for 2026-2027 never move when they are promoted.
14. **Reads are not audited,** but exports of financial reports are logged with user and filter.
15. **Never logged:** payment references, card details. No card numbers are stored at all; "Card" and "Online" modes store only a reference text.

**Sketch for the next design (not built):**

- `FeeCharges (Id, StudentEnrollmentId, FeeStructureItemId, PeriodStart, PeriodEnd, DueDate, Amount, LateFee, Discount, CurrencyCode, Status Open|Settled|Cancelled, ...)`: the "what is owed" ledger, created from the structure (generate for a class + month, or on enrollment). Amounts copied, never read back from the catalog.
- `Payments (Id, ReceiptNo, StudentEnrollmentId, PaymentDate, Amount, CurrencyCode, Mode, Reference, Status Posted|Voided|Failed, Kind Payment|Refund, VoidedAt/By/Reason, ...)`.
- `PaymentAllocations (PaymentId, FeeChargeId, Amount)`: one payment can cover several charges; a charge can be paid in parts.
- Status of a charge (Paid / Partial / Pending) is **computed** from allocations, never stored or overridden by the clerk (the prototype's "status override" is dropped).
- Daily Accounts reads Posted payments minus refunds by school-time date and mode.

## 8. Frontend

Same table, filters, pipeline and modal as the prototype. Amount inputs accept two decimals, reject negatives, show the currency from settings. The Delete button is Deactivate once used (with a tooltip explaining why). Category and year use searchable selects; confirm dialogs and toasts as elsewhere.

## 9. Tests to write

Duplicate item 409; amount/late/tax/day CHECK violations rejected by validator and by the database; edit changes only the catalog (a pre-existing charge keeps its amount); delete of a used item refused, deactivate allowed; Closed-year item cannot be created/edited; copy refuses a non-empty target and applies the percentage with correct rounding (for example 1,500.00 + 3 % = 1,545.00, 99.99 + 3 % = 102.99); role matrix rows; audit test: every money entity writes audit rows on insert, update, and soft delete; decimal round-trip test (123.45 saved and read back exact); a test that Payment has no update/delete endpoint (when that module exists).

## Decisions for you

1. **Fee per grade or per section?** Per grade + year (recommended, matches `AdmissionFees`), with a section override only if you need it, or per section exactly as the prototype?
2. **Admission fee:** keep `AdmissionFees` separate for now, or make "Admission" an ordinary fee item and retire `AdmissionFees` later through a data migration? Recommended: keep both until Fees Collection is designed, then merge.
3. **Charges (dues):** generate charges for each student from the structure (recommended; gives real dues, reminders, aging) or keep the prototype's payment-first model? This is the largest decision in the chain.
4. **Billing period and due schedule:** replace the free text with a real period (month + year, quarter, year, one-time) and add `FirstDueMonth` for Quarterly/Annual? What are the real due months? Which months does a school year cover (for Monthly: 10 charges or 12)?
5. **Late fee:** computed automatically as `LateFeePerDay x days late` (with an optional cap), or typed by the cashier as today? Does it start the day after the due date? Can it be waived, and by whom?
6. **Tax:** is any tax applied to school fees? I will not assume. If none, drop `TaxPercent`. If yes, confirm rate and rounding with your accountant.
7. **Installments:** drop for now (the prototype stores but never uses them) or design real instalment plans? Recommended: drop.
8. **Overpayment:** reject (recommended), or keep the excess as an advance credit on the student?
9. **Payment modes for the UAE:** Cash, Cheque, Card, Bank Transfer, Online. Remove UPI? Is a reference required for non-cash modes? Reference list or fixed list?
10. **Transport fee:** flat per class (prototype) or by route/zone with a student-to-transport assignment (needs the Transport module)? Optional items (Transport) need a per-student assignment to generate charges.
11. **Categories:** fixed five or a `FeeCategories` master you can extend? Recommended: master.
12. **Zero-amount items** (a fee that is free but tracked): allowed?
13. **Copy to next year** with a percentage increase: wanted? Default increase 0 %?
14. **Roles:** do you want a separate **Accountant** role (Payments.Record/Void/Refund, FeeStructure.Edit) instead of using Admin and Clerk? Who may void and refund? Discount limit for non-Admin?
15. **Rounding** per line, half away from zero (recommended)?
16. **Block changing the school currency** once any money row exists? (Recommended: yes.)
17. **Refund policy:** full or partial refunds; do refunds need Admin approval; does a refund reopen the charge?
18. **Receipt numbering year:** calendar year of the payment (prototype) or academic year? Prefix `RCP-` kept?
19. **Backdating:** how many days back may a cashier enter a payment date? Recommended: 0 for Clerk (today only), unlimited with reason for Admin.
20. **Existing data:** is there any real fee or payment data to import, or does everything start empty? (Affects the migration plan.)
