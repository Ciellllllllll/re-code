# Privacy Policy

## Overview

`re:code` is a local Visual Studio extension. When a completion provider is configured, the extension sends a limited portion of editor context to that provider to generate code completion candidates.

## Data Sent To Completion Providers

The extension may send the following data to the configured provider:

- File path
- Current line prefix already typed by the user
- Prefix context before the caret
- Suffix context after the caret
- Provider name and model name selected by the user

The extension does not intentionally send the entire solution or repository. It sends only the context window collected around the active caret position.

## Excluded Paths

The extension skips requests for files that match the built-in security filter, including:

- `.env`
- `.env.*`
- `secrets.*`
- `credentials.*`
- `*.key`
- `*.pem`
- `*.pfx`
- `*.cer`
- `*.crt`

The extension also skips files under these directories:

- `.git`
- `.vs`
- `build`
- `out`
- `x64`
- `x86`
- `Debug`
- `Release`
- `.vscode`
- `node_modules`
- `vcpkg_installed`

## Masking

Before context is sent, the extension masks lines that match built-in secret patterns such as:

- `API_KEY`
- `SECRET`
- `TOKEN`
- `PASSWORD`
- `PRIVATE_KEY`
- `ACCESS_TOKEN`
- `CLIENT_SECRET`
- `Bearer`

Masked matches are replaced with `***`.

## Limits Of Masking

Masking is heuristic and pattern-based. It does not guarantee full secret removal.

Examples of data that may bypass masking:

- Secrets that do not match the built-in patterns
- Secrets split across multiple lines
- Encoded or transformed credentials
- Sensitive identifiers embedded in ordinary source code
- File paths containing sensitive information

Do not use `re:code` on files that contain secrets, credentials, or regulated data unless you have independently confirmed that sending the collected context is acceptable.

## Logging

The extension is designed not to log API keys, full prompts, full source code, or full provider responses.

Diagnostic logging may include safe operational metadata such as:

- Provider name
- Model name
- Request ID
- Completion mode
- File name
- Latency
- Request URL host and path

## Third-Party Providers

When you use a cloud provider, requests are processed according to that provider's terms and privacy practices. Review the provider's policy before use.
