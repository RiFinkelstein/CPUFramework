//using System.Data.SqlClient;
using Microsoft.Data.SqlClient; // Ensure this is included
using System.ComponentModel;
using System.Data;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CPUFramework
{
    public class bizObject : INotifyPropertyChanged
    {
        string _tablename = ""; string _getsproc = ""; string _updatesproc = ""; string _deletesproc = "";
        string _primarykeyname = "";
        string _primarykeyparamname = "";
        DataTable _datatable = new();
        List<PropertyInfo> _properties = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public bizObject()
        {
            Type t = this.GetType();
            _tablename = t.Name;
            if (_tablename.ToLower().StartsWith("biz")) { _tablename = _tablename.Substring(3); }
            _getsproc = _tablename + "Get";
            _updatesproc = _tablename + "Update";
            _deletesproc = _tablename + "Delete";
            _primarykeyname = _tablename + "ID";
            _primarykeyparamname = "@" + _primarykeyname;
            _properties = t.GetProperties().ToList<PropertyInfo>();

        }
        public DataTable Load(int primarykeyvalue)
        {
            DataTable dt = new();
            SqlCommand cmd = SQLUtility.GetSqlcommand(_getsproc);
            SQLUtility.SetParamValue(cmd, _primarykeyparamname, primarykeyvalue);
            dt = SQLUtility.GetDataTable(cmd);
            if (dt.Rows.Count > 0)
            {
                LoadProps(dt.Rows[0]);
            }
            _datatable = dt;
            return dt;
        }
        private void LoadProps(DataRow dr)
        {
            foreach (DataColumn col in dr.Table.Columns)
            {
                SetProp(col.ColumnName, dr[col.ColumnName]);
            }
        }

        public void Save(DataTable datatable)
        {
            if (datatable.Rows.Count == 0)
            {
                throw new Exception($"cannot call {_tablename}  save method becasue there are no rows in the table");
            }
            DataRow r = datatable.Rows[0];
            SQLUtility.SaveDataRow(r, _updatesproc);
        }

        public void Save()
        {
            SqlCommand cmd = SQLUtility.GetSqlcommand(_updatesproc);
            foreach (SqlParameter param in cmd.Parameters)
            {
                var prop = GetProp(param.ParameterName, true, false);
                if (prop != null)
                {
                    object? val = prop.GetValue(this);
                    if (val == null) { val = DBNull.Value; }
                    param.Value = val;
                }
            }
            SQLUtility.ExecuteSQL(cmd);
            foreach (SqlParameter param in cmd.Parameters)
            {
                if (param.Direction == ParameterDirection.InputOutput)
                {
                    SetProp(param.ParameterName, param.Value);

                }
            }
        }
        public void Delete(int id)
        {
            SqlCommand cmd = SQLUtility.GetSqlcommand(_deletesproc);
            SQLUtility.SetParamValue(cmd, _primarykeyparamname, id);
            SQLUtility.ExecuteSQL(cmd);
        }
        public void Delete()
        {
            PropertyInfo? prop = GetProp(_primarykeyname, true, false);
            if (prop != null)
            {
                object id = prop.GetValue(this);
                if (id != null)
                {
                    this.Delete((int)id);
                }
            }
        }
        public void Delete(DataTable datatable)
        {
            int id = (int)datatable.Rows[0][_primarykeyname];
Delete(id);
        }

        private PropertyInfo? GetProp(string propname, bool ForRead, bool ForWrite)
        {
            propname = propname.ToLower();
            if (propname.StartsWith("@")) { propname = propname.Substring(1); }

            PropertyInfo? prop = _properties.FirstOrDefault(p =>
                p.Name.ToLower() == propname
                && (ForRead == false || p.CanRead == true)
                && (ForWrite == false || p.CanWrite == true)
                );
            return prop;
        }

        private void SetProp(string propname, object value)
        {
            var prop = GetProp(propname, false, true);
            if (prop != null)
            {
                if (value == DBNull.Value)
                {
                    value = null;
                }
                prop.SetValue(this, value);
            }
        }

        protected void InvokePropertyChanged([CallerMemberName] string propertyname = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

    }
}
