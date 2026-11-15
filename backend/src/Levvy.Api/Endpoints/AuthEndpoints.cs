using FluentValidation;
using Levvy.Application.Commands;
using Levvy.Application.Common.Interfaces;
using MediatR;

namespace Levvy.Api.Endpoints;

/// <summary>
/// Minimal API route group for /auth (email/password + Google Sign-In/Sign-Up + current user).
/// </summary>
public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", Register).WithSummary("Register a new Levvy account with email and password");
        group.MapPost("/login", Login).WithSummary("Log in with email and password");
        group.MapPost("/google", GoogleAuth).WithSummary("Sign in or sign up using a Google ID token");
        group.MapGet("/config", (IConfiguration cfg) =>
            Results.Ok(new { googleClientId = cfg["Google:ClientId"] ?? cfg["GOOGLE_CLIENT_ID"] ?? "" }))
            .WithSummary("Get public auth configuration (Google OAuth client ID)");
        group.MapGet("/me", GetMe).WithSummary("Get the currently authenticated user's profile");

        return group;
    }

    private static async Task<IResult> Register(
        RegisterUserRequest req,
        ISender sender,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(new RegisterUserCommand(
                req.FirstName,
                req.LastName,
                req.Email,
                req.Password,
                req.Role,
                req.Institution,
                req.Department,
                req.PreferredLanguage,
                req.AccessibilityMode,
                req.CaptionFontSize), ct);

            if (result.IsFailure)
                return Results.Conflict(new { error = result.Error.Description, code = result.Error.Code });

            return Results.Created($"/users/{result.Value.User.Id}/dashboard", result.Value);
        }
        catch (ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> Login(
        LoginUserRequest req,
        ISender sender,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(new LoginUserCommand(req.Email, req.Password), ct);
            if (result.IsFailure)
                return Results.Json(new { error = result.Error.Description, code = result.Error.Code }, statusCode: StatusCodes.Status401Unauthorized);

            return Results.Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GoogleAuth(
        GoogleAuthRequest req,
        ISender sender,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(new GoogleAuthCommand(
                req.IdToken,
                req.Role,
                req.Institution,
                req.Department,
                req.PreferredLanguage,
                req.AccessibilityMode), ct);

            if (result.IsFailure)
                return Results.Json(new { error = result.Error.Description, code = result.Error.Code }, statusCode: StatusCodes.Status401Unauthorized);

            return Results.Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetMe(
        HttpContext http,
        IJwtTokenGenerator jwt,
        IUserRepository users,
        CancellationToken ct)
    {
        var userId = HttpUserResolver.ResolveCurrentUserId(http, jwt);
        if (userId is null)
            return Results.Unauthorized();

        var user = await users.GetByIdAsync(userId.Value, ct);
        if (user is null)
            return Results.NotFound(new { error = "User account was not found." });

        return Results.Ok(UserProfileDto.FromEntity(user));
    }
}
