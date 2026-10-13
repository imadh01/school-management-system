/**
 * G1a: addresses of students, parents and applicants. Same shapes as the API's AddressDtos.
 * Countries (with their states / emirates and postcode rule) come from GET /api/countries.
 */
export const ADDRESS_TYPES = ["Home", "Permanent", "Mailing"] as const;
export type AddressType = (typeof ADDRESS_TYPES)[number];

export interface AddressResponse {
  id: number;
  addressType: AddressType;
  isPrimary: boolean;
  line1: string;
  line2: string | null;
  city: string;
  countryId: number;
  countryName: string;
  regionId: number | null;
  regionName: string | null;
  postalCode: string | null;
}

export interface AddressRequest {
  addressType: AddressType;
  isPrimary: boolean;
  line1: string;
  line2: string | null;
  city: string;
  countryId: number;
  regionId: number | null;
  postalCode: string | null;
}

export interface CountryResponse {
  id: number;
  code: string;
  name: string;
  /** "State", "Emirate". */
  regionLabel: string;
  postalCodeRequired: boolean;
  postalCodePattern: string | null;
  postalCodeHint: string | null;
  regions: { id: number; name: string }[];
}

/** One address as the form holds it: ids as strings so "not chosen yet" is "". */
export interface AddressFormRow {
  addressType: AddressType;
  isPrimary: boolean;
  line1: string;
  line2: string;
  city: string;
  countryId: string;
  regionId: string;
  postalCode: string;
}
