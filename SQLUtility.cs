using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace CPUFramework
{
    public class SQLUtility
    {
        private static string ConnectionString = "";

        public static void SetConnectionString(string connstring, bool tryopen, string username= "", string password= "")
        {
            ConnectionString= connstring;
            if(username != "")
            {
                SqlConnectionStringBuilder b = new();
                b.ConnectionString= ConnectionString;
                b.UserID = username;
                b.Password=  password;
                ConnectionString= b.ConnectionString;
            }
            if (tryopen)
            {
                using(SqlConnection conn= new SqlConnection(ConnectionString))
                {
                    conn.Open();
                }
            }
        }

        public static SqlCommand GetSqlcommand(string sprocname)
        {
            SqlCommand cmd;
            using (SqlConnection conn = new(ConnectionString))
            {
                cmd = new(sprocname, conn);
                cmd.CommandType = CommandType.StoredProcedure;

                conn.Open();
                SqlCommandBuilder.DeriveParameters(cmd);
            }
            return cmd;
        }
        public static DataTable GetDataTable(SqlCommand cmd)
        {
            return DoExecuteSQL(cmd, true);
        }

        public static void SaveDataTable(DataTable dt, string sprocname)
        {
            var rows = dt.Select("", "", DataViewRowState.Added | DataViewRowState.ModifiedCurrent);
            foreach (DataRow r in rows)
            {
                SaveDataRow(r, sprocname, false);
            }
        }

        public static void SaveDataRow(DataRow row, string sprocname, bool acceptChanges = true)
        {
            SqlCommand cmd = GetSqlcommand(sprocname);
            foreach (DataColumn col in row.Table.Columns)
            {
                string paramname = $"@{col.ColumnName}";
                if (cmd.Parameters.Contains(paramname))
                {
                    cmd.Parameters[paramname].Value = row[col.ColumnName];
                }
            }
            DoExecuteSQL(cmd, false);
            foreach (SqlParameter p in cmd.Parameters)
            {
                if (p.Direction == ParameterDirection.InputOutput)
                {
                    string columnname = p.ParameterName.Substring(1);
                    if (row.Table.Columns.Contains(columnname))
                    {
                        row[columnname] = p.Value;
                    }
                }
            }
            if (acceptChanges == true)
            {
                row.Table.AcceptChanges();
            }
        }

        private static DataTable DoExecuteSQL(SqlCommand cmd, bool loadtable)
        {
            DataTable dt = new();
            using (SqlConnection conn = new SqlConnection(SQLUtility.ConnectionString))
            {

                conn.Open();
                cmd.Connection = conn;
                Debug.Print(GetSQL(cmd));
                try
                {
                    SqlDataReader dr = cmd.ExecuteReader();
                    CheckReturnValue(cmd);

                    if (loadtable == true)
                    {
                        dt.Load(dr);
                    }
                }
                catch (SqlException ex)
                {
                    string msg = ParseConstraintMsg(ex.Message);
                    throw new Exception(msg);
                }
                catch (InvalidCastException ex)
                {
                    throw new Exception(cmd.CommandText + ":" + ex.Message + ex);
                }
            }
            SetAllColumnsProperties(dt);
            return dt;
        }

        private static void CheckReturnValue(SqlCommand cmd)
        {
            int returnvalue = 0;
            string msg = "";
            if (cmd.CommandType == CommandType.StoredProcedure)
            {
                foreach (SqlParameter p in cmd.Parameters)
                {
                    if (p.Direction == ParameterDirection.ReturnValue)
                    {
                        if (p.Value != null)
                        {
                            returnvalue = (int)p.Value;
                        }
                    }
                    else if (p.ParameterName.ToLower() == "@message")
                    {
                        if (p.Value != null)
                        {
                            msg = p.Value.ToString();
                        }
                    }
                }
                if (returnvalue == 1)
                {
                    if (msg == "")
                    {
                        msg = $"{cmd.CommandText}did not do action that was requested.";
                    }
                    throw new Exception(msg);

                }
            }
        }


        public static DataTable GetDataTable(string sqlstatement)
        {
            Debug.Print(sqlstatement);
            return DoExecuteSQL(new SqlCommand(sqlstatement), true);
        }

        public static void ExecuteSQL(SqlCommand cmd)
        {
            DoExecuteSQL(cmd, false);
        }


        public static void ExecuteSQL(string sqlstatemnt)
        {
            GetDataTable(sqlstatemnt);
        }

        private static void SetAllColumnsProperties(DataTable dt)
        {
            foreach (DataColumn c in dt.Columns)
            {
                c.AllowDBNull = true;
                c.AutoIncrement = false;
                c.ReadOnly = false;
            }
        }
        public static int GetValueFromFirstRowAsInt(DataTable dt, string columnname)
        {
            int value = 0;
            if (dt.Rows.Count > 0)
            {
                DataRow r = dt.Rows[0];
                if (r[columnname] != null && r[columnname] is int)
                {
                    value = (int)r[columnname];
                }
            }
            return value;
        }

        public static string GetValueFromFirstRowAsString(DataTable dt, string columnname)
        {
            string value = "";
            if (dt.Rows.Count > 0)
            {
                DataRow r = dt.Rows[0];
                if (r[columnname] != null && r[columnname] is string)
                {
                    value = (string)r[columnname];
                }
            }
            return value;
        }
        public static bool TableHasChanges(DataTable dt)
        {
            bool b = false;
            if (dt.GetChanges() != null)
            {
                b = true;
            }
            return b;
        }

        public static string GetSQL(SqlCommand cmd)
        {
            string val = "";
#if DEBUG
            StringBuilder sb = new StringBuilder();
            if (cmd.Connection != null)
            {
                //sb.AppendLine($"--{cmd.Connection.ConnectionString}");
                sb.AppendLine($"--{cmd.Connection.DataSource}");
                sb.AppendLine($"use {cmd.Connection.Database}");
                sb.AppendLine("go");
            }
            if (cmd.CommandType == CommandType.StoredProcedure)
            {
                sb.AppendLine($"exec {cmd.CommandText}");
                int paramcount = cmd.Parameters.Count - 1;
                int paramnum = 0;
                string comma = ",";
                foreach (SqlParameter p in cmd.Parameters)
                {
                    if (p.Direction != ParameterDirection.ReturnValue)
                    {
                        if (paramnum == paramcount)
                        {
                            comma = "";
                        }
                        sb.AppendLine($"{p.ParameterName}= {(p.Value == null ? "null" : p.Value.ToString())}{comma}");

                    }
                    paramnum++;
                }
            }
            else
            {
                sb.AppendLine(cmd.CommandText);
            }
            val = sb.ToString();
#endif
            return val;
        }

        public static void DebugPringDataTable(DataTable dt)
        {
            foreach (DataRow r in dt.Rows)
            {
                foreach (DataColumn c in dt.Columns)
                {
                    Debug.Print(c.ColumnName + " = " + r[c.ColumnName].ToString());
                }
            }
        }

        public static int GetFirstColumnFirstRowValue(string sql)
        {
            int n = 0;
            DataTable dt = GetDataTable(sql);
            if (dt.Rows.Count > 0 && dt.Columns.Count > 0)
            {
                if (dt.Rows[0][0] != DBNull.Value)
                {
                    int.TryParse(dt.Rows[0][0].ToString(), out n);
                }
            }
            return n;
        }


        public static string GetFirstColumnFirstRowValuestring(string sql)
        {
            string firstColumnValue = "";

            DataTable dt = GetDataTable(sql);

            // Check if the DataTable has rows and columns
            if (dt != null && dt.Rows.Count > 0 && dt.Columns.Count > 0)
            {
                // Check if the value in the first row of the first column is not DBNull
                if (dt.Rows[0][0] != DBNull.Value)
                {
                    firstColumnValue = dt.Rows[0][0].ToString();
                }
            }
            return firstColumnValue;
        }

        public static DateTime GetFirstColumnFirstRowValueDate(string sql)
        {
            DateTime result = DateTime.Now;
            DataTable dt = GetDataTable(sql);
            if (dt.Rows.Count > 0 && dt.Columns.Count > 0)
            {
                if (dt.Rows[0][0] != DBNull.Value)
                {
                    DateTime parsedDate;
                    if (DateTime.TryParse(dt.Rows[0][0].ToString(), out parsedDate))
                    {
                        result = parsedDate;
                    }
                }
            }
            return result;
        }

        public static void SetParamValue(SqlCommand cmd, string paramname, object value)
        {
            if(paramname.StartsWith("@")== false)
            {
                paramname = "@" + paramname;
            }
            try
            {
                cmd.Parameters[paramname].Value = value;

            }
            catch (Exception ex)
            {
                throw new Exception(cmd.CommandText + ": " + ex.Message, ex);
            }
        }
        public static string ParseConstraintMsg(string msg)
        {
            string origmsg = msg;
            string prefix = "ck_";
            string msgend = "";
            string notnullprefix = "Cannot insert the value NULL into column '";

            msg = msg.ToLower(); // Normalize case

            if (!msg.Contains(prefix))
            {
                if (msg.Contains("unique key"))
                {
                    prefix = "unique key";
                    msgend = " ";
                }
                else if (msg.Contains("f_"))
                {
                    prefix = "f_";
                }
                else if (msg.Contains(notnullprefix.ToLower()))
                {
                    prefix = notnullprefix.ToLower();
                    msgend = " cannot be blank.";
                }
            }

            if (msg.Contains(prefix))
            {
                msg = msg.Replace("\"", "' "); // Standardize quotes

                int pos = msg.IndexOf(prefix) + prefix.Length;
                msg = msg.Substring(pos).Trim();

                if (prefix == "unique key")
                {
                    int constraintNameStart = msg.IndexOf("'") + 1;
                    int constraintNameEnd = msg.IndexOf("'", constraintNameStart);
                    if (constraintNameStart > 0 && constraintNameEnd > constraintNameStart)
                    {
                        string constraintName = msg.Substring(constraintNameStart, constraintNameEnd - constraintNameStart);
                        msg = FormatConstraintMessage(constraintName, msgend);
                    }
                    else
                    {
                        msg = origmsg; // Fallback if parsing fails
                    }
                }
                else
                {
                    pos = msg.IndexOf("'");
                    if (pos != -1)
                    {
                        msg = msg.Substring(0, pos).Replace("_", " ");
                    }

                    if (prefix == "f_")
                    {
                        var words = msg.Split(' ');
                        if (words.Length > 1)
                        {
                            msg = $"Cannot delete {words[0]} because it has a related {words[1]} record";
                        }
                    }
                    else
                    {
                        // Now we correctly remove the first word **after** the prefix
                        msg = FormatConstraintMessage(msg, msgend);
                    }
                }
            }

            return msg;
        }

        // Helper function to remove the first word **after** the prefix and format the message correctly
        private static string FormatConstraintMessage(string constraintName, string msgend)
        {
            string[] words = constraintName.Split('_');
            if (words.Length > 2)  // Ensure at least a prefix + two words exist
            {
                return string.Join(" ", words.Skip(2)) + msgend;  // Skip the first word after the prefix too
            }
            return constraintName + msgend; // Fallback in case of unexpected format
        }





    }
}