# Contributing to UnlimitedPDF

Thank you for helping improve UnlimitedPDF. This page covers what a pull request needs.

## Before you start: sign the CLA

Every contributor accepts the [Contributor License Agreement](CLA.md) once, on their first pull request. A check on the pull request asks you to sign: add a comment of its own that says exactly

```
I have read the CLA Document and I hereby sign the CLA
```

The pull request can be merged once every author of its commits has signed.

## Opening a pull request

1. Fork the repository and create a branch from `main`.
2. Keep each pull request to one change.
3. In the description, say what changed and why.
4. Make sure the build and the tests pass.

## Build and test

```
dotnet build UnlimitedPDF.slnx
dotnet test UnlimitedPDF.Tests
```

## Code of conduct

Everyone taking part follows the [Code of Conduct](CODE_OF_CONDUCT.md).

## Reporting a security issue

Email [support@unlimitedpdf.com](mailto:support@unlimitedpdf.com) instead of opening a public issue, so the problem can be fixed before it is known.
