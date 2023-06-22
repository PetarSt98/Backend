using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Backend.Models;

namespace Backend.Helpers {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class WorkerAuthorizeAttribute : Attribute, IAuthorizationFilter {
        private readonly IConfiguration _configuration;

        public WorkerAuthorizeAttribute(IConfiguration configuration) {
            _configuration = configuration;
        }
        
        public void OnAuthorization(AuthorizationFilterContext context) {
            var user = (User)context.HttpContext.Items["User"];
            if (user == null || !IsWorker(user))
                context.Result = new JsonResult(new { message = "Unauthorized" }) { StatusCode = StatusCodes.Status401Unauthorized };
        }

        private bool IsWorker(User user) {
            return user.Roles.Contains(_configuration["AppSettings:WorkerGroup"]);
        }
    }
}
