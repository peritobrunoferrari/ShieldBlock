using System;
using System.IO;
using System.Windows.Forms;
using ShieldBlock.UI;

namespace ShieldBlock
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Captura erros nao tratados em threads de UI e background
            Application.ThreadException += (s, e) => ShowFatal(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                ShowFatal(e.ExceptionObject as Exception);

            try
            {
                // 1. Splash com creditos
                using (var splash = new SplashAboutForm())
                {
                    var result = splash.ShowDialog();
                    if (result != DialogResult.OK) return;
                }

                // 2. Janela principal
                MainForm mf;
                try { mf = new MainForm(); }
                catch (Exception ctorEx)
                {
                    ShowFatal(ctorEx, "Erro ao inicializar MainForm");
                    return;
                }
                Application.Run(mf);
            }
            catch (Exception ex)
            {
                ShowFatal(ex);
            }
        }

        private static void ShowFatal(Exception ex, string title = "ShieldBlock - Erro fatal")
        {
            try
            {
                string msg = ex == null ? "Erro desconhecido (null)" : ex.ToString();
                MessageBox.Show(msg, title,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Salva um crash dump no diretorio Logs
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                Directory.CreateDirectory(dir);
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string file = Path.Combine(dir, "crash_" + ts + ".txt");
                File.WriteAllText(file,
                    "[" + DateTime.Now + "]\r\n" + msg + "\r\n");
            }
            catch { /* nao deixa o handler explodir */ }
        }
    }
}
