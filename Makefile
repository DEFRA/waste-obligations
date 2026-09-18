VACUUM_IMAGE := dshanley/vacuum:latest@sha256:e13b08c36d5858753cafe677282bb38d9b7ca7945f3a3afba403ed47520dc5af

dependencies:
	dotnet tool restore

build: dependencies
	dotnet clean
	dotnet build src/Api/Api.csproj -c Release

lint-openapi: build
	docker pull $(VACUUM_IMAGE)
	docker run --rm -v "$(PWD):/work:ro" $(VACUUM_IMAGE) lint -d -r .vacuum.yml src/Api/openapi.json || true
