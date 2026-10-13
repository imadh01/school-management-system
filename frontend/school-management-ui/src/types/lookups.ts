/**
 * Fixed choices offered in drop-downs across modules (student, admission, enrolment...). One list
 * each, so adding "AB-" or a new house happens in one place. Where the API validates a value
 * (e.g. class stages), the list lives next to that module's types instead and must match the API.
 */
export const BLOOD_GROUPS = [
  "A+",
  "A-",
  "B+",
  "B-",
  "AB+",
  "AB-",
  "O+",
  "O-",
] as const;
export const RELIGIONS = [
  "Hindu",
  "Muslim",
  "Christian",
  "Sikh",
  "Buddhist",
  "Jain",
  "Other",
] as const;
export const CATEGORIES = ["General", "OBC", "SC", "ST", "EWS"] as const;
export const ENGLISH_LEVELS = [
  "Native",
  "C2 Proficient",
  "C1 Advanced",
  "B2 Upper Intermediate",
  "B1 Intermediate",
  "A2 Elementary",
] as const;
export const CURRICULA = ["British", "American", "IB", "National"] as const;
export const HOUSES = ["Red", "Blue", "Green", "Yellow"] as const;
export const CUSTODY_ARRANGEMENTS = [
  "Joint",
  "Mother",
  "Father",
  "Guardian",
] as const;
export const DIETARY_OPTIONS = [
  "Halal",
  "Kosher",
  "Vegetarian",
  "Vegan",
  "Pescatarian",
  "Dairy-Free",
  "Gluten-Free",
  "Nut-Free",
] as const;
export const GENDERS = ["Male", "Female", "Other"] as const;
export const GUARDIAN_RELATIONS = ["Father", "Mother", "Guardian"] as const;
export const CONTACT_METHODS = ["Email", "Phone", "WhatsApp", "App"] as const;
export const LANGUAGES = [
  "English",
  "Hindi",
  "Urdu",
  "Arabic",
  "Korean",
] as const;
