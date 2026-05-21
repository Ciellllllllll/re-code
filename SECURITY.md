# Security Policy

## Supported Releases

Security fixes are intended for the latest release line published from this repository.

## Reporting A Vulnerability

Please avoid opening a public issue for undisclosed vulnerabilities.

Preferred channel:

- GitHub Private Vulnerability Reporting for this repository, if enabled

If private reporting is unavailable, open a minimal public issue asking for a secure contact path and do not include exploit details, secrets, or proof-of-concept payloads.

## Supply Chain Notes

- Release VSIX artifacts are produced by GitHub Actions
- Release workflows generate GitHub artifact attestations
- Release notes include checksum and `gh attestation verify` instructions
- Tagged releases are expected to be code-signed when signing secrets are configured
