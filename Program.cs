using System;
using System.IO;

namespace Kartamania
{
    public static class Program
    {
        [STAThread]
        private static void Main()
        {
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
