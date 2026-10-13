/**
 * G1b: file helpers for uploads. The browser checks a file BEFORE sending it, so the user gets the
 * answer at once (and a 50 MB scan is not uploaded just to be refused). The API repeats every check:
 * these are a convenience, never the rule.
 */

/** "5.31 MB (5,567,201 bytes)": the same wording as the API, readable and exact. */
export function describeSize(bytes: number): string {
  const exact = `${bytes.toLocaleString("en-US")} ${bytes === 1 ? "byte" : "bytes"}`;
  if (bytes < 1024) return exact;
  const readable =
    bytes >= 1024 * 1024
      ? `${trim(bytes / 1024 / 1024)} MB`
      : `${trim(bytes / 1024)} KB`;
  return `${readable} (${exact})`;
}

/** "5.31 MB" or "245 KB" for lists, without the exact bytes. */
export function shortSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} ${bytes === 1 ? "byte" : "bytes"}`;
  return bytes >= 1024 * 1024
    ? `${trim(bytes / 1024 / 1024)} MB`
    : `${trim(bytes / 1024)} KB`;
}

function trim(value: number): string {
  return String(Math.round(value * 100) / 100);
}

/** The message for a file over the limit (same text as the API's). */
export function tooLargeMessage(sizeBytes: number, limitBytes: number): string {
  return `The file is ${describeSize(sizeBytes)}. The largest file allowed is ${describeSize(limitBytes)}.`;
}

export const NOT_ACCEPTED_MESSAGE =
  "Only PDF, JPG and PNG files can be uploaded, and this file's content is none of them. " +
  "(The content is checked, not the name: renaming a file does not change what it is.)";

/** The content types the API accepts, detected from the first bytes like the API does. */
export type AcceptedFileType = "application/pdf" | "image/jpeg" | "image/png";

/**
 * What the file really is, from its first 8 bytes ("magic numbers"), not from its name or the type
 * the browser guesses from the extension. Null for anything that is not a PDF, JPG or PNG.
 */
export async function detectFileType(
  file: Blob,
): Promise<AcceptedFileType | null> {
  const head = new Uint8Array(await file.slice(0, 8).arrayBuffer());
  const startsWith = (bytes: number[]) => bytes.every((b, i) => head[i] === b);

  if (startsWith([0x25, 0x50, 0x44, 0x46, 0x2d])) return "application/pdf"; // %PDF-
  if (startsWith([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]))
    return "image/png";
  if (startsWith([0xff, 0xd8, 0xff])) return "image/jpeg";
  return null;
}

/** Hands a downloaded file to the browser as a normal download. */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  // Give the browser a moment to start the download before the URL is released.
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}

/** The file name from a Content-Disposition header (RFC 5987 filename* first), or the fallback. */
export function fileNameFromDisposition(
  header: string | undefined,
  fallback: string,
): string {
  if (!header) return fallback;
  const star = /filename\*=UTF-8''([^;]+)/i.exec(header);
  if (star) {
    try {
      return decodeURIComponent(star[1]);
    } catch {
      /* fall through to the plain name */
    }
  }
  const plain = /filename="?([^";]+)"?/i.exec(header);
  return plain ? plain[1] : fallback;
}
