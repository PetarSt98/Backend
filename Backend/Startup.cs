using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using NetCoreOidcExample.Helpers;
using Microsoft.AspNetCore.Mvc.NewtonsoftJson;
using NetCoreOidcExample.Helpers;
using NetCoreOidcExample.Models;
using Backend.Controllers;
namespace Backend
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            StaticConfig = configuration;
        }

        public static IConfiguration StaticConfig { get; private set; }
        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            RegisterDependencies(services);
            services.AddScoped<AuthorizeGroupAttribute>();
            services.AddSingleton(Configuration);
            services.AddSwaggerGen(c => {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Remote Desktop Service",
                    Version = "v1",
                    Description = "Remote Desktop Service Backend",
                });
                c.EnableAnnotations();
                c.AddSecurityDefinition("bearer", new OpenApiSecurityScheme()
                {
                    In = ParameterLocation.Header,
                    BearerFormat = "JWT",
                    Scheme = "bearer",
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        Password = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl = new Uri("https://auth.cern.ch/auth/realms/cern/protocol/openid-connect/auth"),
                            TokenUrl = new Uri("https://auth.cern.ch/auth/realms/cern/protocol/openid-connect/token")
                        }
                    },
                });
                c.OperationFilter<HeaderFilter>();
            });

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", builder =>
                    builder.WithOrigins(
                        "http://localhost:3000",
                        "https://remote-desktop-gateway-test.app.cern.ch"
                        ) 
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                );
            });

            services.AddControllers();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseRouting();

            app.UseCors("CorsPolicy");

            app.UseSwagger();
            app.UseSwaggerUI(c => {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "JWT validation");
                c.RoutePrefix = string.Empty;
                c.OAuthClientId(Configuration["AppSettings:ClientID"]);
                c.OAuthRealm(Configuration["AppSettings:Issuer"]);
                c.OAuthAppName("JWTVerificationExample");
                c.OAuthClientSecret(Configuration["AppSettings:ClientSecret"]);
            });
            app.UseMiddleware<JwtMiddleware>();
            app.UseEndpoints(endpoints => {
                endpoints.MapDefaultControllerRoute();
            });
        }

        private void RegisterDependencies(IServiceCollection services)
        {
            services.AddScoped<IUserService, DeviceSearcher>();
            services.AddSingleton(Configuration);
        }
    }
}
