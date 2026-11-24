# CodexBatch

Codex に何度も頼む作業を、1本のスクリプトにまとめられます。CodexBatch は、OpenAI の Codex CLI を使った繰り返し作業を自動化する Windows 向けツールです。

複数の資料に同じ指示を出す、入力ファイルがあるときだけ処理する、調査の続きを同じセッションで要約する。こうした手順を、普段スクリプトを書く感覚でテキストファイルに記述できます。Codex への指示は自然な文章で書き、繰り返しや条件分岐は `foreach` や `if` で組み立てます。

編集はいつものテキストエディターで。実行はターミナルから、スクリプトのパスを渡すだけです。同じ作業をもう一度行うときも、同じコマンドで実行できます。

## たとえば、都市ごとの紹介文を作る

CodexBatch のスクリプト書式 **AiScript** では、`<` と `>` の間に Codex への指示を書きます。この例では、`${city}` に東京、大阪が順に入り、都市ごとに指示を実行します。

```text
foreach city : "東京", "大阪"
{
    <
        ${city} の名物を1つ挙げ、1文で紹介してください。
    >
}
```

都市名の一覧をテキストファイルから読ませることもできます。回数が決まっている作業には `for`、ファイルの有無に応じた処理には `if` を使えます。やり取りを引き継いで次の指示を出したいときは、まとめたい処理を `session` ブロックに入れます。

## はじめて使う

1. Windows に .NET Framework 4.8 と Codex CLI の Windows 版を用意し、Codex CLI の認証を済ませます。CodexBatch は `PATH` とユーザー用の npm フォルダから `codex.exe` を探します。
2. ソースからビルドする場合は、Visual Studio 2022 と .NET Framework 4.8 のターゲティングパックを用意します。[HLTConsole.sln](HLTConsole/HLTConsole.sln) を開き、構成を `Release`、プラットフォームを `x86` にしてビルドします。
3. 上の例をリポジトリのルートに `example.txt` として保存します。文字コードは UTF-8（BOM 付き）または CP932（Windows の Shift_JIS）を使います。リポジトリのルートから PowerShell で次を実行します。

   ```powershell
   .\HLTConsole\HLTConsole\bin\Release\HLTConsole.exe .\example.txt
   ```

Codex の応答はコンソールに表示されます。Codex の作業フォルダと AiScript 内の相対パスの基準は、どちらもスクリプトを保存したフォルダです。

## 書式とサンプル

- [AiScript 書式仕様書](spec/Script_Syntax.md) で、構文の詳細と組み合わせ例を確認できます。
- [サンプルスクリプト](Sample/AllFeatures.txt) で、繰り返しや条件分岐、セッション共有の例を確認できます。このサンプルを実行すると、`C:\temp\CodexBatchTest` の中身が削除され、フォルダが作り直されます。
