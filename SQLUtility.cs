﻿using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace CPUFramework
{
    public class SQLUtility
    {
        public static string ConnectionString = "";

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
                    if (loadtable== true)
                    {
                        dt.Load(dr);
                    }
                }
                catch(SqlException ex)
                {
                    string msg= ParseConstraintMsg(ex.Message);
                    throw new Exception(msg);
                }
                catch (InvalidCastException ex)
                {
                    throw new Exception(cmd.CommandText + ":" + ex.Message+ ex);
                }
            }
            SetAllColumnsAllowNull(dt);
            return dt;
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

        private static void SetAllColumnsAllowNull(DataTable dt)
        {
            foreach(DataColumn c in dt.Columns)
            {
                c.AllowDBNull = true;
            }
        }

        public static string GetSQL(SqlCommand cmd)
        {
            string val = "";
#if DEBUG
            StringBuilder sb= new StringBuilder();
            if (cmd.Connection != null)
            {
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
           foreach(DataRow r in dt.Rows) { 
                foreach(DataColumn c in dt.Columns)
                {
                    Debug.Print(c.ColumnName + " = "+ r[c.ColumnName].ToString());
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
            string n = " ";
            DataTable dt = GetDataTable(sql);
            return n;
        }

        public static DateTime GetFirstColumnFirstRowValueDate(string sql) { 
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
            try
            {
                cmd.Parameters[paramname].Value = value;

            }
            catch (Exception ex)
            {
                throw new Exception(cmd.CommandText+ ": "+  ex.Message, ex);
            }
        }


            public static string ParseConstraintMsg(string msg)
        {
            string origmsg = msg;
            string prefex = "ck_";
            string msgend = "";
            if (msg.Contains(prefex) == false)
            {
                if (msg.Contains("u_")){
                    prefex = "U_";
                    msgend = "must be uniqe";
                }
            else if (msg.Contains("f_"))
                {
                    prefex = "f_";
                }
            }
            if (msg.Contains(prefex))
            {
                msg = msg.Replace("\"", "' ");
                int pos = msg.IndexOf(prefex) + prefex.Length;
                msg = msg.Substring(pos);
                pos = msg.IndexOf("'");
                if (pos == -1)
                {
                    msg= origmsg;
                }
                else
                {
                    msg = msg.Substring(0, pos);
                    msg = msg.Replace("_", " ");
                    msg = msg + msgend;

                    if (prefex == "f_"){
                        var words= msg.Split(' ');
                        if (words.Length > 1)
                        {
                            msg = $" Cannot delete {words[0]} becasue it has a related {words[1]} record";
                        }

                    }
                }
            }
            return msg;
        }

    }


}
