# CivicFlow Release Verification

## Current status

CivicFlow currently publishes source code through a public Git repository, but it does not yet publish an official signed release artifact or a repository-held signing public key. The repository therefore provides project identity and provenance metadata, not a cryptographic claim that the source cannot be copied or modified.

The non-secret identity manifest is [`CIVICFLOW-PROVENANCE.json`](../CIVICFLOW-PROVENANCE.json). The API assembly also contains CivicFlow product, owner, repository, and version metadata.

## Recommended release workflow

When an official release process is established, create the release artifact and detached signature outside the repository working tree:

```bash
VERSION=0.1.0
dotnet publish PublicServiceCaseManagement.Api/PublicServiceCaseManagement.Api.csproj \
  --configuration Release \
  --output "artifacts/civicflow-api-$VERSION"

tar -czf "artifacts/civicflow-api-$VERSION.tar.gz" \
  -C "artifacts/civicflow-api-$VERSION" .

sha256sum "artifacts/civicflow-api-$VERSION.tar.gz" \
  > "artifacts/civicflow-api-$VERSION.tar.gz.sha256"

gpg --armor --detach-sign \
  --output "artifacts/civicflow-api-$VERSION.tar.gz.asc" \
  "artifacts/civicflow-api-$VERSION.tar.gz"
```

The GPG private key must remain in an external key store or signing environment. It must not be placed in the repository, appsettings files, the application binary, logs, or the published artifact.

Publish the artifact, checksum, detached signature, and the signing public key through the chosen release channel. Record the public-key fingerprint in release documentation through an independently trusted channel before treating signatures as authoritative.

Verification by a consumer:

```bash
gpg --import civicflow-release-signing-public-key.asc
gpg --fingerprint
gpg --verify civicflow-api-0.1.0.tar.gz.asc civicflow-api-0.1.0.tar.gz
sha256sum --check civicflow-api-0.1.0.tar.gz.sha256
```

The commands above are a documented manual workflow, not an active signing pipeline. No release is signed automatically by the current CI workflow.

## What verification proves

A valid detached signature verifies that the artifact was signed by the private key corresponding to the published public key. A checksum verifies artifact integrity. Neither prevents copying, proves exclusive authorship of every line, or replaces copyright, licensing, or provenance records.
