using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Application.Shared.Data;
using Application.Shared.Models;
using Application.Shared.Models.User;
using Microsoft.AspNetCore.Identity;
using Application.Attributes;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Application.Shared.Services.Org;
using Application.Models;

namespace Application.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [RequireCompanyHeader]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;


        public UsersController(ApplicationDbContext context, 
                                IUserService userService, 
                                UserManager<ApplicationUser> userManager,
                                RoleManager<IdentityRole> roleManager
                                )
        {
            _context = context;
            _userService = userService;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // GET: api/ApplicationUsers
        [HttpGet]
        public async Task<ActionResult<Response<List<ApplicationUser>>>> GetApplicationUsers()
        {
            // get userId from header
            var userId = Request.Headers["UserId"];

            // get company from header
            var companyId = Request.Headers["X-Company-ID"];


            var applicationUsers = await _userService.GetUsers(companyId);

            var response = new Response<List<ApplicationUser>>
            {
                TotalItems = applicationUsers.Count(),
                Items = applicationUsers,
                DataState = null,
                Status = ResponseStatus.Success
            };


            return Ok(response);
        }


        // GET: api/users/roles
        [HttpGet("roles")]
        public async Task<ActionResult<IList<string>>> GetCurrentUserRoles()
        {
            // get userId from header
            var userId = Request.Headers["UserId"];

            // get company from header
            var companyId = Request.Headers["X-Company-ID"];


            var user = await _userManager.FindByIdAsync(userId);

            var roles = await _userManager.GetRolesAsync(user);


            roles = roles.Where(r => r.StartsWith(companyId)).ToList();

            return Ok(roles);
        }

        // GET: api/users//{id}/roles
        [HttpGet("{Id}/roles")]
        public async Task<ActionResult<IList<string>>> GetApplicationUserRoles(string Id)
        {
            // get userId from header
            var userId = Request.Headers["UserId"];

            // get company from header
            var companyId = Request.Headers["X-Company-ID"];


            var user = await _userManager.FindByIdAsync(Id);

            var roles = await _userManager.GetRolesAsync(user);


            roles = roles.Where(r => r.StartsWith(companyId)).ToList();

            return Ok(roles);
        }


        // GET: api/ApplicationUsers
        [HttpGet("{id}")]
        public async Task<ActionResult<ApplicationUser>> GetApplicationUser(string id)
        {
            // get userId from header
            var userId = Request.Headers["UserId"];

            // get company from header
            var companyId = Request.Headers["X-Company-ID"];


            var applicationUsers = await _userService.GetUser(id);

            return Ok(applicationUsers);
        }


        // GET: api/ApplicationUsers
        [HttpGet("emails")]
        public async Task<ActionResult<List<string>>> GetUserEmails()
        {
            // get userId from header
            var userId = Request.Headers["UserId"];

            // get company from header
            var companyId = Request.Headers["X-Company-ID"];


            var emails = await _userService.GetUseremails(companyId);

            return Ok(emails);
        }



        //POST: api/users
        //To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<ApplicationUser>> PostApplicationUser(UserInputModel userInput)
        {
            //_context.ApplicationUser.Add(applicationUser);
            ApplicationUser user = new ApplicationUser();

            // get userId from header
            var userId = Request.Headers["UserId"];

            // get company from header
            var companyId = Request.Headers["X-Company-ID"];

            if(!String.IsNullOrEmpty(companyId))
            {
                user = await _userService.RegisterUser(userInput, companyId);
            }

            
            //try
            //{
            //    //await _context.SaveChangesAsync();
            //    user = await _userService.RegisterUser(userInput);
            //}
            //catch (DbUpdateException)
            //{
            //    if (ApplicationUserExists(user.Id))
            //    {
            //        return Conflict();
            //    }
            //    else
            //    {
            //        throw;
            //    }
            //}

            return CreatedAtAction("GetApplicationUser", new { id = user.Id }, user);
        }

        // PUT: api/ApplicationUsers/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutApplicationUser(string id, UserInputModel userInput)
        {

            var user = await _userManager.FindByIdAsync(id);

            user.Email = userInput.Email;
            user.UserName = userInput.UserName;

            var userRoles = await _userManager.GetRolesAsync(user);

            // get deleted roles
            var deletedRoles = userRoles.Where(r => !userInput.Roles.Contains(r)).ToList();

            _context.Entry(user).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            

            foreach (var role in userInput.Roles)
            {
                if (!userRoles.Contains(role))
                {
                    await _userManager.AddToRoleAsync(user, role);
                }
            }

            await _context.SaveChangesAsync();

            await _userManager.RemoveFromRolesAsync(user, deletedRoles);

            await _context.SaveChangesAsync();


            return NoContent();
        }


        [HttpPut("{id}/resetPassword")]
        public async Task<IActionResult> ResetPasword(string id, UserInputModel userInput)
        {

            var user = await _userManager.FindByIdAsync(id);

            await _userManager.ChangePasswordAsync(user, userInput.Password, userInput.ConfirmPassword);

            //user.Email = userInput.Email;
            //user.UserName = userInput.UserName;

            //var userRoles = await _userManager.GetRolesAsync(user);

            // get deleted roles
            //var deletedRoles = userRoles.Where(r => !userInput.Roles.Contains(r)).ToList();

            //_context.Entry(user).State = EntityState.Modified;

            //await _context.SaveChangesAsync();



            //foreach (var role in userInput.Roles)
            //{
            //    if (!userRoles.Contains(role))
            //    {
            //        await _userManager.AddToRoleAsync(user, role);
            //    }
            //}

            //await _context.SaveChangesAsync();

            //await _userManager.RemoveFromRolesAsync(user, deletedRoles);

            //await _context.SaveChangesAsync();


            return NoContent();
        }


    }
}
