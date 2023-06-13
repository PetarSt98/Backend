using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Backend.Controllers; // If IUserService and UserSearcher are in this namespace
using Microsoft.IdentityModel.Tokens;
// using SynchronizerLibrary;  // Uncomment if IUserService and UserSearcher are in this namespace
using NetCoreOidcExample.Helpers;
namespace Backend
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddScoped<AuthorizeGroupAttribute>();
            // Add this code to register JWT authentication schema
            services.AddAuthentication("Bearer")
                .AddJwtBearer("Bearer", options =>
                {
                    options.Authority = "https://auth.cern.ch/auth/realms/cern";
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateAudience = false
                    };
                });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Backend", Version = "v1" });

                // Add this code to integrate JWT in Swagger UI
                c.AddSecurityDefinition("bearer", new OpenApiSecurityScheme()
                {
                    In = ParameterLocation.Header,
                    BearerFormat = "JWT",
                    Scheme = "bearer",
                    Type = SecuritySchemeType.Http,
                    Description = "JWT Authorization header using the Bearer scheme."
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "bearer" }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // Configure CORS
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(
                    builder =>
                    {
                        builder.WithOrigins("https://rds-front-rds-frontend.app.cern.ch/") // your frontend's domain and port
                               .AllowAnyHeader()
                               .AllowAnyMethod()
                               .AllowCredentials(); // add this line
                    });
            });

            // Register your IUserService here
            services.AddScoped<IUserService, UserSearcher>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // if (env.IsDevelopment())
            if (true)
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Backend v1");

                    // Add this code to enable JWT authentication in Swagger UI
                    c.OAuthUsePkce();
                });
            }
            app.UseAuthentication();
            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthorization();

            // Use CORS
            app.UseCors();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
