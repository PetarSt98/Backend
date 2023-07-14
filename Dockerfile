# Stage 1: Set up a .NET 6.0 build environment for Backend and SynchronizerLibrary
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
RUN dotnet publish Backend.csproj -c Release -o out  # specify the project file here

# Stage 2: Set up a production-ready .NET 6.0 runtime environment
# Include Python 2.7 runtime along with .NET
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
RUN apt-get update && apt-get install -y python2.7 curl
WORKDIR /app
COPY --from=build /app/Backend/out .

# Install Python dependencies
RUN curl https://bootstrap.pypa.io/pip/2.7/get-pip.py --output get-pip.py \
    && python2.7 get-pip.py \
    && pip install suds

# Copy your Python script
COPY PowerShellScripts/SOAPNetworkService.py .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Backend.dll"]
