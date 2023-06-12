# Use the appropriate base image for your C# application
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

# Copy and restore the project files
COPY ./Backend.csproj /app/Backend.csproj

RUN dotnet restore

# Copy the remaining source code
COPY . .

# Build the application
RUN dotnet publish -c Release -o /app/publish

# Create a new stage for the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app

# Copy the published application from the build stage
COPY --from=build /app/publish .

# Expose the appropriate port(s) for your application
EXPOSE 8080

# Set the entry point for the application
ENTRYPOINT ["dotnet", "Backend.dll"]