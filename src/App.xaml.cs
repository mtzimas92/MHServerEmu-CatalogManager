using System.Diagnostics;
using MHServerEmu.Games.GameData;
using System.IO;
using System.Windows;

namespace CatalogManager
{
    public partial class App : Application
    {

        [STAThread]
        public static void Main()
        {
            string dataPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Game");
            Debug.WriteLine($"Current directory: {Directory.GetCurrentDirectory()}");
            Debug.WriteLine($"Looking for .sip files in: {dataPath}");
            Debug.WriteLine($"Game directory exists: {Directory.Exists(dataPath)}");

            if (Directory.Exists(dataPath))
            {
                var files = Directory.GetFiles(dataPath, "*.sip");
                Debug.WriteLine($"Found .sip files: {string.Join(", ", files)}");
            }

            if (!PakFileSystem.Instance.Initialize())
            {
                MessageBox.Show("Failed to initialize pak file system", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var application = new App();
            application.InitializeComponent();
            application.Run();
        }
    }
}
