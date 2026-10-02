using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrendRadar.Application.Abstractions;
using TrendRadar.Application.Auth;

namespace TrendRadar.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = Policies.Owner)]
public sealed class UsersController(AuthService auth, IUserStore users) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<UserDto>> List(CancellationToken ct) =>
        (await users.ListAsync(ct)).Select(UserDto.From);

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var result = await auth.CreateUserAsync(
            User.GetUserId(), request.Email, request.DisplayName, request.Password, request.Role, ct);
        return result.User is { } user
            ? StatusCode(StatusCodes.Status201Created, UserDto.From(user))
            : Problem(title: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }
}
