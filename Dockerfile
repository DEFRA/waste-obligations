# Base dotnet image
FROM mcr.microsoft.com/dotnet/aspnet:11.0.0@sha256:bc0d6653979efba491ab3d6f589d691f96c309c3c3ef81325efa6978e7b7f242 AS base
WORKDIR /app

# Add curl to template.
# CDP PLATFORM HEALTHCHECK REQUIREMENT
RUN apt-get update && \
    apt-get install -y --only-upgrade apt libapt-pkg6.0t64 && \
    apt-get install -y curl && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Build stage image
FROM mcr.microsoft.com/dotnet/sdk:10.0.403@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e AS build
WORKDIR /src

COPY .config/dotnet-tools.json .config/dotnet-tools.json
COPY .csharpierrc .csharpierrc
COPY .editorconfig .editorconfig
COPY global.json global.json

RUN dotnet tool restore

COPY src/Api/Api.csproj src/Api/Api.csproj
COPY src/AuditEvents/AuditEvents.csproj src/AuditEvents/AuditEvents.csproj
COPY tests/Testing/Testing.csproj tests/Testing/Testing.csproj
COPY tests/Api.Tests/Api.Tests.csproj tests/Api.Tests/Api.Tests.csproj
COPY tests/Api.IntegrationTests/Api.IntegrationTests.csproj tests/Api.IntegrationTests/Api.IntegrationTests.csproj
COPY waste-obligations.slnx waste-obligations.slnx
COPY Directory.Build.props Directory.Build.props

RUN dotnet restore

COPY src/Api src/Api
COPY src/AuditEvents src/AuditEvents
COPY tests/Testing tests/Testing
COPY tests/Api.Tests tests/Api.Tests
COPY tests/Api.IntegrationTests tests/Api.IntegrationTests

RUN dotnet csharpier check .

RUN dotnet build --no-restore --warnaserror && \
    dotnet test --test-modules tests/Api.Tests/bin/Debug/net10.0/Api.Tests.dll --no-build

FROM build AS publish

RUN dotnet publish src/Api -c Release -warnaserror -o /app/publish /p:UseAppHost=false

ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

# Final production image
FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .

EXPOSE 8085
ENTRYPOINT ["dotnet", "Api.dll"]
