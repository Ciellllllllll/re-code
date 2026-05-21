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
