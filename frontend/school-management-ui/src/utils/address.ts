import type {
  AddressFormRow,
  AddressRequest,
  AddressResponse,
  AddressType,
  CountryResponse,
} from "@/types/address";
import { ADDRESS_TYPES } from "@/types/address";

/**
 * G1a: converting addresses between the API and the form, and the checks the API makes, so
 * most mistakes show before a round trip. The API stays the authority (and the database behind it).
 */

export const toAddressRows = (addresses: AddressResponse[]): AddressFormRow[] =>
  addresses.map((a) => ({
    addressType: a.addressType,
    isPrimary: a.isPrimary,
    line1: a.line1,
    line2: a.line2 ?? "",
    city: a.city,
    countryId: String(a.countryId),
    regionId: a.regionId === null ? "" : String(a.regionId),
    postalCode: a.postalCode ?? "",
  }));

export const toAddressRequests = (rows: AddressFormRow[]): AddressRequest[] =>
  rows.map((r) => ({
    addressType: r.addressType,
    isPrimary: r.isPrimary,
    line1: r.line1.trim(),
    line2: r.line2.trim() || null,
    city: r.city.trim(),
    countryId: Number(r.countryId),
    regionId: r.regionId ? Number(r.regionId) : null,
    postalCode: r.postalCode.trim() || null,
  }));

/** A new row of the first type not used yet; the first address of a person is the primary one. */
export function newAddressRow(rows: AddressFormRow[]): AddressFormRow | null {
  const type = ADDRESS_TYPES.find(
    (t) => !rows.some((r) => r.addressType === t),
  );
  if (!type) return null;
  return {
    addressType: type,
    isPrimary: rows.length === 0,
    line1: "",
    line2: "",
    city: "",
    countryId: "", // no default country (decision 2)
    regionId: "",
    postalCode: "",
  };
}

export const unusedTypes = (
  rows: AddressFormRow[],
  keep?: AddressType,
): AddressType[] =>
  ADDRESS_TYPES.filter(
    (t) => t === keep || !rows.some((r) => r.addressType === t),
  );

function postalCodeError(
  row: AddressFormRow,
  country: CountryResponse,
): string | null {
  const code = row.postalCode.trim();
  if (!code)
    return country.postalCodeRequired
      ? `Postcode is required for ${country.name}.`
      : null;
  if (!country.postalCodePattern) return null;
  try {
    if (new RegExp(country.postalCodePattern).test(code)) return null;
  } catch {
    return null; // a pattern the browser cannot read: leave it to the API
  }
  return country.postalCodeHint
    ? `Postcode for ${country.name} must be ${country.postalCodeHint}.`
    : `This is not a valid postcode for ${country.name}.`;
}

/** Field errors keyed like the API's ("addresses.0.postalCode"). */
export function validateAddresses(
  rows: AddressFormRow[],
  countries: CountryResponse[],
  prefix = "addresses",
): Record<string, string> {
  const e: Record<string, string> = {};
  rows.forEach((row, i) => {
    const k = (field: string) => `${prefix}.${i}.${field}`;
    if (!row.line1.trim()) e[k("line1")] = "Address line 1 is required.";
    if (!row.city.trim()) e[k("city")] = "City is required.";
    const country = countries.find((c) => String(c.id) === row.countryId);
    if (!country) {
      e[k("countryId")] = "Choose a country.";
      return;
    }
    if (!row.regionId && country.regions.length > 0)
      e[k("regionId")] = `${country.regionLabel} is required.`;
    const postal = postalCodeError(row, country);
    if (postal) e[k("postalCode")] = postal;
  });
  if (rows.length > 0 && rows.filter((r) => r.isPrimary).length !== 1)
    e[prefix] = "Mark exactly one address as primary.";
  return e;
}
