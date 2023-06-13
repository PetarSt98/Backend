# Stage 1: Build the .NET application
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

# Copy csproj and restore dependencies
COPY *.sln .
COPY Backend/*.csproj ./Backend/
COPY SynchronizerLibrary/*.csproj ./SynchronizerLibrary/
RUN dotnet restore

# Copy everything else and build
COPY . .
WORKDIR /app/Backend
RUN dotnet publish -c Release -o out

# Stage 2: Set up a production-ready .NET runtime environment
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app
COPY --from=build /app/Backend/out .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Backend.dll"]
