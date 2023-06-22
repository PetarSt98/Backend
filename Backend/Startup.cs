using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using NetCoreOidcExample.Helpers;
using Backend.ExchangeTokenService;
using Backend.Modules.AccessValidation;
using Backend.Modules.ActiveDirectory;
using Backend.Modules.RequestValidation;
using Backend.Helpers;
using Microsoft.AspNetCore.Mvc.NewtonsoftJson;

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

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            RegisterDependencies(services);
            ConfigureCors(services);
            ConfigureSwagger(services);
            services.AddControllers().AddNewtonsoftJson();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();
            app.UseCors("CorsPolicy");
            app.UseSwagger();
            app.UseSwaggerUI(cfg => {
                cfg.SwaggerEndpoint("/swagger/v1/swagger.json", "Remote Desktop Gateway API");
                cfg.RoutePrefix = "";
                cfg.OAuthClientId(Configuration["AppSettings:ClientID"]);
                cfg.OAuthRealm(Configuration["AppSettings:Issuer"]);
                cfg.OAuthClientSecret(Configuration["AppSettings:Secret"]);
            });
            app.UseAuthorization();
            app.UseMiddleware<JwtMiddleware>();

            app.UseEndpoints(endpoints => {
                endpoints.MapControllers();
            });
        }

        private void RegisterDependencies(IServiceCollection services)
        {
            services.AddScoped<IActiveDirectoryProxy, ActiveDirectoryProxy>();
            services.AddScoped<IRequestValidator, RequestValidator>();
            services.AddScoped<WorkerAuthorizeAttribute>();
            services.AddSingleton(Configuration);
        }

        private void ConfigureCors(IServiceCollection services)
        {
            services.AddCors(options => {
                options.AddPolicy("CorsPolicy", builder => {
                    builder.WithOrigins(
                        "https://rds-front-rds-frontend.app.cern.ch/",
                        "https://rds-back-new-rds-frontend.app.cern.ch/"
                    )
                    .AllowAnyHeader()
                    .AllowAnyMethod();
                });
            });

        }

        private void ConfigureSwagger(IServiceCollection services)
        {
            services.AddSwaggerGen(cfg => {
                cfg.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Remote Desktop Gateway API",
                    Version = "v1",
                    Description = "REST API for DB access as well as policy validation.",
                    Contact = new OpenApiContact
                    {
                        Name = "Petar Stojkovic",
                        Email = "petar.stojkovic@cern.ch"
                    }
                });
                cfg.EnableAnnotations();
                cfg.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
                {
                    In = ParameterLocation.Header,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        Password = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl =
                                new Uri("https://auth.cern.ch/auth/realms/cern/protocol/openid-connect/auth"),
                            TokenUrl = new Uri("https://auth.cern.ch/auth/realms/cern/protocol/openid-connect/token")
                        }
                    }
                });
                cfg.OperationFilter<HeaderFilter>();
            });
        }
    }
}
