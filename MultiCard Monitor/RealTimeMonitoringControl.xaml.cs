using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace MultiCard_Monitor
{
    /// <summary>
    /// Interaction logic for RealTimeMonitoringControl.xaml
    /// </summary>
    public partial class RealTimeMonitoringWindow : Window
    {
        private bool _isMonitoring;

        public RealTimeMonitoringWindow()
        {
            InitializeComponent();
            _isMonitoring = true;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void OnStopMonitoringClicked(object sender, RoutedEventArgs e)
        {
            if (_isMonitoring)
            {
                _isMonitoring = false;
                EventsTextBox.AppendText("\nMonitoring stopped.");
                // Add logic here to actually stop monitoring the logs
            }
        }

        private void OnSaveLogClicked(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"MultiCard_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveFileDialog.FileName, EventsTextBox.Text);
                MessageBox.Show("Log saved successfully.", "Save Log", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OnExitClicked(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
