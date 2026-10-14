/**
 * G1c: a document picked on the New registration form. The application does not exist yet, so
 * the file waits in the browser and is uploaded, one request per file, right after the
 * application is saved.
 */
export interface PendingDocument {
  /** Stable key for React and for retrying one file. */
  key: string;
  file: File;
  /** "PDF" / "JPG" / "PNG" from the content, or null when it is none of them. */
  kind: string | null;
  /** Browser-side problem with the file itself (too big, empty, wrong type). */
  fileError?: string;
  /** Selected document type id as a string ("" = not chosen). */
  typeId: string;
  title: string;
  /** "yyyy-mm-dd" or "" */
  expiryDate: string;
}
