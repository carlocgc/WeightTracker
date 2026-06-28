FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props .
COPY src/WeightTracker.Web/WeightTracker.Web.csproj src/WeightTracker.Web/
RUN dotnet restore src/WeightTracker.Web/WeightTracker.Web.csproj

COPY src/WeightTracker.Web/ src/WeightTracker.Web/
RUN dotnet publish src/WeightTracker.Web/WeightTracker.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__WeightTracker="Data Source=/data/weighttracker.db"

EXPOSE 8080
VOLUME ["/data"]

RUN groupadd --system --gid 10001 weighttracker \
    && useradd --system --uid 10001 --gid weighttracker --create-home --home-dir /home/weighttracker weighttracker \
    && mkdir -p /data \
    && chown -R weighttracker:weighttracker /app /data /home/weighttracker

COPY --from=build --chown=weighttracker:weighttracker /app/publish .

USER weighttracker
ENTRYPOINT ["dotnet", "WeightTracker.Web.dll"]
