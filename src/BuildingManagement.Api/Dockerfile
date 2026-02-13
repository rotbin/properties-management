FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/BuildingManagement.Api/BuildingManagement.Api.csproj", "BuildingManagement.Api/"]
COPY ["src/BuildingManagement.Core/BuildingManagement.Core.csproj", "BuildingManagement.Core/"]
COPY ["src/BuildingManagement.Infrastructure/BuildingManagement.Infrastructure.csproj", "BuildingManagement.Infrastructure/"]
RUN dotnet restore "BuildingManagement.Api/BuildingManagement.Api.csproj"
COPY src/ .
WORKDIR "/src/BuildingManagement.Api"
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
# Create uploads directory
RUN mkdir -p /app/wwwroot/uploads
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "BuildingManagement.Api.dll"]
