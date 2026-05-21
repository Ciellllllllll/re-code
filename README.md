# re:code

## 概要

`re:code` は Visual Studio 2022 向けの VSIX 拡張機能です。  
C / C++ の編集中に AI 補完候補を Ghost Text としてエディター上へ表示し、`Tab` で採用できます。

主な用途:

- C / C++ コード補完の支援
- Ghost Text による軽量な補完提示
- DeepSeek などの OpenAI 互換 API を使った補完

## インストール方法

1. GitHub Releases から最新の `.vsix` をダウンロードします。
2. Visual Studio 2022 を終了します。
3. ダウンロードした `.vsix` をダブルクリックします。
4. VSIX Installer の内容を確認してインストールします。
5. Visual Studio 2022 を起動します。
6. `ツール > オプション > re:code` を開き、使用する Provider、Model、API Key を設定します。
7. 初期状態では自動補完は `OFF` です。必要な場合だけ有効化してください。

## Security Notice

- 現在の VSIX は未署名です。
- 現時点では、友人や小規模コミュニティ向けの配布として未署名 VSIX を前提にしています。
- 公式の Release asset は GitHub Actions で生成されます。
- ランダムな第三者ミラーや、出所が確認できない VSIX はインストールしないでください。
- インストール前に SHA-256 と GitHub Artifact Attestation を確認してください。

## Release Verification

SHA-256 の確認:

```powershell
Get-FileHash .\<VSIX_FILE_NAME> -Algorithm SHA256
```

GitHub Artifact Attestation の確認:

```powershell
gh attestation verify .\<VSIX_FILE_NAME> -R Ciellllllllll/re-code
```

Release に含まれる `SHA256SUMS.txt` と、手元で計算した SHA-256 が一致することを確認してください。

## Privacy / Data Transmission Summary

- オンライン Provider を使う場合、補完に必要な編集中コンテキストが選択中 Provider へ送信されることがあります。
- 送信される可能性があるデータは、現在の編集コンテキスト、設定に応じた周辺 prefix / suffix 行、選択中 Provider / Model 情報、補完リクエストに必要な prompt です。
- API Key、リポジトリ全体、無関係なファイルは送信対象ではありません。
- Provider / Model / API Key が未設定の場合、オンライン補完リクエストは送信されません。
- 自動補完は初期状態で `OFF` です。
- ログには API Key、ソースコード本文全文、prompt 全文、AI 応答全文を出さない方針です。

## 動作要件

- IDE:
  Visual Studio 2022 Community / Professional / Enterprise
- 対象アーキテクチャ:
  64-bit Visual Studio 2022
- OS:
  Visual Studio 2022 がサポートする Windows 環境
- ランタイム:
  .NET Framework 4.8
- 対応言語:
  C / C++
- インストール形式:
  VSIX
- API 利用:
  クラウド系 Provider を使う場合は、対応する API Key が必要
- 通信:
  オンライン Provider を使う場合は対象 API endpoint へ到達できるネットワーク接続が必要
- 未設定時の挙動:
  Provider 未設定、または API Key 必須 Provider で API Key 未設定の場合、補完通信は実行されません
- 送信されるデータ:
  補完要求時には、ファイルパス、カーソル位置周辺の prefix / suffix、現在行の prefix をプロバイダーへ送信します
- 除外対象:
  `.env`、`.env.*`、`secrets.*`、`credentials.*`、`.key`、`.pem`、`.pfx`、`.cer`、`.crt` に加え、`.git`、`.vs`、`build`、`out`、`x64`、`x86`、`Debug`、`Release`、`.vscode`、`node_modules`、`vcpkg_installed` 配下のファイルは収集対象から外します
- マスク:
  `API_KEY`、`SECRET`、`TOKEN`、`PASSWORD`、`PRIVATE_KEY`、`ACCESS_TOKEN`、`CLIENT_SECRET`、`Bearer` などを含む行は送信前にパターンベースでマスクします
- マスクの限界:
  正規表現に一致しない秘密情報、分割された値、独自形式の資格情報、ファイルパス自体は完全には保護できません。機密コードや秘密情報を含むファイルでは利用しないでください

## ローカル検証

Experimental Instance での検証:

```powershell
.\scripts\dev-exp.ps1 -Launch
```

Release VSIX の生成確認:

```powershell
.\scripts\build-release.ps1
```

通常ビルド確認:

```powershell
dotnet build GhostText.sln
```
