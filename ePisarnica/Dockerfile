FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["ePisarnica.csproj", "."]
RUN dotnet restore "./ePisarnica.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "ePisarnica.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ePisarnica.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app

# Create certificates directory and copy certificate
RUN mkdir -p /app/certificates
COPY certificates/cert_realan.pfx /app/certificates/cert_realan.pfx

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ePisarnica.dll"]
ENV ASPNETCORE_URLS=http://*:80