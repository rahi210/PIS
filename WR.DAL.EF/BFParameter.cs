﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;

namespace WR.DAL.EF
{
    public class BFParameters
    {
        public static string providerName = "";

        public List<DbParameter> DbParameters { get { return _pList; } }

        private List<DbParameter> _pList = new List<DbParameter>();

        public void Add(string parameterName, object value)
        {
            Add(parameterName, value, DbType.String, ParameterDirection.Input, 0);
        }

        public void Add(string parameterName, object value, DbType dbType)
        {
            Add(parameterName, value, dbType, ParameterDirection.Input, 0);
        }

        public void Add(string parameterName, object value, ParameterDirection direction)
        {
            Add(parameterName, value, DbType.String, direction, 0);
        }

        public void Add(string parameterName, object value, DbType dbType, ParameterDirection direction)
        {
            Add(parameterName, value, dbType, direction, 0);
        }

        public void Add(string parameterName, object value, DbType dbType, ParameterDirection direction, int size)
        {
            if (_pList.Exists(p => p.ParameterName == parameterName))
                throw new ArgumentException("已存在具有相同参数名称的参数");

            DbParameter dp = GetDbParameter();
            dp.ParameterName = parameterName;
            dp.Value = value;
            dp.DbType = dbType;
            dp.Direction = direction;
            dp.Size = size;
            _pList.Add(dp);
        }

        public static BFParameters CreateParameters()
        {
            return new BFParameters();
        }

        ///// <summary>
        ///// 获取执行的sql
        ///// </summary>
        ///// <param name="proBuilder"></param>
        ///// <returns></returns>
        //public DbParameter[] GetDbParameters(ref string proBuilder)
        //{
        //    StringBuilder proc = new StringBuilder("EXEC ");
        //    proc.AppendFormat("{0}", proBuilder);

        //    _pList.ForEach((p) =>
        //    {
        //        if (ParameterDirection.Input == p.Direction)
        //            proc.AppendFormat(" {0},", p.ParameterName);
        //        else
        //            proc.AppendFormat(" {0} OUTPUT,", p.ParameterName);
        //    });

        //    proBuilder = proc.ToString().TrimEnd(new char[] { ',' });
        //    return _pList.ToArray();
        //}
        /// <summary>
        /// 获取执行的sql
        /// </summary>
        /// <param name="proBuilder"></param>
        /// <returns></returns>
        public DbParameter[] GetDbParameters(ref string proBuilder)
        {
            StringBuilder proc = new StringBuilder("begin sp_date_archive( ");
           
            _pList.ForEach((p) =>
            {
                proc.AppendFormat(" {0},", p.ParameterName);
            });

            proBuilder = string.Format("{0});end;", proc.ToString().TrimEnd(new char[] { ',' }));

            return _pList.ToArray();
        }

        /// <summary>
        /// 生成参数
        /// </summary>
        /// <returns></returns>
        private DbParameter GetDbParameter()
        {
            if (string.IsNullOrEmpty(providerName))
                providerName = System.Configuration.ConfigurationManager.ConnectionStrings["DBString"].ProviderName;

            return DbProviderFactories.GetFactory(providerName).CreateParameter();
        }
    }
}