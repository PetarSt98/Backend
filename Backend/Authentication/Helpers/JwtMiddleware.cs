using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NetCoreOidcExample.Models;

namespace NetCoreOidcExample.Helpers {
    public class JwtMiddleware {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;


        public JwtMiddleware(RequestDelegate next, IConfiguration config) {
            _next = next;
            _configuration = config;
        }

        public async Task Invoke(HttpContext context) {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            if (token != null)
                AttachUserToContext(context, token);

            await _next(context);
        }

        private void AttachUserToContext(HttpContext context, string token) {
            try {
                var iss = _configuration["AppSettings:Issuer"];
                IConfigurationManager<OpenIdConnectConfiguration> configurationManager =
                    new ConfigurationManager<OpenIdConnectConfiguration>(
                        $"{iss}/.well-known/openid-configuration", new OpenIdConnectConfigurationRetriever());
                OpenIdConnectConfiguration openIdConfig =
                    configurationManager.GetConfigurationAsync(CancellationToken.None).Result;
                var tokenHandler = new JwtSecurityTokenHandler();
                TokenValidationParameters tvp = GetTokenValidationParameters(iss, openIdConfig);
                tokenHandler.ValidateToken(token, tvp, out SecurityToken validatedToken);
                var jwtToken = (JwtSecurityToken)validatedToken;
                context.Items["User"] = CreateUserFromToken(jwtToken);
            } catch (Exception) {
                // ignored
            }
        }

        private TokenValidationParameters GetTokenValidationParameters(string issuer, OpenIdConnectConfiguration openIdConfig) {
            return new TokenValidationParameters {
                ValidateAudience = true,
                ValidAudience = _configuration["AppSettings:ClientID"],
                ValidIssuer = issuer,
                ValidateIssuer = true,
                IssuerSigningKeys = openIdConfig.SigningKeys,
            };
        }

        private static User CreateUserFromToken(JwtSecurityToken jwtToken) {
            var accountName = GetValueFromToken("cern_upn", jwtToken);
            var name = GetValueFromToken("name", jwtToken);
            var roles = jwtToken.Claims.Where(x => x.Type == "cern_roles").Select(x => x.Value).ToList();

            return new User {
                AccountName = accountName,
                Name = name,
                Roles = roles
            };
        }

        private static string GetValueFromToken(string key, JwtSecurityToken token) {
            return token.Claims.First(x => x.Type == key).Value;
        }
    }
}
