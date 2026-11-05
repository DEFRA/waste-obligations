CI_TOOLS_COMPOSE := compose/compose.ci-tools.yml

dependencies:
	dotnet tool restore

build: dependencies
	dotnet clean
	dotnet build src/Api/Api.csproj -c Release

lint-openapi: build
	docker compose -f $(CI_TOOLS_COMPOSE) pull vacuum
	docker compose -f $(CI_TOOLS_COMPOSE) run --rm --no-deps -T -v "$(PWD):/work:ro" vacuum lint -d -r .vacuum.yml src/Api/openapi.json || true
