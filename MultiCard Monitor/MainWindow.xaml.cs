using System;
using System.Data;
using System.Data.OleDb;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.IO;
using System.Xml.Linq;

namespace MultiCard_Monitor
{
    public partial class MainWindow 
    {
        private RTMonitoring _rtMonitoring;
        private string? _connectionString;
        private string _activityLogPath;
        private string _diagnosticaPath;
        private const string ConfigFilePath = "PathConfig.xml";

        public MainWindow()
        {
            InitializeComponent();
            LoadConfiguration();
        }

        private void OnSetConfigClicked(object sender, RoutedEventArgs e)
        {
            // Open the configuration window
            PathConfigurationWindow configWindow = new PathConfigurationWindow();

            if (configWindow.ShowDialog() == true)
            {
                // Retrieve the paths from the configuration window
                _activityLogPath = configWindow.ActivityLogPath;
                _diagnosticaPath = configWindow.DiagnosticaPath;

                // Save to XML
                SaveConfiguration(_activityLogPath, _diagnosticaPath);
            }
        }

        private void LoadConfiguration()
        {
            if (File.Exists(ConfigFilePath))
            {
                XElement configXml = XElement.Load(ConfigFilePath);
                _activityLogPath = configXml.Element("Paths").Element("ActivityLogPath")?.Value;
                _diagnosticaPath = configXml.Element("Paths").Element("DiagnosticaPath")?.Value;
            }
        }

        private void SaveConfiguration(string activityLogPath, string diagnosticaPath)
        {
            var configXml = new XElement("Configuration",
                                new XElement("Paths",
                                    new XElement("ActivityLogPath", activityLogPath),
                                    new XElement("DiagnosticaPath", diagnosticaPath)
                                ));

            configXml.Save(ConfigFilePath);
        }

