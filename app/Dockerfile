# syntax=docker/dockerfile:1
ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:8.0.408
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:8.0.31

FROM ${DOTNET_SDK_IMAGE} AS build
ARG LAB=starter
WORKDIR /src

# Copy the project first so package restore stays cached when only source changes.
COPY ${LAB}/Workshop.Orders.csproj ./
RUN dotnet restore ./Workshop.Orders.csproj

COPY ${LAB}/ ./
RUN dotnet publish ./Workshop.Orders.csproj --no-restore -c Release -o /out/app

FROM ${DOTNET_SDK_IMAGE} AS healthcheck-build
WORKDIR /src
COPY Healthcheck/Workshop.Healthcheck.csproj ./
RUN dotnet restore ./Workshop.Healthcheck.csproj
COPY Healthcheck/ ./
RUN dotnet publish ./Workshop.Healthcheck.csproj --no-restore -c Release -o /out/healthcheck

FROM ${DOTNET_SDK_IMAGE} AS tools
ARG DOTNET_COUNTERS_VERSION=10.0.745401
ENV DOTNET_CLI_HOME=/tmp \
    PATH=/tools:$PATH
RUN dotnet tool install --tool-path /tools dotnet-counters --version ${DOTNET_COUNTERS_VERSION} \
    && chmod -R a+rx /tools
USER 1654:1654
ENTRYPOINT ["dotnet-counters"]

FROM ${DOTNET_RUNTIME_IMAGE} AS runtime
WORKDIR /app
COPY --from=build /out/app ./
COPY --from=healthcheck-build /out/healthcheck /healthcheck/

# .NET 8 container images support ASPNETCORE_HTTP_PORTS; the app listens on 8080.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER 1654:1654

# The runtime image has no curl or wget. This .NET probe performs GET / without OS packages.
HEALTHCHECK --interval=5s --timeout=2s --start-period=10s --retries=12 CMD ["dotnet", "/healthcheck/Workshop.Healthcheck.dll"]
ENTRYPOINT ["dotnet", "Workshop.Orders.dll"]