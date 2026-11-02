FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/src/Aura.Api/Aura.Api.csproj backend/src/Aura.Api/
RUN dotnet restore backend/src/Aura.Api/Aura.Api.csproj
COPY backend/src/Aura.Api backend/src/Aura.Api
RUN dotnet publish backend/src/Aura.Api/Aura.Api.csproj -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet","Aura.Api.dll"]
