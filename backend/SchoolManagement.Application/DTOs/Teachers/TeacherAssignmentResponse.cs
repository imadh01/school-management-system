namespace SchoolManagement.Application.DTOs.Teachers;

public record TeacherSubjectResponse(int SubjectId, string Name, string Code);

public record TeacherAssignmentResponse(
    int ClassSectionId, string ClassSectionName, string AcademicYearName,
    bool IsClassTeacher, List<TeacherSubjectResponse> Subjects);