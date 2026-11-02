# HTTP runbooks

Use Rider's HTTP Client or the [httpYac extension for VS Code](https://marketplace.visualstudio.com/items?itemName=anweber.vscode-httpyac) to run these requests. The admin routes require a client with the `admin` scope.

For Docker, run `docker compose up --build -d --wait`, select the `docker` environment and use the `.docker.http` files. They use the development Basic-auth client configured in `appsettings.Development.json` on `http://localhost:8080`.

For CDP, copy `http-client.private.env.json.example` to `http-client.private.env.json`, fill in the service URL and OAuth client credentials for the environment, and select that environment with the `.http` files. The private file is ignored by Git. Requests obtain a client-credentials token for the CDP gateway; the client must also have the service's admin scope.

Replace `organisationId` and `obligationYear` in the selected environment with the values being investigated. The organisation ID must be a GUID. Some requests accept optional filters, noted alongside the request. Audit events and several admin lists return newline-delimited JSON.

- [Audit events](audit-events.http) | [Docker](audit-events.docker.http)
- [Organisations](organisations.http) | [Docker](organisations.docker.http)
- [Unsubmitted compliance declarations](unsubmitted-compliance-declarations.http) | [Docker](unsubmitted-compliance-declarations.docker.http)
- [Health](health.http) | [Docker](health.docker.http)

Starting a historical backfill changes data. Check the target environment before running it; the service rejects the request while obligation polling is enabled.
