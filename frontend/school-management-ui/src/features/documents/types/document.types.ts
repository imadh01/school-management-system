import type { BadgeTone } from "@/components/Badge";

/** G1b: Pending → Verified or Rejected; Verified → Pending (unverify). */
export type DocumentStatus = "Pending" | "Verified" | "Rejected";
export const DOCUMENT_STATUSES: readonly DocumentStatus[] = [
  "Pending",
  "Verified",
  "Rejected",
];

export type DocumentExpiryState = "Expired" | "ExpiringSoon";

/** Whose documents: a student, or an application that is not enrolled yet. */
export type DocumentOwner =
  { type: "student"; id: number } | { type: "admission"; id: number };

export interface DocumentTypeResponse {
  id: number;
  code: string;
  name: string;
  /** Identity / medical: downloading needs Students.ViewSensitive. */
  isSensitive: boolean;
  /** Passport, visa, Emirates ID: an expiry date is required. */
  hasExpiry: boolean;
}

export interface DocumentOptionsResponse {
  types: DocumentTypeResponse[];
  /** The upload limit (a server setting), so the form can refuse a big file before sending it. */
  maxFileSizeBytes: number;
  expiringSoonDays: number;
}

export interface DocumentResponse {
  id: number;
  ownerType: "Student" | "Admission";
  ownerId: number;
  ownerName: string;
  /** Admission number of a student, registration number of an application. */
  ownerNumber: string;
  documentTypeId: number;
  documentTypeName: string;
  isSensitive: boolean;
  hasExpiry: boolean;
  title: string | null;
  originalFileName: string;
  /** From the file's content: application/pdf, image/jpeg or image/png. */
  contentType: string;
  sizeBytes: number;
  expiryDate: string | null;
  expiryState: DocumentExpiryState | null;
  status: DocumentStatus;
  uploadedBy: string | null;
  uploadedAt: string;
  reviewedBy: string | null;
  reviewedAt: string | null;
  rejectionReason: string | null;
  /** The caller uploaded it, so the API refuses their Verify / Reject. */
  isOwnUpload: boolean;
  /** False for an identity or medical file when the caller lacks Students.ViewSensitive. */
  canDownload: boolean;
  rowVersion: string;
}

export interface UploadDocumentInput {
  documentTypeId: number;
  title: string;
  /** "yyyy-mm-dd" or "" */
  expiryDate: string;
  file: File;
}

/** Filters of the Documents page, sent to the API. */
export interface DocumentListFilters {
  status?: DocumentStatus;
  documentTypeId?: number;
  expiry?: DocumentExpiryState;
}

export const EXPIRY_LABEL: Record<DocumentExpiryState, string> = {
  Expired: "Expired",
  ExpiringSoon: "Expiring soon",
};

export const EXPIRY_TONE: Record<DocumentExpiryState, BadgeTone> = {
  Expired: "bad",
  ExpiringSoon: "warn",
};

/** "PDF", "JPG", "PNG" for the content types the API accepts. */
export function fileKind(contentType: string): string {
  switch (contentType) {
    case "application/pdf":
      return "PDF";
    case "image/jpeg":
      return "JPG";
    case "image/png":
      return "PNG";
    default:
      return contentType;
  }
}
