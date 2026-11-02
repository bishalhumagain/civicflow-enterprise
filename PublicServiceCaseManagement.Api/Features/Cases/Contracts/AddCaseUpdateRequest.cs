using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Infrastructure.Validation;

namespace PublicServiceCaseManagement.Api.Features.Cases.Contracts;

/// <summary>Adds a public update, information request, or internal note to an authorized case.</summary>
public sealed class AddCaseUpdateRequest
{
    /// <summary>Update category; internal notes are not visible to citizens.</summary>
    [EnumDataType(typeof(CaseUpdateKind))]
    public CaseUpdateKind Kind { get; init; }

    /// <summary>Required non-empty message, subject to the case-update length limit.</summary>
    [Required]
    [NotWhiteSpace]
    [TrimmedStringLength(
        CaseUpdate.MaximumMessageLength,
        MinimumLength = 1)]
    public string Message { get; init; } = string.Empty;
}