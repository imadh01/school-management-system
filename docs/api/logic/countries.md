# Countries controller: what the endpoint actually does

Source: `backend/SchoolManagement.API/Controllers/CountriesController.cs`, route `api/countries`.
Written for a developer with about two years of experience who is learning this codebase.

## How this file was made

- **Everything here was read from the code**: the controller, `AddressService.GetCountriesAsync` and `BuildAsync`, and the access matrix row.
- **I did not run the API or the database.** The example is invented.
- Tests: the access matrix covers the permission; `AddressRulesTests` covers the address rules in the database. I did not run them in this session.
- The doubts I noticed are at the end.

## The one endpoint

| # | Method + URL | Permission needed | Called by (React) |
|---|---|---|---|
| 1 | `GET /api/countries` | any signed-in user | every form that has an address (users, students, parents, admissions, My Account) |

## Background

Reference data for addresses: each country has its regions (emirates for the UAE, states for India, ...), a label for the region ("Emirate", "State"), and a postcode rule. The data is seeded by migrations; there is **no endpoint to add or edit countries**.

---

## 1. `GET /api/countries`

### 1a. Purpose and caller
Fills the country and region drop-downs and tells the form how to validate a postcode.

### 1b. Request
None.

### 1c. Response
`200 OK`, array of `CountryResponse`: `id, code, name, regionLabel, postalCodeRequired, postalCodePattern, postalCodeHint, regions[{ id, name }]`.

### 1d. Logic, in order
1. Signed-in check only.
2. Load countries with their regions.
3. Keep **active** countries, sorted by sort order then name; for each keep **active** regions, sorted the same way.

### 1e. Business rules
| Rule | Where |
|---|---|
| Inactive countries and regions are hidden from the lists | service |
| An address that already uses an inactive country/region can still be saved unchanged | `AddressService.BuildAsync` accepts inactive ones on save |
| The server re-checks every address on save (country exists, region belongs to the country, a region is required when the country has regions, postcode pattern) | `AddressService.BuildAsync`, `AddressRequestValidator` |

### 1f. Errors
401.

### 1g. Concurrency and retries
Read only. The list is small and can be cached by the browser.

### 1h. Why is it built this way?
The frontend gets the rules with the data, so the form and the server agree; the server still re-checks.

### Example (invented)
```json
[ { "id": 1, "code": "AE", "name": "United Arab Emirates", "regionLabel": "Emirate", "postalCodeRequired": false,
    "postalCodePattern": null, "postalCodeHint": null, "regions": [ { "id": 1, "name": "Abu Dhabi" }, { "id": 2, "name": "Dubai" } ] } ]
```

---

## Doubts and possible problems

### Blocker
None found.

### Should fix
None found.

### Question
1. **No screen or endpoint to maintain countries.** Is adding a country a migration/seed task by design?
2. **The list is sent in full each time** (all countries with all regions). Fine for a few countries; if the full world list is added later, consider caching or paging.
3. **Is the UAE meant to be the default country?** The default comes from the school settings (`countryId`), see `settings-and-masters.md`.
