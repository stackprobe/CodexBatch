using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HLTStudio.Commons;

namespace HLTStudio.AIScripts
{
	/// <summary>
	/// スクリプト
	/// </summary>
	public class AIScript
	{
		/// <summary>
		/// コンストラクタ
		/// </summary>
		/// <param name="file">スクリプトファイル</param>
		public AIScript(string file)
		{
			// TODO
		}

		/// <summary>
		/// 実行
		/// </summary>
		public void Run()
		{
			// TODO
		}

		/// <summary>
		/// ログ出力
		/// </summary>
		/// <param name="message">メッセージ</param>
		private static void WriteLog(object message)
		{
			ProcMain.WriteLog(message);
		}
	}
}
