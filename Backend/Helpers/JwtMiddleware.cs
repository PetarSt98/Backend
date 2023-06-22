using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Backend.Models;

namespace Backend.Helpers {
    public class JwtMiddleware {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly ILogger<JwtMiddleware> _logger;

        public JwtMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<JwtMiddleware> logger) {
            _next = next;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context) {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            if (token != null)
                AttachUserToContext(context, token);

            await _next(context);
        }

        private void AttachUserToContext(HttpContext context, string token) {
            try {
                string issuer = _configuration["AppSettings:Issuer"];
                string openIdConfigAddress = $"{issuer}/.well-known/openid-configuration";
                IConfigurationManager<OpenIdConnectConfiguration> configurationManager =
                    new ConfigurationManager<OpenIdConnectConfiguration>(openIdConfigAddress,
                        new OpenIdConnectConfigurationRetriever());
                OpenIdConnectConfiguration openIdConfig =
                    configurationManager.GetConfigurationAsync(CancellationToken.None).Result;
                var tokenHandler = new JwtSecurityTokenHandler();
                TokenValidationParameters tvp = GetTokenValidationParameters(issuer, openIdConfig);
                tokenHandler.ValidateToken(token, tvp, out SecurityToken validatedToken);
                var jwtToken = (JwtSecurityToken)validatedToken;
                context.Items["User"] = CreateUserFromToken(jwtToken);
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed validating token.");
            }
        }

        private TokenValidationParameters GetTokenValidationParameters(string issuer, OpenIdConnectConfiguration openIdConfig) {
            return new TokenValidationParameters {
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidAudience = _configuration["AppSettings:ClientID"],
                ValidIssuer = issuer,
                IssuerSigningKeys = openIdConfig.SigningKeys
            };
        }

        private User CreateUserFromToken(JwtSecurityToken jwtToken) {
            var roles = GetRolesFrom(jwtToken);
            if (roles.Contains(_configuration["AppSettings:WorkerGroup"]))
                return new User(roles);
            var accountName = GetValueFromToken("cern_upn", jwtToken);
            var name = GetValueFromToken("name", jwtToken);
            var email = GetValueFromToken("email", jwtToken);
            return new User(accountName, name, email);
        }

        private List<string> GetRolesFrom(JwtSecurityToken jwtToken) {
            return jwtToken.Claims.Where(claim => claim.Type == "cern_roles").Select(claim => claim.Value).ToList();
        }

        private string GetValueFromToken(string key, JwtSecurityToken jwtToken) {
            return jwtToken.Claims.First(x => x.Type == key).Value;
        }
    }
}
