FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY GroundControl.sln ./
COPY src/GroundControl.Api/GroundControl.Api.csproj ./src/GroundControl.Api/
COPY src/GroundControl.Core/GroundControl.Core.csproj ./src/GroundControl.Core/
COPY src/GroundControl.Infrastructure/GroundControl.Infrastructure.csproj ./src/GroundControl.Infrastructure/
COPY tests/GroundControl.Tests/GroundControl.Tests.csproj ./tests/GroundControl.Tests/

# Restore dependencies
RUN dotnet restore

# Copy all source files
COPY . .

# Copy seed data to output
COPY Docs/seed_data.sql /app/seed_data.sql

# Build the application
WORKDIR /src/src/GroundControl.Api
RUN dotnet build -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Expose port
EXPOSE 8000

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8000
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "GroundControl.Api.dll"]