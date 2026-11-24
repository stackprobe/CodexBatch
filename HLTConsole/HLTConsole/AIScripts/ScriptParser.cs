using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Execution = HLTStudio.AIScripts.AIScript.Execution;

namespace HLTStudio.AIScripts
{
	internal sealed class ScriptParser
	{
		private const string Identifier = @"[A-Za-z_][A-Za-z0-9_]*";
		private static readonly Regex VariableReference = new Regex(@"\$?(?:\$\{(" + Identifier + @")\})");
		private static readonly Regex PathStatement = new Regex(@"^(delete|resetdir|writable)\s+(.+)\z");
		private static readonly Regex IfHeader = new Regex(@"^if\s*(\(.*\))\z");
		private static readonly Regex ForHeader = new Regex(@"^for\s+(" + Identifier + @")\s*:\s*([+-]?[0-9]+)\s*\.\.\.\s*([+-]?[0-9]+)\z");
		private static readonly Regex ForeachHeader = new Regex(@"^foreach\s+(" + Identifier + @")\s*:\s*(.+)\z");
		private static readonly Regex ForeachListPrefix = new Regex(@"^\s*foreach\s+" + Identifier + @"\s*:\s*\z");
		private static readonly Regex FileSource = new Regex(@"^FILE\s+(.+)\z");
		private static readonly Regex ListItem = new Regex(@"\G\s*""((?:[^""\\]|\\.)*)""\s*");
		private static readonly Regex PathToken = new Regex(@"^(?:""([^""]+)""|([^\s""()]+))\z");

		private readonly string FileName;
		private readonly MatchCollection Lines;
		private readonly HashSet<string> Variables = new HashSet<string>(StringComparer.Ordinal);
		private int Position;
		private int CommentStart;
		private bool InSession;

		public ScriptParser(string fileName, string source)
		{
			this.FileName = fileName;
			this.Lines = Regex.Matches(source, @"([^\r\n]*)(\r\n|\r|\n)|([^\r\n]+)\z");
		}

		public Action<Execution> Parse()
		{
			return this.ReadBlock(false);
		}

		private Action<Execution> ReadBlock(bool nested)
		{
			var statements = new List<Action<Execution>>();
			string line;

			while ((line = this.ReadCodeLine()) != null)
			{
				if (line == "}")
				{
					if (!nested)
						throw this.Error(this.Position, "対応する「{」がありません。");
					break;
				}

				statements.Add(this.ReadStatement(line, this.Position));
			}

			if (line == null && nested)
				throw this.Error(this.Position + 1, "ブロックを閉じる「}」がありません。");

			return execution =>
			{
				foreach (Action<Execution> statement in statements)
					statement(execution);
			};
		}

		private Action<Execution> ReadStatement(string line, int lineNumber)
		{
			if (line == "<")
				return this.ReadPrompt(lineNumber);

			if (VariableReference.IsMatch(line))
				throw this.Error(lineNumber, "変数参照はプロンプト内でのみ使用できます。");

			if (line == "session")
			{
				if (this.InSession)
					throw this.Error(lineNumber, "session を入れ子にできません。");

				this.InSession = true;
				Action<Execution> body = this.ReadBody();
				this.InSession = false;
				return execution => execution.RunSession(body);
			}

			Match match = PathStatement.Match(line);
			if (match.Success)
			{
				string path = ParsePath(match.Groups[2].Value, message => this.Error(lineNumber, message));

				switch (match.Groups[1].Value)
				{
					case "delete": return execution => execution.DeletePath(path);
					case "resetdir": return execution => execution.ResetDirectory(path);
					case "writable": return execution => execution.AddWritableDirectory(path);
				}
			}

			match = IfHeader.Match(line);
			if (match.Success)
			{
				var reader = new ConditionReader(match.Groups[1].Value, message => this.Error(lineNumber, message));
				Func<Execution, bool> condition = reader.Parse();
				Action<Execution> body = this.ReadBody();

				return execution =>
				{
					if (condition(execution))
						body(execution);
				};
			}

			match = ForHeader.Match(line);
			if (match.Success)
			{
				string name = match.Groups[1].Value;
				var start = BigInteger.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
				var end = BigInteger.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
				Action<Execution> body = this.ReadLoopBody(name);

				return execution => execution.RunLoop(name, EnumerateRange(start, end), body);
			}

			match = ForeachHeader.Match(line);
			if (match.Success)
			{
				string name = match.Groups[1].Value;
				Func<Execution, IEnumerable<string>> values = this.ReadForeachSource(match.Groups[2].Value, lineNumber);
				Action<Execution> body = this.ReadLoopBody(name);

				return execution => execution.RunLoop(name, values(execution), body);
			}

			throw this.Error(lineNumber, "構文を認識できません。");
		}

