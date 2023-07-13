# Stage 1: Build the .NET application
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

# Copy solution file
COPY *.sln .

# Copy csproj files and restore
COPY Backend/*.csproj ./Backend/
COPY SynchronizerLibrary/*.csproj ./SynchronizerLibrary/
RUN dotnet restore

# Copy everything else and build
COPY Backend/. ./Backend/
COPY SynchronizerLibrary/. ./SynchronizerLibrary/

RUN dotnet publish -c Release -o out

# Stage 2: Run the .NET application
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime

# Allow the container user to bind to privileged ports
RUN apt-get update \
    && apt-get install -y libcap2-bin \
    && setcap 'cap_net_bind_service=+ep' /usr/share/dotnet/dotnet

# Create non-root user
RUN useradd -m myuser
USER myuser

WORKDIR /app
COPY --from=build /app/out .

# Copy the exe from SOAPServicesApi project to the Docker container
COPY SOAPServicesApi/bin/Release/SOAPServicesApi.exe ./Resources/SOAPServicesApi.exe

# Expose port 8080 and set ASP.NET Core to listen on port 8080
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Backend.dll"]
