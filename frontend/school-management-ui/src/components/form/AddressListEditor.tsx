import { Info } from "lucide-react";
import type { AddressFormRow, CountryResponse } from "@/types/address";
import { newAddressRow, unusedTypes } from "@/utils/address";
import { Notice } from "../Notice";
import { SelectField, TextField } from "./Inputs";
import { AddRowButton, RepeatRow } from "./RepeatRow";

interface Props {
  /** Prefix for input ids ("student" → "student-addresses-0-city"). */
  idPrefix: string;
  rows: AddressFormRow[];
  onChange: (rows: AddressFormRow[]) => void;
  countries: CountryResponse[];
  /** Field errors keyed "addresses.0.city" (client checks and API errors alike). */
  errors: Record<string, string>;
  /** The old one-line address that could not be copied in G1a, shown as a hint until G1c. */
  legacyAddress?: string | null;
  /** True while the country list is loading. */
  countriesLoading?: boolean;
}

/**
 * G1a: the address block of every person form (student, parent, applicant). Up to three
 * addresses (Home, Permanent, Mailing), one of each, exactly one primary. The country has no
 * default; once chosen, "State" / "Emirate" lists that country's regions and the postcode
 * follows its rule (required, 6 digits for India; optional for the UAE).
 */
export function AddressListEditor({
  idPrefix,
  rows,
  onChange,
  countries,
  errors,
  legacyAddress,
  countriesLoading,
}: Props) {
  const update = (index: number, changes: Partial<AddressFormRow>) =>
    onChange(rows.map((r, i) => (i === index ? { ...r, ...changes } : r)));
  const remove = (index: number) => {
    const next = rows.filter((_, i) => i !== index);
    // Removing the primary address makes the first remaining one primary.
    if (
      rows[index].isPrimary &&
      next.length > 0 &&
      !next.some((r) => r.isPrimary)
    )
      next[0] = { ...next[0], isPrimary: true };
    onChange(next);
  };
  const add = () => {
    const row = newAddressRow(rows);
    if (row) onChange([...rows, row]);
  };

  return (
    <div>
      {legacyAddress && (
        <Notice icon={Info} style={{ marginBottom: 12 }}>
          Address on file from before the address update:{" "}
          <strong>{legacyAddress}</strong>. It could not be copied
          automatically. Enter it below with its country and save.
        </Notice>
      )}
      {errors.addresses && (
        <p className="field__error" style={{ marginBottom: 12 }}>
          {errors.addresses}
        </p>
      )}
      {rows.length === 0 && (
        <p className="cell-muted" style={{ margin: "0 0 12px" }}>
          No address added yet.
        </p>
      )}

      {rows.map((row, i) => {
        const country = countries.find((c) => String(c.id) === row.countryId);
        const id = (field: string) => `${idPrefix}-addresses-${i}-${field}`;
        const err = (field: string) => errors[`addresses.${i}.${field}`];
        return (
          <RepeatRow
            key={i}
            title={`${row.addressType} address`}
            onRemove={() => remove(i)}
          >
            <SelectField
              id={id("addressType")}
              label="Type"
              options={unusedTypes(rows, row.addressType)}
              value={row.addressType}
              onChange={(v) =>
                update(i, { addressType: v as AddressFormRow["addressType"] })
              }
              error={err("addressType")}
            />
            <TextField
              id={id("line1")}
              label="Address line 1"
              required
              placeholder="Villa / flat, building, street"
              value={row.line1}
              onChange={(v) => update(i, { line1: v })}
              error={err("line1")}
            />
            <TextField
              id={id("line2")}
              label="Address line 2"
              placeholder="Area, landmark"
              value={row.line2}
              onChange={(v) => update(i, { line2: v })}
              error={err("line2")}
            />
            <TextField
              id={id("city")}
              label="City"
              required
              value={row.city}
              onChange={(v) => update(i, { city: v })}
              error={err("city")}
            />
            <SelectField
              id={id("countryId")}
              label="Country"
              required
              emptyLabel={countriesLoading ? "Loading…" : "Select a country…"}
              options={countries.map((c) => ({ value: c.id, label: c.name }))}
              value={row.countryId}
              // A new country empties the region: the old one belongs to the other country.
              onChange={(v) => update(i, { countryId: v, regionId: "" })}
              error={err("countryId")}
            />
            <SelectField
              id={id("regionId")}
              label={country ? country.regionLabel : "Emirate / State"}
              required={!!country && country.regions.length > 0}
              disabled={!country}
              emptyLabel={
                country
                  ? `Select ${country.regionLabel.toLowerCase()}…`
                  : "Choose a country first"
              }
              options={(country?.regions ?? []).map((r) => ({
                value: r.id,
                label: r.name,
              }))}
              value={row.regionId}
              onChange={(v) => update(i, { regionId: v })}
              error={err("regionId")}
            />
            <TextField
              id={id("postalCode")}
              label="Postcode"
              required={!!country?.postalCodeRequired}
              hint={
                country ? (country.postalCodeHint ?? "Optional") : undefined
              }
              maxLength={12}
              value={row.postalCode}
              onChange={(v) => update(i, { postalCode: v })}
              error={err("postalCode")}
            />
            <label
              className="checkbox-row"
              style={{ marginTop: 0, minHeight: 44 }}
            >
              <input
                type="radio"
                name={`${idPrefix}-primaryAddress`}
                checked={row.isPrimary}
                onChange={() =>
                  onChange(rows.map((r, j) => ({ ...r, isPrimary: j === i })))
                }
              />
              Primary address
            </label>
          </RepeatRow>
        );
      })}

      <AddRowButton
        label="Add address"
        onClick={add}
        count={rows.length}
        max={3}
        pluralNoun="addresses (one Home, one Permanent, one Mailing)"
      />
    </div>
  );
}
