FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MepPanel.LicenseServer/MepPanel.LicenseServer.csproj", "MepPanel.LicenseServer/"]
COPY ["Directory.Build.props", "./"]
COPY ["VERSION", "./"]
RUN dotnet restore "MepPanel.LicenseServer/MepPanel.LicenseServer.csproj"
COPY . .
WORKDIR "/src/MepPanel.LicenseServer"
RUN dotnet publish "MepPanel.LicenseServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
VOLUME ["/data"]
ENTRYPOINT ["dotnet", "MepPanel.LicenseServer.dll"]
