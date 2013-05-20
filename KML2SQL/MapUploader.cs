using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.IO;

namespace KML2SQL
{
    class MapUploader
    {
        string connectionString;
        string columnName;
        int ID = 1;
        string fileLocation;
        string tableName;
        bool geographyMode;
        string latLong;
        bool processCoordinatesNextIteration = false;
        int srid;
        System.Windows.Controls.TextBlock resultsBox;
        string results;

        public MapUploader(string serverName, string databaseName, string username, string password, string columnName, string fileLocation, string tableName, int srid, bool geographyMode, System.Windows.Controls.TextBlock results)
        {
            connectionString = "Data Source=" + serverName + ";Initial Catalog=" + databaseName + ";Persist Security Info=True;User ID=" + username + ";Password=" + password;
            this.columnName = columnName;
            this.fileLocation = fileLocation;
            this.tableName = tableName;
            this.geographyMode = geographyMode;
            this.srid = srid;
            this.resultsBox = results;
        }

        public void Upload()
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(connectionString))
            {
                string currentLine = "";
                StreamReader reader = new StreamReader(fileLocation);
                connection.Open();
                dropTable();
                createTable(geographyMode);
                while (!reader.EndOfStream)
                {
                    currentLine = reader.ReadLine();
                    if (currentLine.Contains("<SimpleData name=\"id\">"))
                        currentLine = getID(currentLine);
                    if (processCoordinatesNextIteration)
                        processPolygon(connection, currentLine);
                    if (currentLine.Trim() == "<coordinates>")
                        processCoordinatesNextIteration = true;
                }
            }
        }

        private string getID(string currentLine)
        {
            currentLine = currentLine.Replace("<SimpleData name=\"id\">", "");
            currentLine = currentLine.Replace("</SimpleData>", "");
            currentLine = currentLine.Trim();
            currentLine = currentLine.Replace("\t", "");
            ID = int.Parse(currentLine);
            return currentLine;
        }

        private void processPolygon(System.Data.SqlClient.SqlConnection connection, string currentLine)
        {
            getLatLong(currentLine);
            processCoordinatesNextIteration = false;
            string commandString = setCommandString(geographyMode);
            var command = new System.Data.SqlClient.SqlCommand(commandString, connection);
            command.CommandType = System.Data.CommandType.Text;
            command.ExecuteNonQuery();
            updateResults("Processing polygon" + ID.ToString());
        }

        private void getLatLong(string currentLine)
        {
            string[] coordinates = currentLine.Trim().Replace("\t", "").Split(',', ' ');
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < coordinates.Length; i += 3)
            {
                sb.Append(coordinates[i]);
                sb.Append(" ");
                sb.Append(coordinates[i + 1]);
                sb.Append(", ");
            }
            sb.Remove(sb.Length - 2, 2);
            latLong = sb.ToString();
        }

        private string setCommandString(bool geographyMode)
        {
            string commandString;
            if (geographyMode)
                commandString = @"DECLARE @geom geometry;
                            SET @geom = geometry::STPolyFromText('POLYGON(("+latLong+@"))', "+srid+@");
                            DECLARE @validGeom geometry;
                            set @validGeom = @geom.MakeValid().STUnion(@geom.STStartPoint())
                            DECLARE @validGeo geography;
                            SET @validGeo = geography::STGeomFromText(@validGeom.STAsText(), "+srid+@")
                            insert into "+tableName+" VALUES ("+ID+", @validGeo);";
            else
                commandString = @"DECLARE @geom geometry;
                            SET @geom = geometry::STPolyFromText('POLYGON(("+latLong+@"))', "+srid+@");
                            DECLARE @validGeom geometry;
                            set @validGeom = @geom.MakeValid().STUnion(@geom.STStartPoint())
                            insert into "+tableName+" VALUES ("+ID+", @validGeom);";
            return commandString;
        }

        private void dropTable()
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(connectionString))
            {
                connection.Open();
                try
                {
                    string dropCommandString = "DROP TABLE " + tableName + ";";
                    var dropCommand = new System.Data.SqlClient.SqlCommand(dropCommandString, connection);
                    dropCommand.CommandType = System.Data.CommandType.Text;
                    dropCommand.ExecuteNonQuery();
                    updateResults("Existing Table Dropped");
                }
                catch
                {
                    updateResults("No table found to drop");
                }
            }
        }

        private void createTable(bool geographyMode)
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(connectionString))
            {
                string mode = "geometry";
                if (geographyMode)
                    mode = "geography";
                connection.Open();

                    string commandString = @"CREATE TABLE [dbo].[" + tableName + @"] ([Id] INT NOT NULL PRIMARY KEY, [" + columnName + @"] [sys].[" + mode + @"] NOT NULL, );";
                    var command = new System.Data.SqlClient.SqlCommand(commandString, connection);
                    command.CommandType = System.Data.CommandType.Text;
                    command.ExecuteNonQuery();
                    updateResults("Table Created");
            }
            
        }

        private void updateResults(string newResults)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(results + newResults);
            sb.Append(Environment.NewLine);
            results = sb.ToString();
            resultsBox.Text = results;
        }
    }
}
