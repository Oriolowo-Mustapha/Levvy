using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Abstractions;
using MediatR;

namespace Levvy.Application.Queries;

public sealed record GetUserDashboardQuery(Guid UserId) : IRequest<Result<UserDashboardDto>>;
