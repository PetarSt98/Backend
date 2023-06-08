using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace NetCoreOidcExample.Helpers
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AuthorizeGroupAttribute : Attribute, IAuthorizationFilter
    {
        private readonly IConfiguration _configuration;

        public AuthorizeGroupAttribute(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = (User)context.HttpContext.Items["User"];
            if (user == null || !IsAdmin(user))
                context.Result = new JsonResult(new { message = "Unauthorized" }) { StatusCode = StatusCodes.Status401Unauthorized };
        }

        private bool IsAdmin(User user)
        {
            return user.Roles.Contains(_configuration["AppSettings:AdminGroup"]);
        }
    }
}
