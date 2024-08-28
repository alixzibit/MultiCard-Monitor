using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Data;
using System.Data.OleDb;
using System.Xml.Linq;

namespace MultiCard_Monitor
{
    public class RTMonitoring
    {
        private string _activityLogPath;
        private string _diagnosticaPath;
        private DateTime _lastTimestamp;
        private const string ProcessName1 = "Multicard";
        private const string ProcessName2 = "SmartCardPersonalization";

        public RTMonitoring(string activityLogPath, string diagnosticaPath)
        {
            _activityLogPath = activityLogPath;
            _diagnosticaPath = diagnosticaPath;
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
            while (true)
            {
                if (!File.Exists(_activityLogPath)) throw new FileNotFoundException("ActivityLog.mdb not found.");
                using (OleDbConnection connection = new OleDbConnection($"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={_activityLogPath};"))
                {
                    connection.Open();

                    // Monitor Admin table
                    await MonitorTable(connection, "Admin", onNewEvent);

                    // Monitor UserOperations table
                    await MonitorTable(connection, "UserOperations", onNewEvent);

                    // Monitor CardProduction table
                    await MonitorTable(connection, "CardProduction", onNewEvent);
                }

                // Update last timestamp after processing events
                _lastTimestamp = DateTime.Now;

                // Wait for a few seconds before the next check
                await Task.Delay(5000);
            }
        }

        //private async Task MonitorTable(OleDbConnection connection, string tableName, Action<string> onNewEvent)
        //{
        //    string query = $"SELECT * FROM {tableName} WHERE DateTime > @timestamp";
        //    using (OleDbCommand command = new OleDbCommand(query, connection))
        //    {
        //        command.Parameters.AddWithValue("@timestamp", _lastTimestamp);
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
