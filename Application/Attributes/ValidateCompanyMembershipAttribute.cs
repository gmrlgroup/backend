using Application.Shared.Services.Org;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Threading.Tasks;


namespace Application.Attributes;


[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ValidateCompanyMembershipAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var headers = context.HttpContext.Request.Headers;

        if (!headers.ContainsKey("X-Company-ID") || string.IsNullOrEmpty(headers["X-Company-ID"]))
        {
            context.Result = new BadRequestObjectResult("Company should be in the header");
            return;
        }

        if (!headers.ContainsKey("userId") || string.IsNullOrEmpty(headers["userId"]))
        {
            context.Result = new BadRequestObjectResult("User should be in the header");
            return;
        }

        var companyId = headers["X-Company-ID"].ToString();
        var userId = headers["userId"].ToString();

        // Resolve the service dynamically to allow DI
        var companyService = context.HttpContext.RequestServices.GetService(typeof(ICompanyService)) as ICompanyService;
        
        if (companyService == null)
        {
            context.Result = new StatusCodeResult(500); // Internal server error if service is missing
            return;
        }

        bool userIsCompanyMember = await companyService.UserIsCompanyMember(userId, companyId);

        if (!userIsCompanyMember)
        {
            context.Result = new UnauthorizedObjectResult($"You are not a member of the company {companyId}");
            return;
        }

        // Proceed to the next action
        await next();
    }
}
