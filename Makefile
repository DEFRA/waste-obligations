dependencies:
	dotnet tool restore

build: dependencies
	dotnet clean
	dotnet build src/Api/Api.csproj -c Release

lint-openapi: build
	docker pull dshanley/vacuum
	docker run --rm -v "$(PWD):/work:ro" dshanley/vacuum lint -d -r .vacuum.yml src/Api/openapi.json || true
