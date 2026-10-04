FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for restore
COPY Sannel.Encoding.Manager.slnx ./
COPY src/Sannel.Encoding.Manager.Web/Sannel.Encoding.Manager.Web.csproj src/Sannel.Encoding.Manager.Web/
COPY src/Sannel.Encoding.Manager.Data/Sannel.Encoding.Manager.Data.csproj src/Sannel.Encoding.Manager.Data/
COPY src/Sannel.Encoding.Manager.HandBrake/Sannel.Encoding.Manager.HandBrake.csproj src/Sannel.Encoding.Manager.HandBrake/
COPY src/Sannel.Encoding.Manager.Migrations.Sqlite/Sannel.Encoding.Manager.Migrations.Sqlite.csproj src/Sannel.Encoding.Manager.Migrations.Sqlite/
COPY src/Sannel.Encoding.Manager.Migrations.Postgres/Sannel.Encoding.Manager.Migrations.Postgres.csproj src/Sannel.Encoding.Manager.Migrations.Postgres/
COPY src/Sannel.Encoding.Manager.DiscMenu/Sannel.Encoding.Manager.DiscMenu.csproj src/Sannel.Encoding.Manager.DiscMenu/
COPY src/Sannel.Encoding.Manager.DiscMenu.Probe/Sannel.Encoding.Manager.DiscMenu.Probe.csproj src/Sannel.Encoding.Manager.DiscMenu.Probe/

RUN dotnet restore src/Sannel.Encoding.Manager.Web/Sannel.Encoding.Manager.Web.csproj

# Copy everything and publish
COPY src/ src/
RUN dotnet publish src/Sannel.Encoding.Manager.Web/Sannel.Encoding.Manager.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Disc menu inspection (MCP inspect_disc_menus): libdvdnav for DVD menus, libbluray for Blu-ray menus,
# libvlc for menu screenshots. For BD-J (Java) menus also install libbluray-bdj and a JRE
# (e.g. openjdk-17-jre-headless) and set DiscMenu__JavaHome.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libdvdnav4 libbluray2 libvlc5 vlc-plugin-base \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Sannel.Encoding.Manager.Web.dll"]
