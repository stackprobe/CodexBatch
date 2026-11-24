using System;
using System.Linq;
using HLTStudio.AIScripts;
using HLTStudio.Commons;

namespace HLTStudio
{
	class Program
	{
		static void Main(string[] args)
		{
			ProcMain.CUIMain(new Program().Main2);
		}

		private void Main2(ArgsReader ar)
		{
			try
			{
				string[] files = ar.TrailArgs().ToArray();
				if (files.Length != 1)
					throw new ArgumentException("スクリプトファイルを1個指定してください。");

				string file = files[0];
				ProcMain.WriteLog("スクリプトを読み込みます。");
				ProcMain.WriteLog("< " + file);

				var script = new AIScript(file);

				ProcMain.WriteLog("スクリプトを実行します。");
				script.Run();
				ProcMain.WriteLog("スクリプトを実行しました。");
			}
			catch (Exception ex)
			{
				ProcMain.WriteLog(ex);
				Environment.ExitCode = 1;
			}
			SCommon.OpenOutputDirIfCreated();
		}
	}
}
