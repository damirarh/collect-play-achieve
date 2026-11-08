# Project sandbox tooling

The sandbox includes the .NET 10 SDK, Node, pnpm, and Aspire CLI. Use its
private Docker daemon for container-backed resources; never mount the host
Docker socket or copy host credentials into the sandbox.

From the repository root, restore local tools with `dotnet tool restore`.
Install frontend dependencies with `CI=true pnpm --dir web install --frozen-lockfile`.
Do not automatically install Git hooks: this workspace is shared with the host.

Start the full app with
`aspire start --non-interactive --apphost api/CollectPlayAchieve.AppHost --launch-profile sandbox`.
Wait for the `api`, `web`, and `gateway` resources with `aspire wait` before using
them. The HTTP gateway listens on sandbox port 8080; the authenticated dashboard
listens on port 15075. Publish/discover their host mappings with `sbx ports`
on the host. The gateway routes `/` to the frontend and `/api/...` to the API.

Stop the app with
`aspire stop --non-interactive --apphost api/CollectPlayAchieve.AppHost`.
Build/check commands and port-forwarding instructions are in the root README.
