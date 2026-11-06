CI_TOOLS_COMPOSE := compose/compose.ci-tools.yml

dependencies:
	dotnet tool restore

build: dependencies
	dotnet clean
	dotnet build src/Api/Api.csproj -c Release

lint-openapi: build
	docker compose -f $(CI_TOOLS_COMPOSE) pull vacuum
	mkdir -p TestResults
	rm -f TestResults/openapi-vacuum.json
	@status=0; docker compose -f $(CI_TOOLS_COMPOSE) run --rm --no-deps -T --user "$$(id -u):$$(id -g)" -v "$(PWD):/work:ro" -v "$(PWD)/TestResults:/reports" vacuum lint -r .vacuum.yml --format json --output /reports/openapi-vacuum.json src/Api/openapi.json || status=$$?; \
	  if [ "$$status" -gt 1 ]; then exit "$$status"; fi; \
	  python3 -c 'import json; r=json.load(open("TestResults/openapi-vacuum.json")); assert isinstance(r, list); print(json.dumps(r, indent=2))'
