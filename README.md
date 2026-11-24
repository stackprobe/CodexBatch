# CodexBatch

CodexBatch は、OpenAI の Codex CLI に渡す複数のプロンプトを、1つのテキストファイルにまとめて順番に実行する Windows 向けツールです。項目ごとに同じ指示を繰り返したり、入力ファイルがあるときだけ処理を進めたりできます。

## できること

- `<` と `>` で囲んだ自然言語の指示を Codex に渡します。
- `foreach` で文字列の一覧やテキストファイルの各行を処理し、`for` で指定した整数範囲を繰り返します。
- `if` でファイルの有無に応じて処理を切り替えます。
- `session` で前のやり取りを引き継ぎ、調査から要約までのような複数の指示を同じ Codex セッションで実行します。

## はじめて使う

### 1. 実行環境を準備する

- Windows と .NET Framework 4.8 が必要です。
- Codex CLI の Windows 版をインストールし、認証を済ませてください。CodexBatch は `PATH` とユーザー用の npm フォルダから `codex.exe` を探します。
- ソースからビルドする場合は、Visual Studio 2022 と .NET Framework 4.8 のターゲティングパックを用意します。[HLTConsole.sln](HLTConsole/HLTConsole.sln) を開き、構成を `Release`、プラットフォームを `x86` にしてビルドすると、`HLTConsole\HLTConsole\bin\Release\HLTConsole.exe` が生成されます。

### 2. スクリプトを書く

次の内容を `example.txt` に保存します。スクリプトの文字コードは UTF-8（BOM 付き）または CP932（Windows の Shift_JIS）です。CodexBatch では、この書式のスクリプトを **AiScript** と呼びます。

```text
foreach city : "東京", "大阪"
{
    <
        ${city} の名物を1つ挙げ、1文で紹介してください。
    >
}
```

`${city}` に東京、大阪が順に入り、Codex に2回指示を渡します。この例では、指示ごとに新しいセッションを使います。やり取りを引き継ぎたい場合は、まとめたい処理を `session` ブロックに入れます。

### 3. 実行する

リポジトリのルートに `example.txt` を置き、PowerShell で次を実行します。

```powershell
.\HLTConsole\HLTConsole\bin\Release\HLTConsole.exe .\example.txt
```

Codex の応答はコンソールに表示されます。Codex の作業フォルダと、AiScript 内で使う相対パスの基準は、どちらもスクリプトを保存したフォルダです。

## 書式とサンプル

- [AiScript 書式仕様書](spec/Script_Syntax.md) で、構文の詳細と組み合わせ例を確認できます。
- [サンプルスクリプト](Sample/AllFeatures.txt) で、繰り返しや条件分岐、セッション共有の例を確認できます。このサンプルを実行すると、`C:\temp\CodexBatchTest` の中身が削除され、フォルダが作り直されます。
