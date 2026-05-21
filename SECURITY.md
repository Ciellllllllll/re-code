# Security Policy

## Supported Releases

Security fixes are intended for the latest `v0.9.x` release line published from this repository.

## Reporting A Vulnerability

Please avoid opening a public issue for undisclosed vulnerabilities.

Preferred channel:

- GitHub Private Vulnerability Reporting for this repository, if enabled

If private reporting is unavailable, open a minimal public issue asking for a secure contact path and do not include exploit details, secrets, or proof-of-concept payloads.

Do not disclose API keys in issues. Do not paste private source code into public issues.

## Release Trust Model

- Release VSIX artifacts are produced by GitHub Actions
- Release workflows generate GitHub artifact attestations
- Release notes include checksum and `gh attestation verify` instructions
- Release assets include `SHA256SUMS.txt`
- VSIX packages are currently unsigned

This unsigned release policy is intended for current friend/community distribution. Code signing may be reconsidered if distribution expands beyond that scope or if Visual Studio Marketplace publication becomes a target.
