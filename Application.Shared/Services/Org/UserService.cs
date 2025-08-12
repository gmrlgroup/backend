using Application.Shared.Data;
using Application.Shared.Models;
using Application.Shared.Models.User;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using System.Text;

namespace Application.Shared.Services.Org
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly ICompanyService _companyService;

        public UserService(ApplicationDbContext context,
                            UserManager<ApplicationUser> userManager,
                            IUserStore<ApplicationUser> userStore,
                            ICompanyService companyService)
        {
            _context = context;
            _userManager = userManager;
            _userStore = userStore;
            _companyService = companyService;
        }


        public async Task<List<ApplicationUser>> GetUsers(string companyId)
        {
            var applicationUserMembersList = await _context.CompanyMember.Where(m => m.CompanyId == companyId).ToListAsync();
            var applicationUserMembers = applicationUserMembersList.Select(m => m.ApplicationUserId).ToArray();

            return await _context.ApplicationUser.Where(c => applicationUserMembers.Contains(c.Id)).ToListAsync();
        }


        public async Task<ApplicationUser> GetUser(string id)
        {
            return await _context.ApplicationUser.FindAsync(id);
        }

        public async Task<ApplicationUser> GetUserByEmail(string email)
        {
            return await _userManager.FindByEmailAsync(email);
        }

        public async Task<List<string>> GetUseremails(string companyId)
        {
            var members = await _context.CompanyMember.Where(m => m.CompanyId == companyId).ToListAsync();

            var userIds = members.Select(m => m.ApplicationUserId).ToArray();

            var users = await _context.ApplicationUser.Where(u => userIds.Contains(u.Id)).ToListAsync();

            return users.Select(u => u.Email).ToList();
        }



        private IEnumerable<IdentityError>? identityErrors;

        public async Task<ApplicationUser> RegisterUser(UserInputModel userInput, string companyId)
        {
            var user = CreateUser();
            user.EmailConfirmed = true;

            await _userStore.SetUserNameAsync(user, userInput.UserName, CancellationToken.None);
            var emailStore = GetEmailStore();
            await emailStore.SetEmailAsync(user, userInput.Email, CancellationToken.None);
            var result = await _userManager.CreateAsync(user, userInput.Password);

            if (!result.Succeeded)
            {
                identityErrors = result.Errors;

                Console.WriteLine("Error: " + result.Errors);

                return null;
            }


            // add the user as a member to the company
            await _companyService.AddCompanyMember(companyId, user.Id);


            var userId = await _userManager.GetUserIdAsync(user);

            user.Id = userId;


            return user;


        }


        // Update
        public async Task<ApplicationUser> UpdateUserAsync(ApplicationUser user)
        {
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                return user;
            }

            return null;
        }

        // Delete
        public async Task<ApplicationUser> DeleteUserAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return null;
            }

            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                return user;
            }

            return null;
        }

        // change password
        public async Task<ApplicationUser> ChangePasswordAsync(string id, string currentPassword, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return null;
            }

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

            if (result.Succeeded)
            {
                return user;
            }

            return null;
        }


        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }


        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                    $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor.");
            }
        }


    }
}
