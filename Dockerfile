# Stage 1: Build the .NET application
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

# Copy csproj and restore dependencies
COPY *.sln .
COPY Backend/*.csproj ./Backend/
COPY SynchronizerLibrary/*.csproj ./SynchronizerLibrary/
RUN dotnet restore

# Copy PowerShell script
COPY PowerShellScripts/SOAPNetworkService.ps1 ./Backend/

# Copy everything else and build
COPY . .
WORKDIR /app/Backend
RUN dotnet publish -c Release -o out

# Stage 2: Set up a production-ready .NET runtime environment
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime

# Install PowerShell
RUN apt-get update && \
    apt-get install -y powershell

WORKDIR /app
COPY --from=build /app/Backend/out .
COPY --from=build /app/Backend/SOAPNetworkService.ps1 .   # Copy the PowerShell script to the runtime image
EXPOSE 8080
ENTRYPOINT ["dotnet", "Backend.dll"]
