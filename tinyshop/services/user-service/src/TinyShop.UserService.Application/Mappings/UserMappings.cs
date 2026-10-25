using TinyShop.UserService.Application.Models;
using TinyShop.UserService.Domain.Entities;

namespace TinyShop.UserService.Application.Mappings;

internal static class UserMappings
{
    public static UserDto ToDto(this User user) => new(user.Id, user.Name);
}
