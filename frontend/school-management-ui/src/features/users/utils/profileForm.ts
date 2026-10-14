/**
 * Pieces the Users form and My Account share: both send a staff profile and show the same field
 * rules as the API (PhoneNumberRules, "Profile.Gender" error keys).
 */

/** Same rule as the API's PhoneNumberRules. */
export const PHONE_PATTERN = /^\+?[0-9\s\-().]{5,20}$/;
export const PHONE_MESSAGE = "Phone is invalid (digits, spaces, + - ( ) only).";

export const EMAIL_PATTERN = /^\S+@\S+\.\S+$/;

export const blank = (v: string) => v.trim() || null;

/** The API reports profile problems as "Profile.Gender"; the form's fields are flat ("gender"). */
export const remapProfileErrors = (fields: Record<string, string>) =>
  Object.fromEntries(
    Object.entries(fields).map(([k, m]) => [k.replace(/^profile\./i, ""), m]),
  );
