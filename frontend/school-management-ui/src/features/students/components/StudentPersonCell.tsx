import { PersonCell } from "@/components/PersonCell";
import { useStudentPhoto } from "../hooks/useStudentPhoto";

/** A student in a list row: their photo when they have one, otherwise the initials. */
export function StudentPersonCell({
  studentId,
  hasPhoto,
  name,
  roll,
}: {
  studentId: number;
  hasPhoto: boolean;
  name: string;
  roll: string;
}) {
  const photoSrc = useStudentPhoto(studentId, hasPhoto);
  return (
    <PersonCell
      name={name}
      to={`/students/${studentId}`}
      sub={`Roll ${roll}`}
      photoSrc={photoSrc}
    />
  );
}
