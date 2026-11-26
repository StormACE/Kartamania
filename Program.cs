using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Kartamania
{
    public static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        static void EnableDpiAwareness()
        {
            try
            {
                if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return;
            }
            catch { }
            try
            {
                SetProcessDPIAware();
            }
            catch { }
        }

        [STAThread]
        private static void Main()
        {
            EnableDpiAwareness();
            try
            {
                using (var game = new KartamaniaGame())
                    game.Run();
            }
            catch (Exception ex)
            {
                // En cas de plantage, on écrit le détail à côté de l'exécutable pour pouvoir le lire.
                try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), ex.ToString()); } catch { }
                throw;
            }
        }
    }
}
