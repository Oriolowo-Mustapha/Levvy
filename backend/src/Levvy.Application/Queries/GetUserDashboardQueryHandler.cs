using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Abstractions;
using MediatR;

namespace Levvy.Application.Queries;

public sealed class GetUserDashboardQueryHandler(IUserRepository users)
    : IRequestHandler<GetUserDashboardQuery, Result<UserDashboardDto>>
{
    public async Task<Result<UserDashboardDto>> Handle(GetUserDashboardQuery request, CancellationToken ct)
    {
        var dashboard = await users.GetDashboardAsync(request.UserId, ct);
        if (dashboard is null)
        {
            return Result.Failure<UserDashboardDto>(
                new Error("User.NotFound", "User account was not found."));
        }

        return Result.Success(dashboard);
    }
}
