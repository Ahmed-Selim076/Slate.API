using System.ComponentModel.DataAnnotations;
using Slate.Api.Models;

namespace Slate.Api.DTOs;

public record CreateBoardRequest(
    [Required, MinLength(1), MaxLength(120)] string Title
);

public record UpdateBoardTitleRequest(
    [Required, MinLength(1), MaxLength(120)] string Title
);

public record UpdateBoardElementsRequest(
    [Required] string ElementsJson
);

public record UpdateMemberRoleRequest(
    [Required] BoardRole Role
);

public record BoardSummaryResponse(
    Guid Id,
    string Title,
    DateTime LastActiveAt
);

public record BoardDetailResponse(
    Guid Id,
    string Title,
    string ElementsJson,
    DateTime CreatedAt,
    DateTime LastActiveAt,
    Guid OwnerId,
    BoardRole MyRole
);

public record BoardMemberResponse(
    Guid UserId,
    string DisplayName,
    string Email,
    BoardRole Role
);