		private Action<Execution> ReadBody()
		{
			string opening = this.ReadCodeLine();
			if (opening != "{")
				throw this.Error(opening == null ? this.Position + 1 : this.Position, "ブロックを開く「{」が必要です。");

			return this.ReadBlock(true);
		}

		private Action<Execution> ReadLoopBody(string name)
		{
			bool added = this.Variables.Add(name);
			Action<Execution> body = this.ReadBody();
			if (added)
				this.Variables.Remove(name);
			return body;
		}

		private static IEnumerable<string> EnumerateRange(BigInteger start, BigInteger end)
		{
			int step = start <= end ? 1 : -1;

			for (BigInteger value = start; ; value += step)
			{
				yield return value.ToString(CultureInfo.InvariantCulture);
				if (value == end)
					yield break;
			}
		}

		private Func<Execution, IEnumerable<string>> ReadForeachSource(string source, int lineNumber)
		{
			Match file = FileSource.Match(source);
			if (file.Success)
			{
				string path = ParsePath(file.Groups[1].Value, message => this.Error(lineNumber, message));
				return execution => execution.ReadLines(path);
			}

			var values = new List<string>();
			int position = 0;

			while (true)
			{
				Match item = ListItem.Match(source, position);
				if (!item.Success)
					throw this.Error(lineNumber, "foreach の値には二重引用符付きの文字列が必要です。");

				values.Add(this.UnescapeItem(item.Groups[1].Value, lineNumber));
				position += item.Length;

				if (position == source.Length)
					return execution => values;

				if (source[position++] != ',')
					throw this.Error(lineNumber, "foreach の値はカンマで区切ってください。");
			}
		}

		private string UnescapeItem(string value, int lineNumber)
		{
			var result = new StringBuilder();

			for (int index = 0; index < value.Length; index++)
			{
				if (value[index] != '\\')
				{
					result.Append(value[index]);
					continue;
				}

				switch (value[++index])
				{
					case '"': result.Append('"'); break;
					case '\\': result.Append('\\'); break;
					case 'n': result.Append('\n'); break;
					case 'r': result.Append('\r'); break;
					case 't': result.Append('\t'); break;
					default: throw this.Error(lineNumber, "foreach の文字列に未対応のエスケープがあります。");
				}
			}

			return result.ToString();
		}

		private Action<Execution> ReadPrompt(int openingLine)
		{
			var content = new StringBuilder();

			while (this.Position < this.Lines.Count)
			{
				Match source = this.Lines[this.Position++];
				string line = GetLine(source);

				if (this.CommentStart != 0)
				{
					int end = line.IndexOf("*/", StringComparison.Ordinal);
					if (end < 0)
						continue;

					this.CommentStart = 0;
					line = line.Substring(end + 2);
					if (string.IsNullOrWhiteSpace(line))
						continue;
				}

				line = line.TrimStart();

				switch (line.TrimEnd())
				{
					case "<":
						throw this.Error(this.Position, "プロンプトを入れ子にできません。");

					case ">":
						string prompt = content.ToString();
						if (string.IsNullOrWhiteSpace(prompt))
							throw this.Error(openingLine, "プロンプトに内容がありません。");

						return execution => execution.RunPrompt(VariableReference.Replace(prompt, match =>
							match.Value[1] == '$' ? match.Value.Substring(1) : execution.Variables[match.Groups[1].Value]));
				}

				foreach (Match reference in VariableReference.Matches(line))
				{
					string name = reference.Groups[1].Value;
					if (reference.Value[1] != '$' && !this.Variables.Contains(name))
						throw this.Error(this.Position, "未定義の変数です。 " + name);
				}

				content.Append(line).Append(source.Groups[2].Value);
			}

			if (this.CommentStart != 0)
				throw this.Error(this.CommentStart, "複数行コメントを閉じる「*/」がありません。");

			throw this.Error(openingLine, "プロンプトを閉じる「>」がありません。");
		}

