# MediaPortal 2 server (experimental, not yet verified on a Linux host)
# Build from the repository root:
#   docker build -f MediaPortal/Source/Main/MediaPortal.Server.Host/Deployment/Dockerfile -t mp2-server .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet build MediaPortal/Source/MP2-Server-Linux.slnf -c Release

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /src/MediaPortal/Bin/MP2-Server/bin/Release/ .
# Database, configuration and logs live in the data volume
VOLUME /data
# UPnP/HTTP communication with clients, host networking is recommended (UPnP discovery uses multicast)
ENTRYPOINT ["dotnet", "MP2-Server-Host.dll", "--data", "/data"]
