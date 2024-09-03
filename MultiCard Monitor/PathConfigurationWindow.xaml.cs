using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Xml.Linq;

namespace MultiCard_Monitor
{
    public partial class PathConfigurationWindow
    {
        private const string ConfigFilePath = "PathConfig.xml";

        public string ActivityLogPath { get; private set; }
        public string DiagnosticaPath { get; private set; }
        public string CommunicatorLogPath { get; private set; }

        public PathConfigurationWindow()
        {
            InitializeComponent();
            LoadConfiguration();
        }

        private void OnBrowseActivityLog(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "MDB files (*.mdb)|*.mdb|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                ActivityLogPathTextBox.Text = openFileDialog.FileName;
            }
        }

        private void OnBrowseDiagnostica(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "MDB files (*.mdb)|*.mdb|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                DiagnosticaPathTextBox.Text = openFileDialog.FileName;
            }
        }

        private void OnBrowseCommunicatorLog(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Communicator Log file (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                CommunicatorLogPathTextBox.Text = openFileDialog.FileName;
            }
        }

        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            ActivityLogPath = ActivityLogPathTextBox.Text;
            DiagnosticaPath = DiagnosticaPathTextBox.Text;
            CommunicatorLogPath = CommunicatorLogPathTextBox.Text;

            // Save the paths to an XML file
            SaveConfiguration(ActivityLogPath, DiagnosticaPath, CommunicatorLogPath);

            this.DialogResult = true;
            this.Close();
        }

        
         private void SaveConfiguration(string activityLogPath, string diagnosticaPath, string communicatorLogPath)
        {
            var configXml = new XElement("Configuration",
                                new XElement("Paths",
                                    new XElement("ActivityLogPath", activityLogPath),
                                    new XElement("DiagnosticaPath", diagnosticaPath),
                                    new XElement("CommunicatorLogPath", communicatorLogPath)
                                ));

            configXml.Save(ConfigFilePath);
        }

        private void LoadConfiguration()
        {
            if (File.Exists(ConfigFilePath))
            {
                XElement configXml = XElement.Load(ConfigFilePath);
                ActivityLogPathTextBox.Text = configXml.Element("Paths").Element("ActivityLogPath")?.Value;
                DiagnosticaPathTextBox.Text = configXml.Element("Paths").Element("DiagnosticaPath")?.Value;
                CommunicatorLogPathTextBox.Text = configXml.Element("Paths").Element("CommunicatorLogPath")?.Value;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
           //close the window
            this.Close();   
        }
    }
}
