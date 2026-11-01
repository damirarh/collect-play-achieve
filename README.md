# Collect Play Achieve

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
