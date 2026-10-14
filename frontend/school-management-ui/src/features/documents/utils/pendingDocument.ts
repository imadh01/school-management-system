import {
  NOT_ACCEPTED_MESSAGE,
  detectFileType,
  tooLargeMessage,
} from "@/utils/file";
import { fileKind } from "../types/document.types";
import type { PendingDocument } from "../types/pendingDocument";

export const MAX_PENDING_DOCUMENTS = 10;

let counter = 0;

/** Checks one chosen file the way the upload dialog does: size, emptiness, real type. */
export async function toPendingDocument(
  file: File,
  limit: number,
): Promise<PendingDocument> {
  const detected = await detectFileType(file);
  return {
    key: `pending-${++counter}`,
    file,
    kind: detected && fileKind(detected),
    fileError:
      file.size > limit
        ? tooLargeMessage(file.size, limit)
        : file.size === 0
          ? "The file is empty."
          : detected === null
            ? NOT_ACCEPTED_MESSAGE
            : undefined,
    typeId: "",
    title: "",
    expiryDate: "",
  };
}
