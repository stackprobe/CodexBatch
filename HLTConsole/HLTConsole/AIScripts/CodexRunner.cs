using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using HLTStudio.Commons;

namespace HLTStudio.AIScripts
{
	internal static class CodexRunner
	{
		private const int TimeoutMilliseconds = 30 * 60 * 1000;
		private static readonly Encoding Utf8 = new UTF8Encoding(false);

		public static string Run(string workingDirectory, string prompt, string sessionId)
		{
			const string options = "--json --skip-git-repo-check -c sandbox_mode=workspace-write";
			string arguments = sessionId == null
				? "exec " + options + " -"
				: "exec resume " + options + " " + Guid.Parse(sessionId).ToString("D") + " -";

			var startInfo = new ProcessStartInfo
			{
				FileName = FindExecutable(),
				Arguments = arguments,
				WorkingDirectory = workingDirectory,
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				StandardOutputEncoding = Utf8,
				StandardErrorEncoding = Utf8,
			};

			using (Process process = Process.Start(startInfo))
			{
				var elapsed = Stopwatch.StartNew();

				try
				{
					Task<string> stderr = process.StandardError.ReadToEndAsync();
					Task<Events> stdout = Task.Run(() => ReadEvents(process.StandardOutput));
					Task stdin = Task.Run(() => WritePrompt(process.StandardInput, prompt));

					WaitFor(stdout, elapsed);
					if (!process.WaitForExit(RemainingMilliseconds(elapsed)))
						throw Timeout();

					WaitFor(stderr, elapsed);
					Events events = stdout.Result;
					string diagnostics = stderr.Result.Trim();

					if (process.ExitCode != 0)
						throw new InvalidOperationException("Codex が異常終了しました。終了コード " + process.ExitCode + Environment.NewLine +
							(diagnostics.Length == 0 ? events.Error : diagnostics));

					WaitFor(stdin, elapsed);

					if (!events.Completed)
						throw new InvalidOperationException("Codex の完了イベントを確認できませんでした。 " + events.Error);
					if (events.SessionId == null)
						throw new InvalidOperationException("Codex のセッション ID を確認できませんでした。");

					if (diagnostics.Length != 0)
						ProcMain.WriteLog(diagnostics);

					return events.SessionId;
				}
				catch
				{
					try
					{
						if (!process.HasExited)
							process.Kill();
					}
					catch (InvalidOperationException)
					{
						// 終了確認の直後にプロセスが終了することがある。
					}
					throw;
				}
			}
		}

		private static void WritePrompt(StreamWriter input, string prompt)
		{
			using (input)
			{
				byte[] bytes = Utf8.GetBytes(prompt);
				input.BaseStream.Write(bytes, 0, bytes.Length);
			}
		}

		private static string FindExecutable()
		{
			string architecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") ??
				Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
			string npmArchitecture = string.Equals(architecture, "ARM64", StringComparison.OrdinalIgnoreCase) ? "arm64" : "x64";

			foreach (string entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
			{
				string directory = entry.Trim().Trim('"');
				if (!Path.IsPathRooted(directory))
					continue;

				string executable = Path.Combine(directory, "codex.exe");
				if (File.Exists(executable))
					return executable;

				// npm の .cmd を経由せず、Windows 用の実行ファイルを直接起動する。
				string package = Path.Combine(directory, @"node_modules\@openai\codex");
				string platformPackage = "codex-win32-" + npmArchitecture;
				string vendorPath = Path.Combine("vendor", npmArchitecture + "-pc-windows-msvc", "bin", "codex.exe");
				string installed = Path.Combine(package, @"node_modules\@openai", platformPackage, vendorPath);
				if (File.Exists(installed))
					return installed;

				installed = Path.Combine(directory, @"node_modules\@openai", platformPackage, vendorPath);
				if (File.Exists(installed))
					return installed;

				installed = Path.Combine(package, vendorPath);
				if (File.Exists(installed))
					return installed;
			}

			throw new FileNotFoundException("PATH から Codex CLI を見つけられませんでした。", "codex.exe");
		}

		private static void WaitFor(Task task, Stopwatch elapsed)
		{
			if (Task.WaitAny(new[] { task }, RemainingMilliseconds(elapsed)) < 0)
				throw Timeout();

			task.GetAwaiter().GetResult();
		}

		private static int RemainingMilliseconds(Stopwatch elapsed)
		{
			return (int)Math.Max(0, TimeoutMilliseconds - elapsed.ElapsedMilliseconds);
		}

		private static TimeoutException Timeout()
		{
			return new TimeoutException("Codex のプロンプト実行が 30 分でタイムアウトしました。");
		}

		private static Events ReadEvents(TextReader output)
		{
			var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
			var events = new Events();
			string line;

			while ((line = output.ReadLine()) != null)
			{
				if (string.IsNullOrWhiteSpace(line))
					continue;

				var data = serializer.Deserialize<Dictionary<string, object>>(line);
				switch (GetString(data, "type"))
				{
					case "thread.started":
						Guid id;
						if (!Guid.TryParse(GetString(data, "thread_id"), out id))
							throw new InvalidOperationException("Codex のセッション ID が正しくありません。");
						events.SessionId = id.ToString("D");
						break;

					case "turn.completed":
						events.Completed = true;
						break;

					case "turn.failed":
						object error;
						if (data.TryGetValue("error", out error))
							events.Error = GetString(error as Dictionary<string, object>, "message") ?? events.Error;

						throw new InvalidOperationException("Codex のターンが失敗しました。 " + events.Error);

					case "error":
						events.Error = GetString(data, "message");
						break;

					case "item.completed":
						object value;
						if (data.TryGetValue("item", out value))
						{
							var item = value as Dictionary<string, object>;
							if (GetString(item, "type") == "agent_message")
								ProcMain.WriteLog(GetString(item, "text"));
						}
						break;
				}
			}

			return events;
		}

		private static string GetString(Dictionary<string, object> data, string key)
		{
			object value;
			return data != null && data.TryGetValue(key, out value) ? value as string : null;
		}

		private sealed class Events
		{
			public string SessionId;
			public bool Completed;
			public string Error;
		}
	}
}
