using FluentValidation;
using Levvy.Application.Commands;
using Levvy.Application.Common.Interfaces;
using Levvy.Application.Queries;
using MediatR;

namespace Levvy.Api.Endpoints;

/// <summary>
/// Minimal API route group for /users (dashboard metrics, session history, study packs, and profile settings).
/// </summary>
public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/users").WithTags("Users & Dashboard");

        group.MapGet("/me/dashboard", GetMyDashboard).WithSummary("Get dashboard for the currently authenticated user");
        group.MapGet("/{id:guid}/dashboard", GetDashboardById).WithSummary("Get user dashboard by user ID");
        group.MapPut("/me/profile", UpdateMyProfile).WithSummary("Update profile and accessibility settings for the current user");
        group.MapPut("/{id:guid}/profile", UpdateProfileById).WithSummary("Update profile and accessibility settings by user ID");

        return group;
    }

    private static Task<IResult> GetMyDashboard(
        HttpContext http,
        IJwtTokenGenerator jwt,
        ISender sender,
        CancellationToken ct)
        => FetchDashboardAsync(HttpUserResolver.ResolveCurrentUserId(http, jwt), sender, ct);

    private static Task<IResult> GetDashboardById(
        Guid id,
        HttpContext http,
        IJwtTokenGenerator jwt,
        ISender sender,
        CancellationToken ct)
    {
        var bearerUserId = HttpUserResolver.TryResolveFromBearer(http, jwt);
        if (bearerUserId is not null && bearerUserId != id)
            return Task.FromResult(Results.Forbid());

        return FetchDashboardAsync(id, sender, ct);
    }

    private static Task<IResult> UpdateMyProfile(
        UpdateUserProfileRequest req,
        HttpContext http,
        IJwtTokenGenerator jwt,
        ISender sender,
        CancellationToken ct)
        => ApplyProfileUpdateAsync(HttpUserResolver.ResolveCurrentUserId(http, jwt), req, sender, ct);

    private static Task<IResult> UpdateProfileById(
        Guid id,
        UpdateUserProfileRequest req,
        HttpContext http,
        IJwtTokenGenerator jwt,
        ISender sender,
        CancellationToken ct)
    {
        var bearerUserId = HttpUserResolver.TryResolveFromBearer(http, jwt);
        if (bearerUserId is not null && bearerUserId != id)
            return Task.FromResult(Results.Forbid());

        return ApplyProfileUpdateAsync(id, req, sender, ct);
    }

    private static async Task<IResult> FetchDashboardAsync(Guid? userId, ISender sender, CancellationToken ct)
    {
        if (userId is null)
            return Results.Unauthorized();

        var result = await sender.Send(new GetUserDashboardQuery(userId.Value), ct);
        if (result.IsFailure)
            return Results.NotFound(new { error = result.Error.Description, code = result.Error.Code });

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> ApplyProfileUpdateAsync(
        Guid? userId,
        UpdateUserProfileRequest req,
        ISender sender,
        CancellationToken ct)
    {
        if (userId is null)
            return Results.Unauthorized();

        try
        {
            var result = await sender.Send(new UpdateUserProfileCommand(
                userId.Value,
                req.FirstName,
                req.LastName,
                req.Role,
                req.Institution,
                req.Department,
                req.PreferredLanguage,
                req.AccessibilityMode,
                req.CaptionFontSize,
                req.AvatarUrl), ct);

            if (result.IsFailure)
                return Results.NotFound(new { error = result.Error.Description, code = result.Error.Code });

            return Results.Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
