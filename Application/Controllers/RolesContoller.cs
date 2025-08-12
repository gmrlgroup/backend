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
    public class RolesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;


        public RolesController(ApplicationDbContext context, 
                                UserManager<ApplicationUser> userManager,
                                RoleManager<IdentityRole> roleManager
                                )
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // GET: api/IdentityRoles
        [HttpGet]
        public async Task<ActionResult<Response<List<IdentityRole>>>> GetIdentityRoles()
        {
            // get roleId from header
            var userId = Request.Headers["UserId"];

            // get company from header
            var companyId = Request.Headers["X-Company-ID"];


            var identityRoles = await _roleManager.Roles.ToListAsync();

            var roles = identityRoles.Where(r => r.Name.StartsWith(companyId)).ToList();

            var response = new Response<List<IdentityRole>>
            {
                TotalItems = roles.Count(),
                Items = roles,
                DataState = null,
                Status = ResponseStatus.Success
            };


            return Ok(response);
        }




    }
}
