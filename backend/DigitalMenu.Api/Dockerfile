FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY DigitalMenu.slnx ./
COPY backend/DigitalMenu.Api/DigitalMenu.Api.csproj backend/DigitalMenu.Api/
COPY backend/DigitalMenu.Application/DigitalMenu.Application.csproj backend/DigitalMenu.Application/
COPY backend/DigitalMenu.Domain/DigitalMenu.Domain.csproj backend/DigitalMenu.Domain/
COPY backend/DigitalMenu.Infrastructure/DigitalMenu.Infrastructure.csproj backend/DigitalMenu.Infrastructure/

RUN dotnet restore backend/DigitalMenu.Api/DigitalMenu.Api.csproj

COPY . .
RUN dotnet publish backend/DigitalMenu.Api/DigitalMenu.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DigitalMenu.Api.dll"]
