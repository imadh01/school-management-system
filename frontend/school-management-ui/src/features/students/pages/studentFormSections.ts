/**
 * U2: the sections of the student edit page, in the same order as the old dialog, and which
 * form fields live in each. The section list uses this to put an error count next to a section.
 *
 * Field keys follow the API's property paths in camelCase: "firstName", "health.allergies",
 * "pickupPersons.0.phone", "identity.passportNumber".
 */
export const STUDENT_FORM_SECTIONS = [
  {
    id: "academic",
    title: "Identity & academic",
    fields: [
      "admNo",
      "rollNumber",
      "classSectionId",
      "admissionDate",
      "status",
      "photoUrl",
    ],
  },
  {
    id: "personal",
    title: "Personal",
    fields: [
      "firstName",
      "middleName",
      "lastName",
      "preferredName",
      "gender",
      "dateOfBirth",
      "health.bloodGroup",
    ],
  },
  {
    id: "contact",
    title: "Contact & address",
    fields: ["mobile", "email", "addressLine", "city", "state", "pincode"],
  },
  { id: "guardians", title: "Parents / guardians", fields: [] },
  {
    id: "other",
    title: "Other info",
    fields: [
      "category",
      "religion",
      "previousSchool",
      "transportRequired",
      "transportRoute",
      "health.medicalNotes",
    ],
  },
  {
    id: "citizenship",
    title: "Citizenship",
    fields: ["nationality", "secondNationality", "countryOfBirth"],
  },
  {
    id: "languages",
    title: "Languages & curriculum",
    fields: [
      "motherTongue",
      "homeLanguage",
      "englishProficiency",
      "curriculumTrack",
      "ealCode",
      "admissionType",
    ],
  },
  {
    id: "safeguarding",
    title: "Safeguarding & pickup",
    fields: ["custodyArrangement", "mediaConsent", "pickupPersons"],
  },
  {
    id: "health",
    title: "Health & safety",
    fields: [
      "health.dietaryRequirements",
      "health.allergies",
      "health.insuranceProvider",
      "health.insurancePolicyExpiry",
    ],
  },
  { id: "house", title: "House", fields: ["house"] },
  {
    id: "welfare",
    title: "Welfare / finance",
    fields: ["feeConcessionPercent", "health.specialEducationalNeeds"],
  },
  { id: "identity", title: "Identity documents", fields: ["identity"] },
] as const;

/** The section a field key belongs to, or undefined (then its message goes to the top banner). */
export function sectionOfField(key: string): string | undefined {
  for (const section of STUDENT_FORM_SECTIONS) {
    for (const field of section.fields as readonly string[]) {
      if (key === field || key.startsWith(field + ".")) return section.id;
    }
  }
  return undefined;
}
