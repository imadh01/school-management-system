# Eschool — frontend

React + TypeScript + Vite. `npm run dev` starts it on http://localhost:5173 (the API must be running).
Before committing: `npx tsc -b`, `npx oxlint src`, `npx prettier --write src`.

## Folder layout

```
src/
  assets/       global CSS (layout.css, components.css, pages.css)
  components/   shared building blocks (below) — no module-specific code here
  features/<module>/{components,pages,services,types}
  hooks/        shared hooks (below)
  layouts/      app shell, sidebar, header
  routes/       AppRoutes.tsx, permissions.ts, guards
  services/     apiClient (JWT, refresh, correlation id)
  store/        AuthContext, ThemeContext
  types/        shared types and fixed choice lists (types/lookups.ts)
  utils/        small pure helpers
```

## How a module's pages are built (U2)

Every record type follows the same pattern. Copy an existing module (Parents is the simplest
complete one) rather than starting from scratch.

| Page | Route | Built from |
| --- | --- | --- |
| List | `/parents` | `PageHero` + `StatCard`, `Panel`, `StatusPills`, `FilterBar` (`FilterSelect`, `SearchBox`, `ClearFiltersButton`), `DataTable`, `ListEmptyState`, `RowActions`, `PersonCell`, `ExportCsvButton` |
| Detail | `/parents/:id` | `DetailHeader` (+ `EditButton`, `DeleteButton`), `Tabs`/`TabPanel` with `useTabParam`, `DetailCard`, `KeyValueGrid`, `ContactCard` |
| Add / edit | `/parents/new`, `/parents/:id/edit` | `FormPage` + `FormSection`, `useRecordForm`, `fieldBinder` + `TextField` / `SelectField` / `CheckboxField` / `ToggleGroup`, `RepeatRow` + `AddRowButton` |
| Small decision | pop-up | `Dialog` + `DialogFooter` + `useDialogSubmit`; a plain "Are you sure?" is `useConfirmAction` |

Routes for a module are one call in `routes/AppRoutes.tsx`:

```tsx
{recordRoutes(
  "/parents",
  { list: <ParentsPage />, detail: <ParentDetailPage />, form: <ParentFormPage /> },
  { create: Permission.ParentsCreate, edit: Permission.ParentsEdit },
)}
```

## Shared hooks

| Hook | What it does |
| --- | --- |
| `useAsyncData(load, deps, fallbackMessage)` | Loads data when the page opens; `{ data, error, isLoading, reload, setData }`. Ignores late answers for old ids. |
| `useRecordForm({ sections, initial, noun, onReload })` | Everything an add/edit page needs: form state, "unsaved changes", errors per field and section, the save flow, the 409 "someone else saved" message with Reload, the leave-page guard. Spread `pageProps` on `FormPage`, render `guard` once. |
| `useFilters(initial)` | All filters of a list in one object; `key` goes to `DataTable`'s `resetPageKey`. |
| `useConfirmAction({ title, message, run, success, failure })` | Confirm → request → toast. Render `dialog` once, call `ask(row)`. |
| `useDialogSubmit(onClose)` | Busy flag + API errors inside a pop-up + close on success. |
| `useTabParam(tabIds)` | The open tab of a detail page, kept in `?tab=`. |
| `useCan()` | `can(Permission.X)` — show/hide buttons only; the API checks every request. |

## Shared helpers

- `utils/format.ts` — `formatDate`, `ageOn` (dates from the API are calendar dates, never shifted by time zone).
- `utils/address.ts` + `components/form/AddressListEditor` + `components/detail/AddressList` + `hooks/useCountries` (G1a) — the address block of every person form and page: Home / Permanent / Mailing, one primary, country → state/emirate list and postcode rule from `GET /api/countries`. To add addresses to a new kind of person: keep `AddressFormRow[]` in the form state, convert with `toAddressRows` / `toAddressRequests`, check with `validateAddresses`.
- `utils/collections.ts` — `uniqueSorted` (filter drop-downs), `countBy` (status counts).
- `utils/csv.ts` — `downloadCsv(filename, headers, rows)`.
- `utils/form.ts` — `fieldBinder`, `blankToNull`, `numberOrNull`, `remapRowErrors`, `FormSectionDef`.
- `utils/apiError.ts` — turns API errors into messages / field errors.
- `utils/password.ts` + `components/form/PasswordRules` — the API's password rules, shown live.
- `types/lookups.ts` — fixed choice lists (blood groups, houses, relations, contact methods…). Lists the API validates live next to that module's types instead (e.g. `CLASS_STAGES`).

## Rules of thumb

- Permissions: names in `routes/permissions.ts` must match the backend `Permissions.cs`. Hide buttons with `useCan`; never rely on it for security.
- Teachers (F1 scoped roles) get reduced records from the API (e.g. `ParentContactResponse`); use the module's type guard (`isFullParent`, `isFullStudent`) before showing full fields.
- No `window.alert`, no second pop-up for errors: field errors go under the field, the rest in the banner of the page or dialog.
- Before adding a helper or component, look in `components/`, `hooks/` and `utils/` — if something close exists, extend it instead of copying it.
