import axios from "axios";
import { apiClient } from "@/services/apiClient";
import { fileNameFromDisposition, saveBlob } from "@/utils/file";
import type {
  DocumentListFilters,
  DocumentOptionsResponse,
  DocumentOwner,
  DocumentResponse,
  UploadDocumentInput,
} from "../types/document.types";

const ownerPath = (owner: DocumentOwner) =>
  owner.type === "student"
    ? `/students/${owner.id}/documents`
    : `/admissions/${owner.id}/documents`;

let cachedOptions: Promise<DocumentOptionsResponse> | null = null;

export const documentService = {
  /**
   * Types and the size limit (settings). Loaded once per page load and shared by every upload
   * form; a failed load is not cached.
   */
  getOptions(): Promise<DocumentOptionsResponse> {
    if (!cachedOptions) {
      cachedOptions = apiClient
        .get<DocumentOptionsResponse>("/documents/options")
        .then((res) => res.data)
        .catch((err) => {
          cachedOptions = null;
          throw err;
        });
    }
    return cachedOptions;
  },

  /** The Documents page. */
  getAll: (filters: DocumentListFilters = {}) =>
    apiClient
      .get<DocumentResponse[]>("/documents", { params: filters })
      .then((res) => res.data),

  /** The Documents tab of a student or an application. */
  getForOwner: (owner: DocumentOwner) =>
    apiClient.get<DocumentResponse[]>(ownerPath(owner)).then((res) => res.data),

  /** One file per request, as multipart/form-data. The API checks the size and the real type again. */
  upload: (owner: DocumentOwner, input: UploadDocumentInput) => {
    const form = new FormData();
    form.append("documentTypeId", String(input.documentTypeId));
    if (input.title.trim()) form.append("title", input.title.trim());
    if (input.expiryDate) form.append("expiryDate", input.expiryDate);
    form.append("file", input.file, input.file.name);
    return apiClient
      .post<DocumentResponse>(ownerPath(owner), form, {
        headers: { "Content-Type": "multipart/form-data" },
      })
      .then((res) => res.data);
  },

  /** Downloads through the API (permission check + audit row), then saves it under its real name. */
  download: async (document: DocumentResponse) => {
    const res = await apiClient
      .get<Blob>(`/documents/${document.id}/file`, { responseType: "blob" })
      .catch(async (err: unknown) => {
        // With responseType "blob" an error body arrives as a Blob too: turn it back into the
        // usual JSON error, so getApiErrors can show the API's message (e.g. "needs Students.ViewSensitive").
        if (axios.isAxiosError(err) && err.response?.data instanceof Blob) {
          try {
            err.response.data = JSON.parse(await err.response.data.text());
          } catch {
            /* not JSON: keep the fallback message */
          }
        }
        throw err;
      });
    saveBlob(
      res.data,
      fileNameFromDisposition(
        res.headers["content-disposition"] as string | undefined,
        document.originalFileName,
      ),
    );
  },

  verify: (document: DocumentResponse) =>
    apiClient
      .post<DocumentResponse>(`/documents/${document.id}/verify`, {
        rowVersion: document.rowVersion,
      })
      .then((res) => res.data),

  reject: (document: DocumentResponse, reason: string) =>
    apiClient
      .post<DocumentResponse>(`/documents/${document.id}/reject`, {
        reason,
        rowVersion: document.rowVersion,
      })
      .then((res) => res.data),

  unverify: (document: DocumentResponse) =>
    apiClient
      .post<DocumentResponse>(`/documents/${document.id}/unverify`, {
        rowVersion: document.rowVersion,
      })
      .then((res) => res.data),

  delete: (document: DocumentResponse) =>
    apiClient.delete(`/documents/${document.id}`),
};
