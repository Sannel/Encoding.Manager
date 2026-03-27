FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for restore
COPY Sannel.Encoding.Manager.slnx ./
COPY src/Sannel.Encoding.Manager.Web/Sannel.Encoding.Manager.Web.csproj src/Sannel.Encoding.Manager.Web/
COPY src/Sannel.Encoding.Manager.Data/Sannel.Encoding.Manager.Data.csproj src/Sannel.Encoding.Manager.Data/
COPY src/Sannel.Encoding.Manager.HandBrake/Sannel.Encoding.Manager.HandBrake.csproj src/Sannel.Encoding.Manager.HandBrake/
COPY src/Sannel.Encoding.Manager.Migrations.Sqlite/Sannel.Encoding.Manager.Migrations.Sqlite.csproj src/Sannel.Encoding.Manager.Migrations.Sqlite/
COPY src/Sannel.Encoding.Manager.Migrations.Postgres/Sannel.Encoding.Manager.Migrations.Postgres.csproj src/Sannel.Encoding.Manager.Migrations.Postgres/

RUN dotnet restore src/Sannel.Encoding.Manager.Web/Sannel.Encoding.Manager.Web.csproj

# Copy everything and publish
COPY src/ src/
RUN dotnet publish src/Sannel.Encoding.Manager.Web/Sannel.Encoding.Manager.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Sannel.Encoding.Manager.Web.dll"]
