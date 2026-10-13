using FluentValidation;
using SchoolManagement.Application.DTOs.Documents;

namespace SchoolManagement.Application.Validators;

/// <summary>
/// G1b: the form fields of an upload. The file itself (size, real type, duplicate) and the rules
/// that need the DocumentTypes table (active? needs an expiry date?) are checked by DocumentService.
/// </summary>
public class UploadDocumentRequestValidator : AbstractValidator<UploadDocumentRequest>
{
    public UploadDocumentRequestValidator()
    {
        RuleFor(x => x.DocumentTypeId).GreaterThan(0).WithMessage("Choose a document type from the list.");
        RuleFor(x => x.Title).MaximumLength(120);
    }
}

public class ReviewDocumentRequestValidator : AbstractValidator<ReviewDocumentRequest>
{
    public ReviewDocumentRequestValidator()
    {
        RuleFor(x => x.RowVersion).Must(RowVersionRules.IsValid).WithMessage(RowVersionRules.Message);
    }
}

public class RejectDocumentRequestValidator : AbstractValidator<RejectDocumentRequest>
{
    public RejectDocumentRequestValidator()
    {
        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Give the reason for rejecting this document.")
            .MaximumLength(300);
        RuleFor(x => x.RowVersion).Must(RowVersionRules.IsValid).WithMessage(RowVersionRules.Message);
    }
}
