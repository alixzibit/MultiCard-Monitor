using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Data;
using System.Data.OleDb;
using System.Xml.Linq;
using System.Collections.Generic;

namespace MultiCard_Monitor
{
    public class RTMonitoring
    {
        private bool _isMonitoring;
        private string _activityLogPath;
        private string _diagnosticaPath;
        private string _communicatorLogPath;
        private DateTime _lastTimestamp;
        private const string ProcessName1 = "Multicard";
        private const string ProcessName2 = "SmartCardPersonalization";

        public RTMonitoring(string activityLogPath, string diagnosticaPath , string communicatorLogPath)
        {
            _activityLogPath = activityLogPath;
            _diagnosticaPath = diagnosticaPath;
            _communicatorLogPath = communicatorLogPath;
            _isMonitoring = true;
        }

        public bool AreProcessesRunning()
        {
            return Process.GetProcessesByName(ProcessName1).Length > 0 &&
                   Process.GetProcessesByName(ProcessName2).Length > 0;
        }

        public void StoreCurrentTimestamp()
        {
            _lastTimestamp = DateTime.Now;
            // Save timestamp to XML for persistence, if needed
            var xml = new XElement("Monitoring",
                           new XElement("LastTimestamp", _lastTimestamp.ToString("MM/dd/yyyy hh:mm:ss tt")));
            xml.Save("LastMonitoringState.xml");
        }

        public async Task MonitorEvents(Action<string> onNewEvent)
        {
            while (_isMonitoring)
            {
                DateTime loopStartTimestamp = _lastTimestamp;

                // Check and monitor MDB files
                if (File.Exists(_activityLogPath))
                {
                    using (OleDbConnection activityLogConnection = new OleDbConnection($"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={_activityLogPath};"))
                    {
                        activityLogConnection.Open();

                        await MonitorTable(activityLogConnection, "Admin", onNewEvent);
                        await MonitorTable(activityLogConnection, "UserOperations", onNewEvent);
                        await MonitorTable(activityLogConnection, "CardProduction", onNewEvent);
                    }
                }

                if (File.Exists(_diagnosticaPath))
                {
                    using (OleDbConnection diagnosticaConnection = new OleDbConnection($"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={_diagnosticaPath};"))
                    {
                        diagnosticaConnection.Open();
                        await MonitorStatiAllarmiTable(diagnosticaConnection, onNewEvent);
                    }
                }

                // Monitor Communicator log file
                if (File.Exists(_communicatorLogPath))
                {
                    await MonitorCommunicatorLog(onNewEvent);
                }

                // Update last timestamp after processing all events
                _lastTimestamp = DateTime.Now;

                // Wait for a few seconds before the next check
                await Task.Delay(1000);
            }
        }


        //private async Task MonitorTable(OleDbConnection connection, string tableName, Action<string> onNewEvent)
        //{
        //    Format the DateTime to match the expected format in Access(without leading zeros)
        //    string formattedTimestamp = _lastTimestamp.ToString("M/d/yyyy h:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture);

        //    string query = $"SELECT * FROM {tableName} WHERE DateTime > ?";
        //    using (OleDbCommand command = new OleDbCommand(query, connection))
        //    {
        //        Use a parameterized query to avoid SQL injection and ensure proper data type handling
        //        command.Parameters.AddWithValue("?", formattedTimestamp);

        //        using (OleDbDataReader reader = command.ExecuteReader())
        //        {
        //            while (await reader.ReadAsync())
        //            {
        //                string formattedEvent = FormatEvent(reader, tableName);
        //                onNewEvent?.Invoke(formattedEvent);
        //            }
        //        }
        //    }
        //}

