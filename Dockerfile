# syntax=docker/dockerfile:1
# Build Angular (SPA) + publicação ASP.NET Core — um único serviço Kestrel serve API + arquivos estáticos.

FROM node:20-alpine AS angular
WORKDIR /src/powerbi.client
COPY powerbi.client/package.json powerbi.client/package-lock.json ./
RUN npm ci
COPY powerbi.client/ ./
RUN npx ng build --configuration production

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS publish
WORKDIR /src
COPY PowerBi.Server/ ./PowerBi.Server/
COPY --from=angular /src/powerbi.client/dist/powerbi.client/browser ./PowerBi.Server/wwwroot
WORKDIR /src/PowerBi.Server
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish -p:SkipSpaProject=true --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENTRYPOINT ["dotnet", "PowerBi.Server.dll"]
