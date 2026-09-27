# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages dotnet restore src/ApiGateway/QualifyAI.ApiGateway/QualifyAI.ApiGateway.csproj --configfile NuGet.Config --disable-parallel
RUN --mount=type=cache,target=/root/.nuget/packages dotnet publish src/ApiGateway/QualifyAI.ApiGateway/QualifyAI.ApiGateway.csproj -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 DOTNET_NOLOGO=1
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet","QualifyAI.ApiGateway.dll"]
