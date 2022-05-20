namespace Template.Web.Infrastructure.Filters;

using Microsoft.AspNetCore.Mvc.Filters;

public sealed class ApiExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        context.Result = new ObjectResult(new
        {
            Code = 500,
            Message = "A server error occurred.",
        })
        {
            StatusCode = 500
        };
    }
}
