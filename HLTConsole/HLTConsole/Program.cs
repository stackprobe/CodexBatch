using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using HLTStudio.Commons;
using HLTStudio.AIScripts;

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
			if (ProcMain.DEBUG)
			{
				Main3();
			}
			else
			{
				Main4(ar);
			}
			SCommon.OpenOutputDirIfCreated();
		}

		private void Main3()
		{
#if DEBUG
			// -- choose one --

			//Main4(new ArgsReader(new string[] { }));
			//Main4(new ArgsReader(new string[] { }));
			//Main4(new ArgsReader(new string[] { }));

			// --
#endif
			SCommon.Pause();
		}

		private void Main4(ArgsReader ar)
		{
			try
			{
				Main5(ar);
			}
			catch (Exception ex)
			{
				ProcMain.WriteLog(ex);

				MessageBox.Show(ex.ToString(), $"{Path.GetFileNameWithoutExtension(ProcMain.SelfFile)} / エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void Main5(ArgsReader ar)
		{
			string[] files = ar.TrailArgs()
				.Select(file => SCommon.MakeFullPath(file))
				.ToArray();

			if (files.Length == 0)
				throw new Exception("スクリプトファイルが指定されていません！");

			foreach (string file in files)
				if (!File.Exists(file))
					throw new Exception("スクリプトファイルが存在しません！" + file);

			try
			{
				ProcMain.WriteLog("スクリプトを読み込みます。");

				AIScript[] scripts = files.Select(file =>
				{
					ProcMain.WriteLog("読み込み：" + file);
					AIScript script = new AIScript(file);
					ProcMain.WriteLog("読み込み OK");
					return script;
				})
				.ToArray();

				ProcMain.WriteLog("スクリプトを実行します。");

				foreach (var script in scripts)
				{
					ProcMain.WriteLog("実行：" + script.ScriptFilePath);
					script.Run();
					ProcMain.WriteLog("実行 OK");
				}
				ProcMain.WriteLog("スクリプトを実行しました。");
			}
			catch (Exception ex)
			{
				ProcMain.WriteLog(ex);
			}
		}
	}
}