        private async void OnRealTimeMonitoringClicked(object sender, RoutedEventArgs e)
        {

            if (string.IsNullOrEmpty(_activityLogPath) || string.IsNullOrEmpty(_diagnosticaPath))
            {
                MessageBox.Show("Please configure the paths to ActivityLog.mdb and Diagnostica.mdb first.", "Configuration Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _rtMonitoring = new RTMonitoring(_activityLogPath, _diagnosticaPath);

            if (!_rtMonitoring.AreProcessesRunning())
            {
                MessageBox.Show("Ensure that both Multicard.exe and SmartCardPersonalization.exe are running and try again.", "Processes Not Running", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _rtMonitoring.StoreCurrentTimestamp();

            RealTimeMonitoringControl monitoringControl = new RealTimeMonitoringControl();
            this.Content = monitoringControl; // Display the UserControl in MainWindow

            await _rtMonitoring.MonitorEvents(eventText =>
            {
                Dispatcher.Invoke(() => monitoringControl.EventsTextBox.AppendText(eventText + Environment.NewLine));
            });
        }



        private void OnLoadFileClicked(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "MDB files (*.mdb)|*.mdb|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string mdbFilePath = openFileDialog.FileName;
                _connectionString = $@"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={mdbFilePath};";

                EnableControls();
                LoadData();
            }
        }

        //private void LoadData(string filter = "")
        //{
        //    if (string.IsNullOrEmpty(_connectionString))
        //    {
        //        MessageBox.Show("No MDB file loaded. Please load an MDB file first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        //        return;
        //    }

        //    try
        //    {
        //        using (OleDbConnection connection = new OleDbConnection(_connectionString))
        //        {
        //            connection.Open();

        //            // Clear existing tabs
        //            TablesTabControl.Items.Clear();

        //            // Load specified tables
        //            string[] tableNames = { "Admin", "UserOperations", "CardProduction" };
        //            foreach (string tableName in tableNames)
        //            {
        //                DataTable dataTable = new DataTable();
        //                string query = $"SELECT * FROM {tableName}";

        //                if (!string.IsNullOrEmpty(filter))
        //                {
        //                    query += " WHERE YourColumnName LIKE @filter"; // Adjust YourColumnName to an appropriate column
        //                }

        //                using (OleDbCommand command = new OleDbCommand(query, connection))
        //                {
        //                    if (!string.IsNullOrEmpty(filter))
        //                    {
        //                        command.Parameters.AddWithValue("@filter", $"%{filter}%");
        //                    }

        //                    using (OleDbDataAdapter adapter = new OleDbDataAdapter(command))
        //                    {
        //                        adapter.Fill(dataTable);
        //                    }
        //                }

        //                // Create a new TabItem for each table
        //                TabItem tabItem = new TabItem
        //                {
        //                    Header = tableName
        //                };

        //                // Create a DataGrid to display the table data
        //                DataGrid dataGrid = new DataGrid
        //                {
        //                    AutoGenerateColumns = true,
        //                    IsReadOnly = true,
        //                    ItemsSource = dataTable.DefaultView
        //                };

        //                // Set the DataGrid as the content of the TabItem
        //                tabItem.Content = dataGrid;

        //                // Add the TabItem to the TabControl
        //                TablesTabControl.Items.Add(tabItem);
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Error: {ex.Message}");
        //    }
        //}

        private void LoadData(string filter = "")
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                MessageBox.Show("No MDB file loaded. Please load an MDB file first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (OleDbConnection connection = new OleDbConnection(_connectionString))
                {
                    connection.Open();

                    // Clear existing tabs
                    TablesTabControl.Items.Clear();

                    // List of tables to load
                    string[] commonTableNames = { "Admin", "UserOperations", "CardProduction" };
                    string specialTableName = "StatiAllarmi";

                    // Check if the MDB file contains the "StatiAllarmi" table
                    DataTable schemaTable = connection.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);
                    bool containsStatiAllarmi = false;
                    bool containsCommonTables = false;

                    foreach (DataRow row in schemaTable.Rows)
                    {
                        string tableName = row["TABLE_NAME"].ToString();
                        if (tableName == specialTableName)
                        {
                            containsStatiAllarmi = true;
                        }
                        else if (Array.Exists(commonTableNames, name => name == tableName))
                        {
                            containsCommonTables = true;
                        }
                    }

                    // Load the "StatiAllarmi" table with translated headers if it exists
                    if (containsStatiAllarmi)
                    {
                        LoadStatiAllarmiTable(connection, filter);
                    }

                    // Load the common tables ("Admin," "UserOperations," "CardProduction") if they exist
                    if (containsCommonTables)
                    {
                        LoadCommonTables(connection, commonTableNames, filter);
                    }

                    if (!containsStatiAllarmi && !containsCommonTables)
                    {
                        MessageBox.Show("No recognized tables found in the MDB file.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void LoadStatiAllarmiTable(OleDbConnection connection, string filter)
        {
            try
            {
                DataTable dataTable = new DataTable();
                string query = "SELECT * FROM StatiAllarmi";

                if (!string.IsNullOrEmpty(filter))
                {
                    query += " WHERE TIPO APPARATO LIKE @filter"; // Adjust filter column as needed
                }

                using (OleDbCommand command = new OleDbCommand(query, connection))
                {
                    if (!string.IsNullOrEmpty(filter))
                    {
                        command.Parameters.AddWithValue("@filter", $"%{filter}%");
                    }

                    using (OleDbDataAdapter adapter = new OleDbDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }
                }

                // Translate column headers
                dataTable.Columns["TIPO APPARATO"].ColumnName = "TYPE DEVICE";
                dataTable.Columns["DATA ORA"].ColumnName = "DATE TIME";
                dataTable.Columns["STATO OPERATIVO"].ColumnName = "OPERATING STATUS";
                dataTable.Columns["STATO DIAGNOSTICO"].ColumnName = "DIAGNOSTIC STATUS";
                dataTable.Columns["MODALITA OPERATIVA"].ColumnName = "OPERATING MODE";
                dataTable.Columns["ALLARME"].ColumnName = "ALARM";
                dataTable.Columns["DESCRIZIONE ALLARME"].ColumnName = "ALARM DESCRIPTION";

                // Create a new TabItem for the "StatiAllarmi" table
                TabItem tabItem = new TabItem
                {
                    Header = "StatiAllarmi (Translated)"
                };

                // Create a DataGrid to display the translated table data
                DataGrid dataGrid = new DataGrid
                {
                    AutoGenerateColumns = true,
                    IsReadOnly = false,
                    ItemsSource = dataTable.DefaultView
                };

                // Set the DataGrid as the content of the TabItem
                tabItem.Content = dataGrid;

                // Add the TabItem to the TabControl
                TablesTabControl.Items.Add(tabItem);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading 'StatiAllarmi' table: {ex.Message}");
            }
        }

        private void LoadCommonTables(OleDbConnection connection, string[] tableNames, string filter)
        {
            foreach (string tableName in tableNames)
            {
                try
                {
                    DataTable dataTable = new DataTable();
                    string query = $"SELECT * FROM {tableName}";

                    if (!string.IsNullOrEmpty(filter))
                    {
                        query += " WHERE YourColumnName LIKE @filter"; // Adjust filter column as needed
                    }

                    using (OleDbCommand command = new OleDbCommand(query, connection))
                    {
                        if (!string.IsNullOrEmpty(filter))
                        {
                            command.Parameters.AddWithValue("@filter", $"%{filter}%");
                        }

                        using (OleDbDataAdapter adapter = new OleDbDataAdapter(command))
                        {
                            adapter.Fill(dataTable);
                        }
                    }

                    // Create a new TabItem for each common table
                    TabItem tabItem = new TabItem
                    {
                        Header = tableName
                    };

                    // Create a DataGrid to display the table data
                    DataGrid dataGrid = new DataGrid
                    {
                        AutoGenerateColumns = true,
                        IsReadOnly = false,
                        ItemsSource = dataTable.DefaultView
                    };

                    // Set the DataGrid as the content of the TabItem
                    tabItem.Content = dataGrid;

                    // Add the TabItem to the TabControl
                    TablesTabControl.Items.Add(tabItem);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading '{tableName}' table: {ex.Message}");
                }
            }
        }



        private void EnableControls()
        {
            refresh.IsEnabled = true;
            Exportcsv.IsEnabled = true;
            foreach (var control in new[] { TablesTabControl })
            {
                control.IsEnabled = true;
            }
        }

        //private void OnSearchClicked(object sender, RoutedEventArgs e)
        //{
        //    string filter = SearchTextBox.Text;
        //    LoadData(filter);
        //}

        private void OnRefreshClicked(object sender, RoutedEventArgs e)
        {
            //SearchTextBox.Text = string.Empty;
            LoadData();
        }

        private void OnExportClicked(object sender, RoutedEventArgs e)
        {
            if (TablesTabControl.SelectedItem is TabItem selectedTab)
            {
                DataGrid dataGrid = selectedTab.Content as DataGrid;
                if (dataGrid != null && dataGrid.ItemsSource is DataView dataView)
                {
                    SaveFileDialog saveFileDialog = new SaveFileDialog
                    {
                        Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
                    };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        ExportDataTableToCSV(dataView.ToTable(), saveFileDialog.FileName);
                    }
                }
            }
            else
            {
                MessageBox.Show("No table selected.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExportDataTableToCSV(DataTable dataTable, string filePath)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    // Write column headers
                    for (int i = 0; i < dataTable.Columns.Count; i++)
                    {
                        writer.Write(dataTable.Columns[i]);
                        if (i < dataTable.Columns.Count - 1)
                        {
                            writer.Write(",");
                        }
                    }
                    writer.WriteLine();

                    // Write rows
                    foreach (DataRow row in dataTable.Rows)
                    {
                        for (int i = 0; i < dataTable.Columns.Count; i++)
                        {
                            writer.Write(row[i].ToString());
                            if (i < dataTable.Columns.Count - 1)
                            {
                                writer.Write(",");
                            }
                        }
                        writer.WriteLine();
                    }
                }
                MessageBox.Show("Data exported successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }
    }
}
