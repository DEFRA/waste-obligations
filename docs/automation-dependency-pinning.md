# Automation dependency pinning

The four GitHub workflows use full commit SHAs for external actions. Version or
branch comments record the reference resolved when the pin was chosen. Local
reusable workflows run from the caller's commit and do not need a separate SHA.
The existing Docker build action pin was verified and retained.

Dockerfile base images, Compose images, Trivy and the Makefile's Vacuum image
use registry manifest digests. Tags remain for readability; the digest selects
the content, including for tags named `latest`. Manifest-list digests retain
platform selection for local ARM64 and CI AMD64 hosts.

The Sonar scanner is installed at an explicit NuGet version, with a matching
cache key and no fallback to the old unversioned scanner cache. NuGet tools
are versioned packages, not GitHub actions, so a Git commit is not a usable
installation reference.

## Updating pins

Dependabot checks GitHub Actions, Docker and Docker Compose dependencies weekly
using their [separate supported ecosystems](https://docs.github.com/en/code-security/reference/supply-chain-security/supported-ecosystems-and-repositories). Review
its changes before merging. Mongo permits minor, patch and digest updates within version 7; major
version upgrades are excluded. A hash prevents a reference from moving silently,
but does not establish that the selected code is trustworthy. The Sonar scanner
version, Trivy command and Vacuum Makefile pin need explicit review when updating;
do not assume Dependabot discovers arbitrary shell commands or Make variables.
Resolve action references against the upstream repository and container digests
against the registry. Run the repository's required Compose/build/test cycle.

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
