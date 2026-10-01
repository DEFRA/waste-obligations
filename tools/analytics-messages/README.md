# Analytics message samples

Create files containing the message body and attributes produced by the API's
analytics transport, and optionally send the same message to a local SQS queue.
The tool is a standalone .NET 10 console application under
`tools/analytics-messages`; it is not part of `waste-obligations.slnx` and does not
start the API. The [Compose queue harness](#generate-and-send-with-compose)
runs Floci and the tool together using Docker, without .NET on the host.

The tool and its tests are excluded from Sonar analysis and coverage. Their local
build settings also remove the inherited Sonar C# analyzer package.

Commands below use a POSIX shell such as Bash or Zsh and run from the repository
root unless stated otherwise. A PowerShell example is included for Docker
generation. Keep the tool in the checkout: its build settings import the root
`Directory.Build.props`.

## Prerequisites and run options

Choose one way to run the tool:

| Available on the host | How to run |
| --- | --- |
| .NET 10 SDK | [Run the source project](#generate-files). The SDK includes the runtime. |
| Docker, without .NET | [Generate files inside the SDK container](#generate-without-installing-net), or [generate and send to Floci with Compose](#generate-and-send-with-compose). No host SDK or runtime is needed. |
| .NET 10 runtime, without the SDK | [Run a published framework-dependent copy](#host-with-the-net-10-runtime-only) supplied by a developer. |
| Neither .NET nor Docker | [Run a self-contained copy](#host-without-a-net-runtime-or-docker) published for the host's operating system and CPU. |

Check an existing .NET installation with:

```sh
dotnet --list-sdks
dotnet --list-runtimes
```

Source execution requires a `10.0` SDK. A runtime-only host needs
`Microsoft.NETCore.App 10.0.x` for the framework-dependent copy. If `dotnet`
cannot be found, use Docker or obtain a self-contained copy. Installing the
[.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) is another
option; installing only the runtime does not enable `dotnet run` or publishing.

File generation without `--queue-url` does not require the API, MongoDB, an AWS
account or queue services. Source builds may need network access to NuGet. The
optional queue harness requires a running Docker engine with Linux containers
and Docker Compose v2. It supplies its own AWS CLI and local credentials; no
host AWS CLI or real AWS credentials are needed. Check Docker with:

```sh
docker version
docker compose version
```

## Generate files

With the .NET 10 SDK installed, generate the bundled event:

```sh
dotnet run --project tools/analytics-messages -- \
  tools/analytics-messages/examples/compliance-declaration-created.json \
  tools/analytics-messages/output
```

The input must be a UTF-8 JSON object. The tool preserves its original bytes,
including formatting, apart from an optional UTF-8 byte order mark. It does not
validate the analytics schema or construct an audit event. Use a complete
analytics event when testing a consumer's event handling; the bundled example
shows that shape.

To use your own event, replace the first argument with its JSON file path and
the second with your chosen output directory. Quote paths containing spaces.
The tool keeps event IDs, timestamps and payload values exactly as supplied;
edit these in the input when testing a consumer that deduplicates events.
Input formatting counts towards the size threshold, so use the actual
serialised JSON when comparing the API's size decision.

The output directory must be missing or empty. Each run writes:

| File | Contents |
| --- | --- |
| `message-body.txt` | The UTF-8 message body, without an added newline or byte order mark. |
| `message-attributes.json` | AWS message attributes using `DataType` and `StringValue`, ready for SNS or SQS `--message-attributes`. |
| `message.json` | A consumer fixture containing `Body` and `MessageAttributes`. It has no queue-generated `MessageId` or `ReceiptHandle`. |

The API's body threshold is **258,048 bytes** (252 KiB). At or below that size,
the body stays as JSON. Above it, the tool compresses the original UTF-8 bytes
with gzip at .NET's `CompressionLevel.SmallestSize`, encodes the result as
Base64 and adds `Content-Encoding: gzip+base64`. Every message also has
`Content-Type: application/json`. If the encoded body still exceeds 258,048
bytes, generation fails. The limit applies to body bytes; it is the API's
transport threshold, not a substitute for AWS request limits.

To exercise decompression with a small input, use a different empty directory:

```sh
dotnet run --project tools/analytics-messages -- \
  tools/analytics-messages/examples/compliance-declaration-created.json \
  tools/analytics-messages/output-compressed \
  --force-compression
```

`--force-compression` deliberately compresses inputs that the API would send as
plain JSON. Use it for a compressed transport fixture; omit it when checking
the API's size-based decision.

The default output folders shown here are Git-ignored. Choose a new empty
folder for each run, or remove files from a previous run before reusing its
folder. Generation does not append messages or overwrite existing files.
Without `--queue-url`, the tool only writes files. See
[local queue publishing](#publish-to-a-local-queue) to also send a message.

## Generate without installing .NET

Docker can build and run the tool using the same pinned .NET SDK image as the
repository's main Docker build. Only Docker is needed on the host. Choose this
route instead of the source command above, and start with an empty output
directory:

```sh
mkdir -p tools/analytics-messages/output

docker run --rm \
  --mount "type=bind,source=$PWD,target=/workspace,readonly" \
  --mount "type=bind,source=$PWD/tools/analytics-messages/output,target=/messages" \
  --workdir /workspace \
  mcr.microsoft.com/dotnet/sdk:10.0@sha256:35d40304542c8689331f8cab17c65926cdf48fe711e289321d71924b230a7d29 \
  dotnet run --project tools/analytics-messages \
  --artifacts-path /tmp/analytics-messages-build \
  -- tools/analytics-messages/examples/compliance-declaration-created.json \
  /messages --force-compression
```

This writes the three files to `tools/analytics-messages/output` on the host.
The checkout is mounted read-only; build and restore output stays in the
temporary container, which is removed after the command finishes.
[`--artifacts-path`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-run#options)
keeps those build files separate from host builds. The first run may need to
download the image and restore dependencies. Subsequent runs still build the
tool in a fresh container.

Omit `--force-compression` for the API's normal size-based behaviour. To use
another input inside the checkout, replace its path after `--`. An input
outside the checkout needs an additional read-only bind mount; pass its
container path to the tool. To change the output location, create an empty host
directory and change the second mount's `source`, keeping its `target` and the
output argument as `/messages`.

The equivalent Docker command in Windows PowerShell is:

```powershell
New-Item -ItemType Directory -Force tools/analytics-messages/output | Out-Null

docker run --rm `
  --mount "type=bind,source=$($PWD.Path),target=/workspace,readonly" `
  --mount "type=bind,source=$($PWD.Path)/tools/analytics-messages/output,target=/messages" `
  --workdir /workspace `
  mcr.microsoft.com/dotnet/sdk:10.0@sha256:35d40304542c8689331f8cab17c65926cdf48fe711e289321d71924b230a7d29 `
  dotnet run --project tools/analytics-messages `
  --artifacts-path /tmp/analytics-messages-build `
  -- tools/analytics-messages/examples/compliance-declaration-created.json `
  /messages --force-compression
```

Docker must be allowed to mount the checkout and output paths. On Linux, files
written by the SDK container may be owned by root. If that prevents host edits,
add `--user "$(id -u):$(id -g)"` and
`--env DOTNET_CLI_HOME=/tmp/dotnet-home --env NUGET_PACKAGES=/tmp/nuget-packages`
before the image name when running the POSIX command.

## Use a published copy

A developer with the .NET 10 SDK can publish the tool and send the output
directory to another developer. Publish the tool project directly, rather than
the main API solution. The example output paths below are Git-ignored.

### Host with the .NET 10 runtime only

On the publishing machine:

```sh
dotnet publish tools/analytics-messages/AnalyticsMessages.csproj \
  -c Release --self-contained false -p:UseAppHost=false \
  -o tools/analytics-messages/artifacts/framework-dependent
```

Copy the **entire** `artifacts/framework-dependent` directory to the receiving
host, including `AnalyticsMessages.dll`, `AnalyticsMessages.deps.json` and
`AnalyticsMessages.runtimeconfig.json`. Also supply an input event, such as the
bundled example. From the copied directory, run:

```sh
dotnet AnalyticsMessages.dll /path/to/input.json /path/to/empty-output-directory \
  --force-compression
```

The host needs the .NET 10 runtime but no SDK or repository checkout. Omit the
last option for normal size-based behaviour. A published copy also accepts
`--queue-url` when a local queue is already running; use the host queue URL
shown in [local queue publishing](#generate-and-send-with-host-net).

### Host without a .NET runtime or Docker

Publish a self-contained copy on a machine with the .NET 10 SDK. It includes
the .NET runtime, so the recipient can run the executable directly. Choose the
runtime identifier for the **receiving** host:

| Receiving host | Runtime identifier |
| --- | --- |
| Linux (glibc), x64 | `linux-x64` |
| Linux (glibc), ARM64 | `linux-arm64` |
| Alpine Linux (musl), x64 | `linux-musl-x64` |
| Alpine Linux (musl), ARM64 | `linux-musl-arm64` |
| macOS, Intel | `osx-x64` |
| macOS, Apple Silicon | `osx-arm64` |
| Windows, x64 | `win-x64` |
| Windows, ARM64 | `win-arm64` |

See the [.NET runtime identifier catalog](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog)
if the target host differs from these examples.

For example, publish for a Linux x64 host:

```sh
dotnet publish tools/analytics-messages/AnalyticsMessages.csproj \
  -c Release -r linux-x64 --self-contained true \
  -o tools/analytics-messages/artifacts/linux-x64
```

Replace `linux-x64` in both arguments for a different target. Publishing may
download that target's runtime packs. Copy the **entire** output directory and
an input event to the receiving host. On Linux or macOS, run from the copied
directory:

```sh
chmod +x AnalyticsMessages
./AnalyticsMessages /path/to/input.json /path/to/empty-output-directory \
  --force-compression
```

For a Windows publish, run in PowerShell:

```powershell
.\AnalyticsMessages.exe C:\samples\input.json C:\samples\output --force-compression
```

Self-contained output is specific to the chosen operating system and CPU;
publish a separate copy for each target. It still relies on the operating
system's native dependencies. See Microsoft's
[deployment guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/)
and [installation prerequisites](https://learn.microsoft.com/en-us/dotnet/core/install/)
for supported hosts. If neither .NET nor Docker can be installed, obtain this
published directory from a developer who can build it; source files alone are
not executable.

## Arguments and exit codes

All run options use the same tool arguments:

```text
analytics-messages <input.json> <output-directory> [--force-compression] [--queue-url <url>]
analytics-messages --help
```

`-h` also prints help. With `dotnet run`, keep `--` between the .NET CLI options
and these tool arguments. With a published DLL or executable, pass the tool
arguments directly.

The optional flags can appear in either order after the input and output
arguments. `--queue-url` writes all three files first, then sends their exact
body and attributes directly to SQS. A successful send prints its `MessageId`.
The files remain available if the send fails.

Publishing is limited to a local emulator: the queue URL must use HTTP with a
loopback host such as `localhost` or `127.0.0.1`, or the Compose service host
`floci`. It must include a queue path and have no credentials, query string or
fragment. The tool uses the URL's origin as its AWS endpoint, region
`eu-west-2`, and fixed local credentials `test` / `test`. It uses a ten-second
send timeout with no automatic retries.

| Exit code | Meaning |
| --- | --- |
| `0` | Files were generated and any requested queue send succeeded, or help was printed. |
| `1` | Invalid input, encoding/size failure, a file/directory access error, or a queue-send failure. The reason is printed to stderr. Files generated before a queue-send failure are retained. |
| `2` | Invalid command arguments. Usage is printed to stderr. |

## Read the files in a consumer

Load `message.json` as a fixture, or read `message-body.txt` together with
`message-attributes.json`. The attributes are AWS message metadata, not HTTP
headers. Always keep the body and attributes together when sharing files.

Decode the message using `Content-Encoding.StringValue`:

1. If `Content-Encoding` is absent, parse the body directly as JSON.
2. If its value is `gzip+base64`, base64-decode the body, gzip-decompress the
   bytes, decode them as UTF-8, then parse the resulting JSON.
3. Reject any other encoding rather than guessing.

For example, this optional Python 3 check reads either generated encoding and
prints the original JSON. It is not required to generate or queue messages:

```sh
python3 - <<'PY'
from pathlib import Path
import base64
import gzip
import json

message = json.loads(Path('tools/analytics-messages/output/message.json').read_text(encoding='utf-8'))
body = message['Body']
encoding = message['MessageAttributes'].get('Content-Encoding', {}).get('StringValue')
if encoding == 'gzip+base64':
    body = gzip.decompress(base64.b64decode(body, validate=True)).decode('utf-8')
elif encoding is not None:
    raise ValueError(f'Unsupported content encoding: {encoding}')
json.loads(body)
print(body, end='')
PY
```

## Publish to a local queue

The optional Compose stack runs a separate Floci instance with a dedicated SNS
topic and SQS queue. It uses the same pinned Floci and AWS CLI images as the main
Compose stack, but a separate project, network and port. It does not start the
main application or share its analytics queue.

### Generate and send with Compose

This is the quickest route when Docker is available. From the repository root,
create an empty output directory and run:

```sh
mkdir -p tools/analytics-messages/output

docker compose -f tools/analytics-messages/compose.yml run --rm generator \
  tools/analytics-messages/examples/compliance-declaration-created.json \
  /messages --force-compression \
  --queue-url http://floci:4566/000000000000/analytics_message_samples_queue
```

Compose starts Floci, waits for its health check, and initialises the queue,
topic and subscription before running the tool. The tool writes the three files
to `tools/analytics-messages/output` on the host, then sends the message directly
to SQS and prints its `MessageId`. Floci stays running so a consumer can read it.
No host .NET SDK, runtime or AWS CLI is needed. The SDK container may need
network access to download its image and restore dependencies.

Omit `--force-compression` to use the API's size-based decision. Replace the
input path to use another event inside the checkout. The checkout is mounted
read-only and build output stays in the temporary container. For another host
output directory, create it first and set `MESSAGE_DIRECTORY` to its absolute
path before the Compose command; keep the tool's output argument as `/messages`.

In PowerShell, the same run can be written on one command line:

```powershell
New-Item -ItemType Directory -Force tools/analytics-messages/output | Out-Null
docker compose -f tools/analytics-messages/compose.yml run --rm generator tools/analytics-messages/examples/compliance-declaration-created.json /messages --force-compression --queue-url http://floci:4566/000000000000/analytics_message_samples_queue
```

Each run needs an empty output directory. `mkdir -p` does not clear files left
by a previous run, even if its queue send failed. The tool does not delete those
files or retry failed sends. If a send times out, inspect the queue before
sending again: the message may have arrived even though the acknowledgement
did not, and another send can create a duplicate.

The command above sends directly to SQS. To test the production SNS-to-SQS
transport too, use the [SNS publish route](#send-existing-files-through-sns)
below with files generated without `--queue-url`.

### Generate and send with host .NET

With the .NET 10 SDK on the host, start and initialise the queue explicitly:

```sh
docker compose -f tools/analytics-messages/compose.yml up -d --wait floci
docker compose -f tools/analytics-messages/compose.yml run --rm floci-init
```

Then generate files in a new empty directory and send them to the host endpoint:

```sh
dotnet run --project tools/analytics-messages -- \
  tools/analytics-messages/examples/compliance-declaration-created.json \
  tools/analytics-messages/output-sqs --force-compression \
  --queue-url http://localhost:14566/000000000000/analytics_message_samples_queue
```

Published DLLs and self-contained executables accept the same arguments after
their executable name. The standalone `docker run` command above can also send
to the running stack: add
`--network waste-obligations-analytics-messages_default` before the SDK image
name, and add `--queue-url` with the `http://floci:4566/...` queue URL to the tool
arguments. `localhost` inside a container refers to that container; use `floci`
for containers on this Compose network.

### Send existing files through SNS

This route exercises SNS publication and the queue subscription used by the
API's transport. Generate files using any route above without `--queue-url`,
then start and initialise Floci using the two startup commands above if it is
not already running. The subscription uses `RawMessageDelivery=true`, so SQS
receives the generated body and attributes directly, without an SNS envelope.
Publish the files from the default output directory:

```sh
docker compose -f tools/analytics-messages/compose.yml run --rm aws sns publish \
  --topic-arn arn:aws:sns:eu-west-2:000000000000:analytics_message_samples \
  --message file:///messages/message-body.txt \
  --message-attributes file:///messages/message-attributes.json
```

The AWS CLI service mounts `tools/analytics-messages/output` read-only at
`/messages`. To publish from another directory, set `MESSAGE_DIRECTORY` to its
absolute path for the command, for example:

```sh
MESSAGE_DIRECTORY="$PWD/tools/analytics-messages/output-compressed" \
  docker compose -f tools/analytics-messages/compose.yml run --rm aws sns publish \
  --topic-arn arn:aws:sns:eu-west-2:000000000000:analytics_message_samples \
  --message file:///messages/message-body.txt \
  --message-attributes file:///messages/message-attributes.json
```

### Receive and process messages

SQS consumers poll the queue; they do not subscribe to it as a push stream.
The SNS subscription is what routes published SNS messages into the queue. A
consumer reads directly sent and SNS-delivered messages in the same way.

Receive a message with all its attributes:

```sh
docker compose -f tools/analytics-messages/compose.yml run --rm aws sqs receive-message \
  --queue-url http://floci:4566/000000000000/analytics_message_samples_queue \
  --message-attribute-names All \
  --attribute-names All \
  --max-number-of-messages 1 \
  --wait-time-seconds 10
```

A receive temporarily hides the message; it does not delete it. After checking
the response, delete it using the returned `ReceiptHandle`:

```sh
docker compose -f tools/analytics-messages/compose.yml run --rm aws sqs delete-message \
  --queue-url http://floci:4566/000000000000/analytics_message_samples_queue \
  --receipt-handle '<ReceiptHandle from receive-message>'
```

The response wraps messages in a `Messages` array. Apply the decoding steps
above to each message's `Body` and `MessageAttributes`. A consumer must request
all message attributes so it receives `Content-Encoding`. Delete only after
successful processing, using the latest receipt handle for that message.
Messages are available again after their visibility timeout if left undeleted.

Consumers polling the same queue compete for messages, so each consumer will
receive only some of them. To give developers independent copies, use separate
queues with SNS subscriptions to the same topic and publish through SNS, or
run this stack separately on each developer's machine and publish a sample to
each stack.

A developer's consumer on the same host can connect with these settings:

- AWS endpoint: `http://localhost:14566`
- Queue URL: `http://localhost:14566/000000000000/analytics_message_samples_queue`
- Region: `eu-west-2`
- Access key and secret key: `test` / `test`

The endpoint binds to `127.0.0.1`; another machine cannot connect to it. A
consumer container attached to `waste-obligations-analytics-messages_default`
can use `http://floci:4566` and the queue URL shown in the CLI commands instead.
Set `ANALYTICS_MESSAGES_PORT` on Compose commands to change the host port, and
use the same port in the host consumer's settings and the tool's host
`--queue-url`.

For example, to use port `24566` in a POSIX shell, set it before starting the
stack and keep it set for subsequent Compose commands:

```sh
export ANALYTICS_MESSAGES_PORT=24566
docker compose -f tools/analytics-messages/compose.yml up -d --wait floci
docker compose -f tools/analytics-messages/compose.yml run --rm floci-init
```

Use `http://localhost:24566` and the corresponding host queue URL in the
consumer and host tool. The container commands still use `http://floci:4566`.
A port change does not move an existing consumer's connection automatically.

In PowerShell, replace environment assignments such as
`MESSAGE_DIRECTORY="..." docker compose ...` with a separate assignment before
the Compose command:

```powershell
$env:MESSAGE_DIRECTORY = "$($PWD.Path)/tools/analytics-messages/output-compressed"
# Run the SNS publish command above, using backticks for line continuations.
# Remove the override when returning to the default output directory:
Remove-Item Env:MESSAGE_DIRECTORY
```

Use `$env:ANALYTICS_MESSAGES_PORT = '24566'` for the port override in PowerShell.

To share with a developer on another machine, send the three generated files
for an offline fixture, or have them place the body and attribute files in
their checkout's empty `tools/analytics-messages/output` directory and run
the startup and publish commands on their own machine. The loopback queue
endpoint shown here is reachable only on the machine running the stack.

Stop the stack and discard its messages:

```sh
docker compose -f tools/analytics-messages/compose.yml down -v --remove-orphans
```

Generated files remain on disk after teardown. The Floci stack has no
persistent message volume, so restart it, initialise it and publish again for
another queue test.

## Troubleshooting

| Symptom | Action |
| --- | --- |
| `dotnet` is missing, or no compatible SDK is found | Choose Docker or a published copy above. A runtime-only installation cannot build the source project. |
| A published DLL asks for `Microsoft.NETCore.App 10.0` | Install the .NET 10 runtime, or request a self-contained copy for the host. |
| A published executable reports the wrong executable format | Check the receiving host's OS and CPU against the runtime identifier used when publishing. |
| The output directory must be empty | Choose a new directory or remove the previous generated files. `mkdir -p` does not empty an existing directory. |
| Invalid JSON, invalid UTF-8, or a non-object input | Supply a UTF-8 file containing one JSON object, rather than a queue receive response array or compressed body. |
| The message still exceeds the SNS size limit | Reduce the input payload; forcing compression does not bypass the encoded-body limit. |
| Docker cannot connect or mount a path | Start the Docker engine, enable Linux containers, create the output directory and allow file sharing for the mounted paths. |
| Image pulls or dependency restores fail | Check network/proxy access to Microsoft's container registry and NuGet, or use an already-published copy. |
| Port `14566` is already in use | Set `ANALYTICS_MESSAGES_PORT` before startup and update the host consumer's endpoint and queue URL. |
| `--queue-url` is rejected | Use an HTTP queue URL with a queue path and host `localhost`, a loopback IP, or `floci`. Remote hosts, HTTPS, credentials, query strings and fragments are rejected. |
| Files were generated but the tool could not send them | Check Floci is running and `floci-init` succeeded. Use `localhost:14566` from the host or `floci:4566` on the Compose network. Inspect the queue before retrying after a timeout, and use an empty output directory for another tool run. Existing files can be sent through SNS without regenerating them. |
| SNS publish cannot find `/messages/message-body.txt` | Generate files first and check `MESSAGE_DIRECTORY`; it must point to the folder containing the body and attribute files. |
| The queue is empty after publishing | Check that `floci-init` completed, the queue URL or SNS topic ARN matches the example, and another consumer has not received or deleted the message. |
| A second receive returns no message | The first receive may have hidden it. Wait for visibility timeout, or delete the processed message and publish another sample. |
| A compressed body is being parsed as JSON | Request message attributes and decode `gzip+base64` before parsing. |

To inspect local queue setup failures:

```sh
docker compose -f tools/analytics-messages/compose.yml ps -a
docker compose -f tools/analytics-messages/compose.yml logs floci
docker compose -f tools/analytics-messages/compose.yml run --rm floci-init
```

## Check the tool

```sh
dotnet build tools/analytics-messages/analytics-messages.slnx
dotnet test --test-modules tools/analytics-messages/tests/AnalyticsMessages.Tests/bin/Debug/net10.0/AnalyticsMessages.Tests.dll --no-build
```

These developer-only files do not change application behaviour or journey
contracts, so the shared journey suite needs no update.