        private async Task MonitorTable(OleDbConnection connection, string tableName, Action<string> onNewEvent)
        {
            // Format the DateTime to match the expected format in Access
            string formattedTimestamp = _lastTimestamp.ToString("MM/dd/yyyy hh:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture);

            string query = $"SELECT * FROM {tableName} WHERE DateTime > @timestamp";
            using (OleDbCommand command = new OleDbCommand(query, connection))
            {
                // Use a parameterized query to avoid SQL injection and ensure proper data type handling
                command.CommandText = $"SELECT * FROM {tableName} WHERE DateTime > ?";
                command.Parameters.AddWithValue("?", formattedTimestamp);

                using (OleDbDataReader reader = command.ExecuteReader())
                {
                    while (await reader.ReadAsync())
                    {
                        string formattedEvent = FormatEvent(reader, tableName);
                        onNewEvent?.Invoke(formattedEvent);
                    }
                }
            }
        }




        private async Task MonitorStatiAllarmiTable(OleDbConnection connection, Action<string> onNewEvent)
        {
            try
            {
                string formattedTimestamp = _lastTimestamp.ToString("MM/dd/yyyy hh:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture);
                string query = "SELECT * FROM StatiAllarmi WHERE [DATA ORA] > ?";

                using (OleDbCommand command = new OleDbCommand(query, connection))
                {
                    command.Parameters.AddWithValue("?", formattedTimestamp);

                    using (OleDbDataReader reader = command.ExecuteReader())
                    {
                        while (await reader.ReadAsync())
                        {
                            string formattedEvent = FormatStatiAllarmiEvent(reader);
                            onNewEvent?.Invoke(formattedEvent);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                onNewEvent?.Invoke($"Error loading 'StatiAllarmi' table: {ex.Message}");
            }
        }


        private DateTime ParseCommunicatorLogTimestamp(string timestamp)
        {
            DateTime parsedTimestamp;

            // Check if the timestamp is in the format "YYYY-MM-DD HH:MM:SS"
            if (DateTime.TryParseExact(timestamp, "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out parsedTimestamp))
            {
                return parsedTimestamp;
            }

            // Check if the timestamp is in the Julian format "DDMMYYYY HHMMSS:FFF"
            if (timestamp.Length > 17 && timestamp[8] == ' ')
            {
                // Extract the main part and ignore the fractional seconds
                string julianDate = timestamp.Substring(0, 17);
                parsedTimestamp = ConvertJulianToDateTime(julianDate);
            }
            else
            {
                // Handle potential errors or unexpected formats
                throw new FormatException("Unrecognized timestamp format: " + timestamp);
            }

            return parsedTimestamp;
        }

        private DateTime ConvertJulianToDateTime(string julianDate)
        {
            // Julian date in format: DDMMYYYY HHMMSS:FFF
            string datePart = julianDate.Substring(0, 8);  // "03092024"
            string timePart = julianDate.Substring(9, 6);  // "125806"

            // Parse the date and time, ignoring the fractional seconds
            DateTime dateTime = DateTime.ParseExact(datePart, "ddMMyyyy", null);
            TimeSpan time = TimeSpan.ParseExact(timePart, "hhmmss", null);
            dateTime = dateTime.Add(time);

            return dateTime;
        }

        //private async Task MonitorCommunicatorLog(Action<string> onNewEvent)
        //{
        //    try
        //    {
        //        using (FileStream fileStream = new FileStream(_communicatorLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        //        using (StreamReader reader = new StreamReader(fileStream))
        //        {
        //            string line;
        //            while ((line = await reader.ReadLineAsync()) != null)
        //            {
        //                // Extract and convert the Julian timestamp
        //                string julianDate = line.Substring(0, 17); // Assuming the timestamp is always in the first 17 characters
        //                DateTime logTimestamp = ConvertJulianToDateTime(julianDate);

        //                // Compare with the last timestamp
        //                if (logTimestamp > _lastTimestamp)
        //                {
        //                    // Only process lines that contain "****" or "===="
        //                    if (line.Contains("****") || line.Contains("===="))
        //                    {
        //                        onNewEvent?.Invoke(line);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        onNewEvent?.Invoke($"Error reading communicator log file: {ex.Message}");
        //    }
        //}
        private async Task MonitorCommunicatorLog(Action<string> onNewEvent)
        {
            try
            {
                using (FileStream fileStream = new FileStream(_communicatorLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(fileStream))
                {
                    string line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        // Extract the timestamp from the line
                        string timestamp = ExtractTimestampFromLogLine(line);

                        if (!string.IsNullOrEmpty(timestamp))
                        {
                            DateTime logTimestamp = ParseCommunicatorLogTimestamp(timestamp);

                            // Compare with the last timestamp
                            if (logTimestamp > _lastTimestamp)
                            {
                                // Only process lines that contain "****" or "===="
                                if (line.Contains("****") || line.Contains("===="))
                                {
                                    onNewEvent?.Invoke(line);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                onNewEvent?.Invoke($"Error reading communicator log file: {ex.Message}");
            }
        }

        private string ExtractTimestampFromLogLine(string line)
        {
            // Assuming the timestamp is always at the start of the line
            if (line.Length >= 17)
            {
                return line.Substring(0, 19); // Extract the first 19 characters for both formats
            }

            return string.Empty;
        }




        private List<string> ParseCommunicatorLog(string[] logLines)
        {
            var parsedLines = new List<string>();

            foreach (var line in logLines)
            {
                if (line.Contains("****") || line.Contains("===="))
                {
                    parsedLines.Add(line);
                }
            }

            return parsedLines;
        }




        private string FormatEvent(OleDbDataReader reader, string tableName)
        {
            string formattedEvent = string.Empty;
            string dateTime = reader["DateTime"].ToString();

            if (tableName == "Admin")
            {
                string userName = reader["UserName"].ToString();
                string category = reader["Category"].ToString();
                string activity = reader["Activity"].ToString();
                string description = reader["Description"].ToString();
                string machineId = reader["MachineID"].ToString();
                string unitId = reader["UnitID"].ToString();
                string workstationName = reader["WorkstationName"].ToString();
                string autoNum = reader["AutoNum"].ToString();

                formattedEvent = $"{dateTime} [{tableName}] UserName \"{userName}\" Category {category} Activity {activity} " +
                                 $"Description {description} MachineID {machineId} UnitID {unitId} WorkstationName {workstationName} AutoNum {autoNum}";
            }
            else if (tableName == "UserOperations")
            {
                string userName = reader["UserName"].ToString();
                string category = reader["Category"].ToString();
                string activity = reader["Activity"].ToString();
                string jobName = reader["JobName"].ToString();
                string jobPath = reader["JobPath"].ToString();
                string description = reader["Description"].ToString();
                string machineId = reader["MachineID"].ToString();
                string unitId = reader["UnitID"].ToString();
                string autoNum = reader["AutoNum"].ToString();

                formattedEvent = $"{dateTime} [{tableName}] UserName \"{userName}\" Category {category} Activity {activity} " +
                                 $"JobName {jobName} JobPath {jobPath} Description {description} MachineID {machineId} UnitID {unitId} AutoNum {autoNum}";
            }
            else if (tableName == "CardProduction")
            {
                string machineId = reader["MachineID"].ToString();
                string unitId = reader["UnitID"].ToString();
                string userName = reader["UserName"].ToString();
                string category = reader["Category"].ToString();
                string activity = reader["Activity"].ToString();
                string jobId = reader["JobID"].ToString();
                string jobName = reader["JobName"].ToString();
                string jobPath = reader["JobPath"].ToString();
                string description = reader["Description"].ToString();
                string cardId = reader["CardID"].ToString();
                string customId = reader["CustomID"].ToString();
                string autoNum = reader["AutoNum"].ToString();
                string cardNumber = MaskCardNumber(reader["Card Number"].ToString());
                string cardName = reader["Card Name"].ToString();
                string expiryDate = reader["Expiry Date"].ToString();

                formattedEvent = $"{dateTime} [{tableName}] MachineID {machineId} UnitID {unitId} UserName \"{userName}\" " +
                                 $"Category {category} Activity {activity} JobID {jobId} JobName {jobName} JobPath {jobPath} " +
                                 $"Description {description} CardID {cardId} CustomID {customId} AutoNum {autoNum} " +
                                 $"Card Number {cardNumber} Card Name {cardName} Expiry Date {expiryDate}";
            }

            return formattedEvent;
        }

        private string FormatStatiAllarmiEvent(OleDbDataReader reader)
        {
            try
            {
                // Check if each column exists and is not null before accessing it
                string dateTime = reader.IsDBNull(reader.GetOrdinal("DATA ORA")) ? "Unknown" : reader["DATA ORA"].ToString();
                string typeDevice = reader.IsDBNull(reader.GetOrdinal("TIPO APPARATO")) ? "Unknown" : reader["TIPO APPARATO"].ToString();
                string operatingStatus = reader.IsDBNull(reader.GetOrdinal("STATO OPERATIVO")) ? "Unknown" : reader["STATO OPERATIVO"].ToString();
                string diagnosticStatus = reader.IsDBNull(reader.GetOrdinal("STATO DIAGNOSTICO")) ? "Unknown" : reader["STATO DIAGNOSTICO"].ToString();
                string operatingMode = reader.IsDBNull(reader.GetOrdinal("MODALITA OPERATIVA")) ? "Unknown" : reader["MODALITA OPERATIVA"].ToString();
                string alarm = reader.IsDBNull(reader.GetOrdinal("ALLARME")) ? "Unknown" : reader["ALLARME"].ToString();
                string alarmDescription = reader.IsDBNull(reader.GetOrdinal("DESCRIZIONE ALLARME")) ? "Unknown" : reader["DESCRIZIONE ALLARME"].ToString();

                return $"{dateTime} [StatiAllarmi] Type Device: {typeDevice}, Operating Status: {operatingStatus}, Diagnostic Status: {diagnosticStatus}, Operating Mode: {operatingMode}, Alarm: {alarm}, Alarm Description: {alarmDescription}";
            }
            catch (Exception ex)
            {
                return $"Error formatting StatiAllarmi event: {ex.Message}";
            }
        }


        private string MaskCardNumber(string cardNumber)
        {
            if (cardNumber.Length >= 12)
            {
                return cardNumber.Substring(0, 6) + "******" + cardNumber.Substring(cardNumber.Length - 4);
            }
            return cardNumber; // Return as is if the card number is too short to mask
        }

    }
}
