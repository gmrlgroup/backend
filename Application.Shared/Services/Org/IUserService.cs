using Application.Shared.Models;
using Application.Shared.Models.User;

namespace Application.Shared.Services.Org
{
    public interface IUserService
    {
        Task<List<ApplicationUser>> GetUsers(string userId);

        Task<ApplicationUser> GetUser(string id);

        Task<ApplicationUser> GetUserByEmail(string email);

        Task<ApplicationUser> UpdateUserAsync(ApplicationUser user);

        Task<ApplicationUser> DeleteUserAsync(string id);

        Task<ApplicationUser> RegisterUser(UserInputModel userInput, string companyId);

        Task<List<string>> GetUseremails(string companyId);

    }
}
