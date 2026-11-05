# Automation dependency pinning

The GitHub workflows use full commit SHAs for external actions. Version or
branch comments record the reference resolved when the pin was chosen. Local
reusable workflows run from the caller's commit and do not need a separate SHA.
The existing Docker build action pin was verified and retained.

Dockerfile base images and Compose images, including Trivy and Vacuum,
use version tags alongside registry manifest digests. The digest selects the
content; the tag identifies the release for readers and update tooling. Verify
the tag resolves to the digest when changing either. Manifest-list digests retain
platform selection for local ARM64 and CI AMD64 hosts.

SonarCloud's `docker:S8431` flags a tag and digest together in a Dockerfile. This
project excludes that rule for the root `Dockerfile` via SonarCloud's project
analysis scope; other rules remain active for that file. The PR analysis passed
with this exclusion and the tag-and-digest runtime reference. Revisit the
exclusion if SonarCloud reliably recognises the Dependabot configuration.
The image version tags were checked against their pinned digests on 5 November
2026.

The Sonar scanner and CSharpier versions are declared in the repository's
[.NET tool manifest](../.config/dotnet-tools.json) and restored by CI. Dependabot
can propose tool-manifest updates through the NuGet ecosystem.

## Updating pins

Dependabot checks GitHub Actions, Docker and Docker Compose dependencies weekly
using their [separate supported ecosystems](https://docs.github.com/en/code-security/reference/supply-chain-security/supported-ecosystems-and-repositories).
[Dependabot automation](dependabot-auto-merge.md) can approve and merge
verified minor and patch updates when all checks pass and publication capacity
is available. Updates without a minor or patch classification stay manual.
Mongo permits minor, patch and digest updates within version 7; major
version upgrades are excluded. A hash prevents a reference from moving silently,
but does not establish that the selected code is trustworthy. The Trivy and
Vacuum image references are in [compose.ci-tools.yml](../compose/compose.ci-tools.yml),
which is scanned by the configured Docker Compose ecosystem. Resolve
action references against the upstream repository and container digests against
the registry. Run the repository's required Compose/build/test cycle.

## Coverage plan

- Dependabot monitors the root NuGet solution and the separate healthcheck
  solution; the PR workflow builds and tests the healthcheck tool. Verify the
  first post-merge update includes its expected project files.
- Dependabot can update the Sonar scanner through the .NET tool manifest. Verify
  its next update PR changes the manifest and that its scanner and quality gate
  complete before relying on automatic merges.
- Trivy and Vacuum use image references in `compose/compose.ci-tools.yml`,
  covered by the `/compose` Docker Compose entry. Verify the first update PRs
  change the expected references and Dependabot supplies their update type;
  unclassified or major updates remain manual.
- The Node, .NET and Java setup selectors and `ubuntu-latest` runner are not
  version updates proposed by Dependabot. Their pinned setup actions receive
  Dependabot PRs, but runtime-major and runner-image changes require an
  explicit scheduled review or a separately tested update mechanism.

## Remaining trust boundaries

- The pinned `DEFRA/cdp-build-action` composites still invoke mutable upstream
  action tags (AWS credentials, Secrets Manager, ECR login, Docker login and
  Docker build). Those references need pinning in that repository, followed by
  updating this repository's commit pin. Pinning a composite does not pin its
  nested actions or downloaded tools.
- The journey-test action and test code retain matching-branch selection with
  fallback to `main`, unchanged from the original workflow. These dynamic
  references are not pinned: branch write access remains a trust boundary
  because these tests execute with CI secrets. The inspected
  upstream composite also contains mutable action references, including
  `DEFRA/cdp-build-action/docker-login@main`, and needs upstream hardening.
  Other service source checkouts and published images are controlled by that
  composite. Freezing its action implementation to a committed SHA here would
  remove the existing ability to test action changes on matching branches.
- GitHub-hosted runner images, Node/.NET/Java release selectors, packages
  installed by `apt`, NuGet transitive dependencies, Sonar server downloads and
  Trivy vulnerability databases are not made immutable by these changes.
  A fully reproducible build would additionally require package lock files,
  controlled package feeds/snapshots and pinned toolchain distributions.
- Pinning does not prevent malicious code already present in the chosen commit,
  compromised credentials, or unsafe workflow changes. Workflow permissions and
  repository rules are separate controls.

Pins were resolved from upstream GitHub, container registry and NuGet APIs on
18 September 2026. No upstream repositories or repository settings were changed.
The dependency-review action v5.0.0 pin and the refreshed ASP.NET runtime digest
were verified on 7 October 2026. The runtime update removes the high-severity
OpenSSL finding exposed by the new blocking image scan. See
[dependency automation security exposure](dependabot-auto-merge.md#eligibility)
for credential, transitive-dependency and unknown-malware limits.
