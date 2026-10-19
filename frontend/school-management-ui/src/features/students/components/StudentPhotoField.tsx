import { useRef, useState } from "react";
import { Camera, Trash2 } from "lucide-react";
import { studentService } from "../services/studentService";
import { useStudentPhoto } from "../hooks/useStudentPhoto";
import { useToast } from "@/components/Toast";
import { getApiErrors } from "@/utils/apiError";
import { describeSize, detectFileType } from "@/utils/file";
import { initialsOf } from "@/utils/initials";

/** Same limit as the API (StudentPhotoService.MaxBytes). The API checks again. */
const MAX_PHOTO_BYTES = 2 * 1024 * 1024;

/**
 * The student's photo: shows it (or the initials) with Upload / Change / Remove. A change is saved
 * at once, separately from the form's Save button, because the picture is a file, not a field.
 * The browser refuses a wrong or oversized file before sending it; the API repeats the checks.
 */
export function StudentPhotoField({
  studentId,
  studentName,
  hasPhoto: initialHasPhoto,
}: {
  studentId: number;
  studentName: string;
  hasPhoto: boolean;
}) {
  const toast = useToast();
  const input = useRef<HTMLInputElement>(null);
  const [hasPhoto, setHasPhoto] = useState(initialHasPhoto);
  const [version, setVersion] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const src = useStudentPhoto(studentId, hasPhoto, version);

  const choose = async (file: File | undefined) => {
    if (!file) return;
    setError(null);
    if (file.size > MAX_PHOTO_BYTES) {
      setError(
        `The photo is ${describeSize(file.size)}. The largest photo allowed is ${describeSize(MAX_PHOTO_BYTES)}.`,
      );
      return;
    }
    const type = await detectFileType(file);
    if (type !== "image/jpeg" && type !== "image/png") {
      setError("A photo must be a JPG or PNG picture.");
      return;
    }
    setBusy(true);
    try {
      await studentService.setPhoto(studentId, file);
      setHasPhoto(true);
      setVersion((v) => v + 1);
      toast.success("Photo saved.");
    } catch (err) {
      setError(getApiErrors(err, "Could not save the photo.")[0]);
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    setError(null);
    setBusy(true);
    try {
      await studentService.removePhoto(studentId);
      setHasPhoto(false);
      toast.success("Photo removed.");
    } catch (err) {
      setError(getApiErrors(err, "Could not remove the photo.")[0]);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="field photo-field">
      <span className="field__label">Photo</span>
      <div className="photo-field__row">
        {src ? (
          <img className="photo-field__image" src={src} alt="" />
        ) : (
          <span className="avatar photo-field__image" aria-hidden="true">
            {initialsOf(studentName)}
          </span>
        )}
        <div className="photo-field__actions">
          <input
            ref={input}
            type="file"
            accept="image/jpeg,image/png"
            hidden
            onChange={(e) => {
              void choose(e.target.files?.[0]);
              e.target.value = "";
            }}
          />
          <button
            type="button"
            className="btn btn--secondary btn--sm"
            disabled={busy}
            onClick={() => input.current?.click()}
          >
            <Camera size={16} aria-hidden="true" />
            {hasPhoto ? "Change photo" : "Upload photo"}
          </button>
          {hasPhoto && (
            <button
              type="button"
              className="btn btn--secondary btn--sm"
              disabled={busy}
              onClick={() => void remove()}
            >
              <Trash2 size={16} aria-hidden="true" />
              Remove
            </button>
          )}
          <p className="field-note">
            JPG or PNG, up to 2 MB. Saved as soon as you choose it.
          </p>
          {error && (
            <p className="field__error" role="alert">
              {error}
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