		private string ReadCodeLine()
		{
			while (this.Position < this.Lines.Count)
			{
				string line = this.RemoveComments(GetLine(this.Lines[this.Position++])).Trim();
				if (line.Length != 0)
					return line;
			}

			if (this.CommentStart != 0)
				throw this.Error(this.CommentStart, "複数行コメントを閉じる「*/」がありません。");

			return null;
		}

		private static string GetLine(Match source)
		{
			return source.Groups[3].Success ? source.Groups[3].Value : source.Groups[1].Value;
		}

		private string RemoveComments(string line)
		{
			var result = new StringBuilder();
			bool quoted = false;
			bool escapedList = false;

			for (int index = 0; index < line.Length; index++)
			{
				char current = line[index];
				char next = index + 1 < line.Length ? line[index + 1] : '\0';

				if (this.CommentStart != 0)
				{
					if (current == '*' && next == '/')
					{
						this.CommentStart = 0;
						index++;
					}
					continue;
				}

				if (quoted && escapedList && current == '\\' && next != '\0')
				{
					result.Append(current).Append(next);
					index++;
					continue;
				}

				if (current == '"')
				{
					if (!quoted && !escapedList)
						escapedList = ForeachListPrefix.IsMatch(result.ToString());

					quoted = !quoted;
				}

				if (!quoted && current == '/')
				{
					if (next == '/')
						break;

					if (next == '*')
					{
						this.CommentStart = this.Position;
						result.Append(' ');
						index++;
						continue;
					}
				}

				result.Append(current);
			}

			return result.ToString();
		}

		private static string ParsePath(string token, Func<string, FormatException> error)
		{
			Match match = PathToken.Match(token);
			if (!match.Success)
				throw error("パスの書式が正しくありません。");

			return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
		}

		private FormatException Error(int line, string message)
		{
			return new FormatException(string.Format("{0}({1}): {2}", this.FileName, line, message));
		}

		private sealed class ConditionReader
		{
			private static readonly Regex Token = new Regex(@"\G\s*(""[^""]*""|[()]|[^\s""()]+)");
			private readonly string Source;
			private readonly Func<string, FormatException> Error;
			private int Position;

			public ConditionReader(string source, Func<string, FormatException> error)
			{
				this.Source = source;
				this.Error = error;
			}

			public Func<Execution, bool> Parse()
			{
				Func<Execution, bool> condition = this.ReadAtom();

				if (this.Position != this.Source.Length)
					throw this.Error("if の条件式に余分な記述があります。");

				return condition;
			}

			private Func<Execution, bool> ReadOr()
			{
				Func<Execution, bool> condition = this.ReadAnd();

				while (this.Take("OR"))
				{
					Func<Execution, bool> left = condition;
					Func<Execution, bool> right = this.ReadAnd();
					condition = execution => left(execution) || right(execution);
				}

				return condition;
			}

			private Func<Execution, bool> ReadAnd()
			{
				Func<Execution, bool> condition = this.ReadNot();

				while (this.Take("AND"))
				{
					Func<Execution, bool> left = condition;
					Func<Execution, bool> right = this.ReadNot();
					condition = execution => left(execution) && right(execution);
				}

				return condition;
			}

			private Func<Execution, bool> ReadNot()
			{
				bool negate = false;
				while (this.Take("NOT"))
					negate = !negate;

				Func<Execution, bool> condition = this.ReadAtom();
				return negate ? execution => !condition(execution) : condition;
			}

			private Func<Execution, bool> ReadAtom()
			{
				if (this.Take("("))
				{
					Func<Execution, bool> condition = this.ReadOr();

					if (!this.Take(")"))
						throw this.Error("条件式を閉じる「)」が必要です。");
					return condition;
				}

				if (!this.Take("EXISTS"))
					throw this.Error("条件式には EXISTS または括弧付きの式が必要です。");

				Match token = Token.Match(this.Source, this.Position);
				string path = ParsePath(token.Groups[1].Value, this.Error);
				this.Position += token.Length;
				return execution => File.Exists(execution.ResolvePath(path));
			}

			private bool Take(string expected)
			{
				Match token = Token.Match(this.Source, this.Position);
				if (!token.Success || token.Groups[1].Value != expected)
					return false;

				this.Position += token.Length;
				return true;
			}
		}
	}
}
