using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HLTStudio.Commons;

namespace HLTStudio.AIScripts
{
	/// <summary>
	/// スクリプト
	/// </summary>
	public class AIScript
	{
		public string ScriptFilePath { get; private set; }
		private readonly string WorkingDirectory;
		private readonly Action<Execution> Body;

		/// <summary>
		/// スクリプトを読み込み、構文を検証する。
		/// </summary>
		/// <param name="file">スクリプトファイル</param>
		public AIScript(string file)
		{
			file = Path.GetFullPath(file);
			this.ScriptFilePath = file;
			this.WorkingDirectory = Path.GetDirectoryName(file);
			this.Body = new ScriptParser(file, ReadText(file)).Parse();
		}

		/// <summary>
		/// 実行
		/// </summary>
		public void Run()
		{
			this.Body(new Execution(this.WorkingDirectory));
		}

		internal static string ReadText(string file)
		{
			byte[] bytes = File.ReadAllBytes(file);

			if (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf)
				return new UTF8Encoding(false, true).GetString(bytes, 3, bytes.Length - 3);

			return Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetString(bytes);
		}

		internal sealed class Execution
		{
			private readonly string WorkingDirectory;
			private readonly HashSet<string> WritableDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			private bool InSession;
			private string SessionId;

			public readonly Dictionary<string, string> Variables = new Dictionary<string, string>(StringComparer.Ordinal);

			public Execution(string workingDirectory)
			{
				this.WorkingDirectory = workingDirectory;
			}

			public string ResolvePath(string path)
			{
				return Path.Combine(this.WorkingDirectory, path);
			}

			public void DeletePath(string path)
			{
				SCommon.DeletePath(this.ResolveDeletionPath(path));
			}

			public void ResetDirectory(string path)
			{
				string directory = this.ResolveDeletionPath(path);

				if (Directory.Exists(directory))
					SCommon.DeletePath(directory);

				Directory.CreateDirectory(directory);
			}

			public void AddWritableDirectory(string path)
			{
				string directory = Path.GetFullPath(this.ResolvePath(path));

				if (!Directory.Exists(directory))
					throw new DirectoryNotFoundException("書き込み先のフォルダが見つかりません。パス：" + directory);

				this.WritableDirectories.Add(directory);
			}

			private string ResolveDeletionPath(string path)
			{
				string fullPath = Path.GetFullPath(this.ResolvePath(path));
				string root = Path.GetPathRoot(fullPath);
				char[] separators = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

				if (string.Equals(fullPath.TrimEnd(separators), root.TrimEnd(separators), StringComparison.OrdinalIgnoreCase))
					throw new IOException("ルートディレクトリは削除できません。パス：" + fullPath);

				return fullPath.TrimEnd(separators);
			}

			public string[] ReadLines(string path)
			{
				var lines = new List<string>();
				using (var reader = new StringReader(ReadText(this.ResolvePath(path))))
				{
					string line;
					while ((line = reader.ReadLine()) != null)
						lines.Add(line);
				}
				return lines.ToArray();
			}

			public void RunLoop(string name, IEnumerable<string> values, Action<Execution> body)
			{
				string previous;
				bool shadowed = this.Variables.TryGetValue(name, out previous);

				try
				{
					foreach (string value in values)
					{
						this.Variables[name] = value;
						body(this);
					}
				}
				finally
				{
					if (shadowed)
						this.Variables[name] = previous;
					else
						this.Variables.Remove(name);
				}
			}

			public void RunSession(Action<Execution> body)
			{
				this.InSession = true;
				try
				{
					body(this);
				}
				finally
				{
					this.InSession = false;
					this.SessionId = null;
				}
			}

			public void RunPrompt(string prompt)
			{
				string sessionId = CodexRunner.Run(this.WorkingDirectory, prompt, this.SessionId, this.WritableDirectories.Where(Directory.Exists));
				if (this.InSession)
					this.SessionId = sessionId;
			}
		}
	}
}
