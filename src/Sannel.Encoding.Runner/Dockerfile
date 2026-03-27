FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for restore
COPY Sannel.Encoding.Manager.slnx ./
COPY src/Sannel.Encoding.Runner/Sannel.Encoding.Runner.csproj src/Sannel.Encoding.Runner/
COPY src/Sannel.Encoding.Manager.HandBrake/Sannel.Encoding.Manager.HandBrake.csproj src/Sannel.Encoding.Manager.HandBrake/

RUN dotnet restore src/Sannel.Encoding.Runner/Sannel.Encoding.Runner.csproj

# Copy everything and publish
COPY src/ src/
RUN dotnet publish src/Sannel.Encoding.Runner/Sannel.Encoding.Runner.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Sannel.Encoding.Runner.dll"]
