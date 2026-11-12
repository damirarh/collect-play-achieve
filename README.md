# Collect Play Achieve

## Docker Sandbox

The local [Docker Sandboxes](https://docs.docker.com/ai/sandboxes/) environment
adds .NET 11, Node v26.13.0, pnpm 10.34.9, and Aspire CLI 13.6.5 to Copilot.
It uses a **v3 mixin**, compatible with the `copilot` shortcut as resolved by
`sbx` v0.51.0. Some Docker documentation still describes that shortcut as v2;
do not combine this mixin with a v2 workload.

From the repository root on the host:

```sh
sbx env run ./sbxenv.yaml
```

Approve the environment and use Docker's normal Copilot credential setup.
The first creation builds the toolchain; subsequent runs attach to the same
sandbox. The root environment file is mounted read-only, and it contains no
host lifecycle commands or secrets.

Alternatively, without environment-file orchestration:

```sh
sbx run copilot . --name collect-play-achieve \
  --kit ./sandbox/cpa --publish 8080 --publish 15075 \
  --env UseArtifactsOutput=true \
  --env ArtifactsPath=/home/agent/.cache/collect-play-achieve/artifacts \
  --env npm_config_store_dir=/home/agent/.cache/pnpm/store
```

### Restore and check

Inside the sandbox, from the repository root:

```sh
dotnet tool restore
CI=true pnpm --dir web install --frozen-lockfile
dotnet build api/CollectPlayAchieve.slnx
(cd api && dotnet test --project CollectPlayAchieve.Api.Tests)
dotnet csharpier check ./api
pnpm --dir web lint
pnpm --dir web format:check
pnpm --dir web build
```

Workspace edits and frontend dependencies are shared with the host. .NET build
artifacts stay under the sandbox's `/home/agent/.cache/collect-play-achieve/artifacts`
to avoid sharing stale `bin`/`obj` files between host and sandbox builds.
pnpm's store stays under `/home/agent/.cache/pnpm/store`.
`CI=true` lets pnpm reinstall dependencies without a terminal prompt when its
host and sandbox stores differ. Git hook installation is intentionally manual
because it changes the host-mounted Git repository.

### Run the full app

Inside the sandbox:

```sh
aspire start --non-interactive \
  --apphost api/CollectPlayAchieve.AppHost --launch-profile sandbox
aspire wait api
aspire wait web
aspire wait gateway
aspire describe
```

The opt-in `sandbox` profile uses HTTP without host certificate setup. The
gateway serves the frontend and `/api/...` on sandbox port 8080. The dashboard
uses port 15075 and retains its login-token authentication.

On the host, discover the loopback mappings:

```sh
sbx ports collect-play-achieve
```

Open the host port mapped to 8080 for the app and the port mapped to 15075 for
the dashboard. Use the dashboard login token reported by Aspire; the
sandbox-internal dashboard URL itself is not the host-facing address.
Host ports are allocated dynamically and can change after a sandbox restart.
To choose a fixed mapping later, use `sbx ports collect-play-achieve --publish 8080:8080`.

Keep a Copilot session attached while developing. To keep the sandbox running
after disconnecting, run `sbx run --detach --name collect-play-achieve` on the
host. Stop the AppHost inside the sandbox:

```sh
aspire stop --non-interactive --apphost api/CollectPlayAchieve.AppHost
```

Then stop the sandbox on the host with `sbx stop collect-play-achieve`.
YARP uses the sandbox's private Docker daemon; no host Docker socket is needed.
Normal local Aspire launch profiles are unchanged.

### Updating and troubleshooting

Tools persist across sandbox restarts. To change the mixin, create a sandbox
with a new name, for example `sbx env run --name collect-play-achieve-new ./sbxenv.yaml`;
reattaching does not update an existing sandbox's kit.

Tool defaults are in `sandbox/cpa/cpa.yaml`. Keep the Node default aligned
with `web/.node-version`, pnpm compatible with `web/pnpm-lock.yaml`, and Aspire
aligned with the AppHost SDK. The SDK default follows the latest stable .NET
10.0 channel when the installation layer is rebuilt; cached builds retain
their previously resolved SDK.

Validate the source kit with `sbx kit validate ./sandbox/cpa` (requires the
kit builder; `--offline` does not build v3 source kits). Inspect blocked runtime
requests with `sbx policy log collect-play-achieve`; add only required domains
to the mixin rather than disabling network isolation. Downloads during the
image build use the builder's network. Organization policies can still restrict
kit loading and runtime access.

## Git hooks

This repository uses [Husky.NET](https://alirezanet.github.io/Husky.Net/) to manage Git hooks.

To install the hooks locally, run:

```sh
dotnet tool restore
dotnet husky install
```

The pre-commit hook runs:

- CSharpier (`./api`)
- Prettier (`./web`)
- ESLint (`./web`)
