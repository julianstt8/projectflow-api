# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ProjectFlow.slnx ./
COPY src/ProjectFlow.Domain/ProjectFlow.Domain.csproj src/ProjectFlow.Domain/
COPY src/ProjectFlow.Application/ProjectFlow.Application.csproj src/ProjectFlow.Application/
COPY src/ProjectFlow.Infrastructure/ProjectFlow.Infrastructure.csproj src/ProjectFlow.Infrastructure/
COPY src/ProjectFlow.Api/ProjectFlow.Api.csproj src/ProjectFlow.Api/
RUN dotnet restore src/ProjectFlow.Api/ProjectFlow.Api.csproj

COPY src/ src/
RUN dotnet publish src/ProjectFlow.Api/ProjectFlow.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ProjectFlow.Api.dll"]
