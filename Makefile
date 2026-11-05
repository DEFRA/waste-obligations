VACUUM_IMAGE := dshanley/vacuum:v0.36.2@sha256:75cc764c2d4ca6b5fd8747326929b76bf58e6987f1bf1812a8bcab200743a5e2

dependencies:
	dotnet tool restore

build: dependencies
	dotnet clean
	dotnet build src/Api/Api.csproj -c Release

lint-openapi: build
	docker pull $(VACUUM_IMAGE)
	docker run --rm -v "$(PWD):/work:ro" $(VACUUM_IMAGE) lint -d -r .vacuum.yml src/Api/openapi.json || true
