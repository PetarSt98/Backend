# Stage 1: Build the .NET application
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

# Copy csproj and restore dependencies
COPY *.sln .
COPY Backend/*.csproj ./Backend/
COPY SynchronizerLibrary/*.csproj ./SynchronizerLibrary/
RUN dotnet restore

# Copy PowerShell script
COPY PowerShellScripts/SOAPNetworkService.ps1 ./PowerShellScripts/
COPY PowerShellScripts/SOAPNetworkService.ps1 ./Backend/
# Copy everything else and build
COPY . .
WORKDIR /app/Backend
RUN dotnet publish Backend.csproj -c Release -o out

# Stage 2: Set up a production-ready .NET runtime environment
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime

# Install PowerShell
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       ca-certificates \
       curl \
       apt-transport-https \
       gnupg2 \
    && curl https://packages.microsoft.com/keys/microsoft.asc | apt-key add - \
    && echo "deb [arch=amd64] https://packages.microsoft.com/repos/microsoft-debian-buster-prod buster main" > /etc/apt/sources.list.d/microsoft.list \
    && apt-get update \
    && apt-get install -y powershell \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/Backend/out .
COPY --from=build /app/Backend/SOAPNetworkService.ps1 .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Backend.dll"]
