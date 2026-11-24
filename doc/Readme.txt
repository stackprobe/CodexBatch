CodexBatch
==========

CodexBatch は、OpenAI の Codex CLI に渡す指示をテキストファイルにまとめ、
コマンド一つで実行できる Windows 向けツールです。
Codex への指示は自然な文章で書き、繰り返しや条件分岐はスクリプトで指定します。

■ 実行環境

Windows、.NET Framework 4.8、Windows 版の Codex CLI が必要です。
Codex CLI をインストールし、認証を済ませてください。
CodexBatch は PATH とユーザー用の npm フォルダから codex.exe を探します。

■ はじめて使う

次の3行を、CodexBatch.exe と同じフォルダに example.txt として保存します。
文字コードは UTF-8（BOM 付き）または CP932（Windows の Shift_JIS）です。

<
    東京の名物を1つ挙げ、1文で紹介してください。
>

CodexBatch.exe があるフォルダで PowerShell を開き、次を実行します。

    .\CodexBatch.exe .\example.txt

Codex への指示と応答がコンソールに表示されます。
スクリプトを別のフォルダに置く場合は、そのファイルのパスを渡してください。
パスに空白が含まれる場合は二重引用符で囲みます。

    .\CodexBatch.exe "C:\My Scripts\example.txt"

Codex の作業フォルダは、スクリプトファイルを置いたフォルダです。
プロンプトに「result.txt に保存してください」と書くと、そのフォルダを
基準にしたパスで Codex に保存を依頼できます。

■ スクリプトの書き方

CodexBatch のスクリプト書式を AiScript と呼びます。
  < と >   Codex に渡す指示を囲みます。境界記号はそれぞれ単独の行に書きます。
  foreach  指定した文字列や、テキストファイルの各行について繰り返します。
  for      指定した整数範囲について繰り返します。
  if       ファイルの有無に応じて処理を切り替えます。
  session  複数の指示で同じ Codex セッションを使います。
  writable 既存のフォルダを、後続の Codex 呼び出しの書き込み先に追加します。

■ 同梱サンプル

Sample\AllFeatures.txt には、繰り返し、条件分岐、セッション共有などの例があります。
実行前に Sample\AllFeatures_Caution.txt を読み、C:\temp が存在することを確認してください。
このサンプルは C:\temp\CodexBatchTest を中身ごと削除し、作り直します。

    .\CodexBatch.exe .\Sample\AllFeatures.txt

詳しい構文と使用例は、GitHub リポジトリの spec/Script_Syntax.md にあります。
https://github.com/stackprobe/CodexBatch
