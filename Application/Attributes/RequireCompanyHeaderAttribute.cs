using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Application.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireCompanyHeaderAttribute : ActionFilterAttribute
{
    private readonly string _headerName;

    public RequireCompanyHeaderAttribute(string headerName = "X-Company-ID")
    {
        _headerName = headerName;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // Check if the header exists
        if (!context.HttpContext.Request.Headers.TryGetValue(_headerName, out var companyId) || string.IsNullOrWhiteSpace(companyId))
        {
            // If not, return a 400 Bad Request response
            context.Result = new BadRequestObjectResult(new
            {
                Error = $"Missing or invalid '{_headerName}' header."
            });
        }

        base.OnActionExecuting(context);
    }
}
