FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY GroundControl.slnx .
COPY src/GroundControl.Api/GroundControl.Api.csproj src/GroundControl.Api/
COPY tests/GroundControl.Tests/GroundControl.Tests.csproj tests/GroundControl.Tests/
RUN dotnet restore src/GroundControl.Api/GroundControl.Api.csproj

COPY . .
RUN dotnet publish src/GroundControl.Api/GroundControl.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8000
EXPOSE 8000

ENTRYPOINT ["dotnet", "GroundControl.Api.dll"]
