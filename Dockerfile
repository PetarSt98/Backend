# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

# Copy and restore the main project
COPY Backend/Backend.csproj Backend/
RUN dotnet restore Backend/Backend.csproj

# Copy and build the shared library project
COPY SharedLibrary/SharedLibrary.csproj SharedLibrary/
RUN dotnet build SharedLibrary/SharedLibrary.csproj -c Release -o /app/build/SharedLibrary

# Copy the rest of the source code and build the main project
COPY Backend/ Backend/
RUN dotnet build Backend/Backend.csproj -c Release -o /app/build/Backend

# Stage 2: Publish the application
FROM build AS publish
RUN dotnet publish Backend/Backend.csproj -c Release -o /app/publish

# Stage 3: Set up a runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app
COPY --from=publish /app/publish .

# Expose the necessary ports and run the application
EXPOSE 80
ENTRYPOINT ["dotnet", "Backend.dll"]
