/**
 * U2: the sections of the student edit page, in the same order as the old dialog, and which
 * form fields live in each. useRecordForm uses this to count errors per section.
 *
 * Field keys follow the API's property paths in camelCase: "firstName", "health.allergies",
 * "pickupPersons.0.phone", "identity.passportNumber".
 */
import type { FormSectionDef } from "@/utils/form";

export const STUDENT_FORM_SECTIONS: readonly FormSectionDef[] = [
  {
    id: "academic",
    title: "Identity & academic",
    fields: [
      "admNo",
      "rollNumber",
      "classSectionId",
      "admissionDate",
      "status",
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
    fields: ["mobile", "email", "addresses"],
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
];
