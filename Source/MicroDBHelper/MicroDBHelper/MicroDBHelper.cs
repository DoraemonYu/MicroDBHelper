/* 【MicroDBHelper】，用于封装与数据库操作相关的方法。  (Some Thinking memo)
 * 
 *  这个封装类的主要的目标：
 *  1.希望封装重复、重要的操作步骤，降低编码疏忽而引起的DB问题；
 *  2.而非功能方面的灵活或全面，因此功能上会少于下层的Helper。
 *  
 * 
 *  使用说明：
 *  1.提供3个基础的DB操作接口,其中“参数1”为SQL语句，“参数2”为参数数据，“参数3”为是否使用事务(传null时，表示不使用事务，新建连接去处理)
      MicroDBHelper.ExecuteNonQuery(sql, paramValues, tran);
      MicroDBHelper.ExecuteDataTable(sql, paramValues, tran);
      MicroDBHelper.ExecuteScalar(sql, paramValues, tran); 
 *    
 *  2.使用事务时，请用using包括，
      using (var tran = MicroDBHelper.UseTransaction(隔离等级)) 
      {
         MicroDBHelper.ExecuteNonQuery(sql, paramValues, tran);
         tran.MarkSuccess();
      }
 *    成功执行的最后，请调用MarkSuccess()，事务结束前会自动Commit；发生错误或者不显式MarkSuccess()的情况下，均会自动Rollback所有修改;
 *     
 *  3.隔离级别，根据实际情况进行选择。选择依据：
      未提交读（read uncommitted）   当事务A更新某条数据时，不容许其他事务来更新该数据，但可以读取。
      提交读（read committed） 	    当事务A更新某条数据时，不容许其他事务进行任何操作包括读取，但事务A读取时，其他事务可以进行读取、更新
      重复读（repeatable read） 	    当事务A更新数据时，不容许其他事务进行任何操作，但当事务A进行读取时，其他事务只能读取，不能更新。
      序列化（serializable）         最严格的隔离级别，事务必须依次进行。
 *    
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Text;
#if ASYNC_SUPPORT
using System.Linq;
using System.Threading.Tasks;
#endif
using AsyncSQLHelper = Microsoft.SqlHelper;
using SyncSQLHelper  = Microsoft.SqlHelper;

namespace MicroDBHelpers
{

#if lang_zh
    /// <summary>
    /// 微型数据库帮助类(V3，支持异步SQL + 允许多数据库连接)
    /// </summary>
#else
    /// <summary>
    /// Friendly and micro DBHelper (V3, with support of "Async SQL" and "Multiple DB connection" ) 
    /// </summary>
#endif
    public sealed class MicroDBHelper
    {

        /* 目前只封装 MS-SQLHelper;  Currently is fource MS-SQLHelper   */


        //----------Consts------------

#region 常量值
		 
#if lang_zh
        /// <summary>
        /// 默认的别名
        /// </summary>
#else
        /// <summary>
        /// Default alias Name for use
        /// </summary>
#endif
        public const string ALIAS_NAME_DEFAULT = "DEFAULT";

#endregion

#region 错误信息

#if lang_zh
        const string ERRMSG_TRANSACTION_IS_NULL = "指定的事务为空！";
#else
        const string ERRMSG_TRANSACTION_IS_NULL = "The Specified Transaction is null!";
#endif

#endregion


        //----------Members-----------

#region 连接字符串

#if lang_zh
        /// <summary>
        /// 设置连接字符串
        /// </summary>
        /// <param name="m_connectionString">连接字符串</param>
        /// <param name="m_ConnectionAliasName">连接别名；如果没起别名，默认为DEFAULT，会以此作为默认的连接字符串</param>
#else
        /// <summary>
        /// Set the connection string
        /// </summary>
        /// <param name="m_connectionString">connection string</param>
        /// <param name="m_ConnectionAliasName">the Alias Name of Connection (if not pass name,it will use the DEFAULT name instead.)</param>
#endif
        public static void SetConnection(string m_connectionString,string m_ConnectionAliasName = ALIAS_NAME_DEFAULT)
        {
            //检查连接字符串
            SqlConnectionStringBuilder builder = null;
            try
            {
                builder = new SqlConnectionStringBuilder(m_connectionString);
            }
            catch (Exception ex)
            {
#if lang_zh
                const string errMsg = "连接字符串有错误。";
#else
                const string errMsg = "Connection String is invalid.";
#endif

                throw new ArgumentException(errMsg, "m_connectionString", ex);
            }

            //##异步的
            /* 启用异步SQL支持的时候，连接字符串必须包含以下属性。
             * https://msdn.microsoft.com/zh-cn/library/system.data.sqlclient.sqlconnectionstringbuilder.asynchronousprocessing(v=vs.110).aspx
             * 
             * 标记为“允许异步”。即：
             * 1.没有标记时，及时使用Command的Async方法，依然是同步操作；
             * 2.有标记时，如果继续使用Command的同步方法，仍然能用于同步操作。
             */
            builder.AsynchronousProcessing              = true;
            string finalConnectionString                = builder.ToString();

            //检查连接是否可用,同时获取SqlServer的产品版本号
            string version;
            try
            {
                version = AsyncSQLHelper.ExecuteScalar(finalConnectionString, CommandType.Text, "SELECT SERVERPROPERTY('ProductVersion')") as String;
            }
            catch (Exception ex)
            {
#if lang_zh
                const string errMsg = "此连接字符串无法正常工作。";
#else
                const string errMsg = "This Connection String was unable to work properly.";
#endif
                throw new ArgumentException(errMsg, "m_connectionString", ex);
            }

            //记录结果
            var item                    = new ConnectionRepositoryItem
            {
                ConnectionString        = finalConnectionString,
                ServerProductVersion    = new Version(version)
            };
            ConnectionRepository.SetRepositoryItem(m_ConnectionAliasName, item);

        }

        /// <summary>
        /// 获取连接字符串
        /// </summary>
        /// <param name="m_ConnectionAliasName"></param>
        /// <returns>连接别名；如果没起别名，则使用默认的连接字符串</returns>
        internal static string GetConnection(string m_ConnectionAliasName = ALIAS_NAME_DEFAULT)
        {
            var targetItem = ConnectionRepository.GetRepositoryItem(m_ConnectionAliasName);
            if (targetItem != null)
                return targetItem.ConnectionString;
            else
            {
#if lang_zh
                const string errMsg1 = "预期的连接字符串不存在。";
                const string errMsg2 = "连接别名不存在:";
#else
                const string errMsg1 = "the target Connection String is not exist.";
                const string errMsg2 = "the Specified Alias Name was not setted.";  
#endif

                throw new InvalidOperationException(errMsg1, new System.ArgumentOutOfRangeException("m_ConnectionAliasName", errMsg2 + m_ConnectionAliasName));
            }
        }

#endregion


        //---------Control----------

#region 构造函数

        //隐藏默认构造函数
        private MicroDBHelper()
        {}
#endregion
		

        //------DB Operate Methods------
        
#region 查询，返回DataSet结果集

#if lang_zh
        /// <summary>
        /// 查询，返回DataSet结果集
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandParameters">参数集合</param>
        /// <param name="transaction">使用指定的事务处理</param>
        /// <param name="commandType">SQL语句 | 存储过程</param>
        /// <returns>DataSet结果集</returns>
#else
        /// <summary>
        /// Query，Get result of DataSet
        /// </summary>
        /// <param name="commandText">SQL statement</param>
        /// <param name="commandParameters">SqlParameter Collection</param>
        /// <param name="transaction">transaction</param>
        /// <param name="commandType">Text | StoredProcedure</param>
        /// <returns>DataSet</returns>
#endif
        public static DataSet ExecuteDataSet(string commandText, SqlParameter[] commandParameters, MicroDBTransaction transaction, CommandType commandType = CommandType.Text)
        {
            DataSet ds = SyncSQLHelper.ExecuteDataset(transaction.tran, commandType, commandText, commandParameters);
            return ds;
        }

#if lang_zh
        /// <summary>
        /// 查询，返回DataSet结果集
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandParameters">参数集合</param>
        /// <param name="commandType">SQL语句 | 存储过程</param>
        /// <param name="connectionAliasName">连接别名；如果没起别名，则使用默认的连接字符串</param>
        /// <returns>DataSet结果集</returns>
#else
        /// <summary>
        /// Query，Get result of DataSet
        /// </summary>
        /// <param name="commandText">SQL statement</param>
        /// <param name="commandParameters">SqlParameter Collection</param>
        /// <param name="commandType">Text | StoredProcedure</param>
        /// <param name="connectionAliasName">the Alias Name of Connection (if not pass name,it will use the DEFAULT name instead.)</param>
        /// <returns>DataSet</returns>
#endif
        public static DataSet ExecuteDataSet(string commandText, SqlParameter[] commandParameters, string connectionAliasName = ALIAS_NAME_DEFAULT, CommandType commandType = CommandType.Text)
        {
            DataSet ds = SyncSQLHelper.ExecuteDataset(GetConnection(connectionAliasName), commandType, commandText, commandParameters);
            return ds;
        }

#if ASYNC_SUPPORT
#if lang_zh
        /// <summary>
        /// 异步查询，返回DataSet结果集
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandParameters">参数集合</param>
        /// <param name="transaction">使用指定的事务处理</param>
        /// <param name="commandType">SQL语句 | 存储过程</param>
        /// <returns>DataSet结果集</returns>
#else
        /// <summary>
        /// async Query，Get result of DataSet
        /// </summary>
        /// <param name="commandText">SQL statement</param>
        /// <param name="commandParameters">SqlParameter Collection</param>
        /// <param name="transaction">transaction</param>
        /// <param name="commandType">Text | StoredProcedure</param>
        /// <returns>DataSet</returns>
#endif
        public static async Task<DataSet> ExecuteDataSetAsync(string commandText, SqlParameter[] commandParameters, MicroDBTransaction transaction, CommandType commandType = CommandType.Text)
        {
            DataSet ds = await AsyncSQLHelper.ExecuteDatasetAsync(transaction.tran, commandType, commandText, commandParameters);
            return ds;
        }
#endif

#if ASYNC_SUPPORT
#if lang_zh
        /// <summary>
        /// 查询，返回DataSet结果集
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandParameters">参数集合</param>
        /// <param name="commandType">SQL语句 | 存储过程</param>
        /// <param name="connectionAliasName">连接别名；如果没起别名，则使用默认的连接字符串</param>
        /// <returns>DataSet结果集</returns>
#else
        /// <summary>
        /// async Query，Get result of DataSet
        /// </summary>
        /// <param name="commandText">SQL statement</param>
        /// <param name="commandParameters">SqlParameter Collection</param>
        /// <param name="commandType">Text | StoredProcedure</param>
        /// <param name="connectionAliasName">the Alias Name of Connection (if not pass name,it will use the DEFAULT name instead.)</param>
        /// <returns>DataSet</returns>
#endif
        public static async Task<DataSet> ExecuteDataSetAsync(string commandText, SqlParameter[] commandParameters, string connectionAliasName = ALIAS_NAME_DEFAULT, CommandType commandType = CommandType.Text)
        {
            DataSet ds = await AsyncSQLHelper.ExecuteDatasetAsync(GetConnection(connectionAliasName), commandType, commandText, commandParameters);
            return ds;
        }
#endif

        #endregion

        #region 查询，返回DataTable结果集

#if lang_zh
        /// <summary>
        /// 查询，返回DataTable结果集
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandParameters">参数集合</param>
        /// <param name="transaction">使用指定的事务处理</param>
        /// <param name="commandType">SQL语句 | 存储过程</param>
        /// <returns>DataTable结果集</returns>
#else
        /// <summary>
        /// Query，Get result of DataTable
        /// </summary>
        /// <param name="commandText">SQL statement</param>
        /// <param name="commandParameters">SqlParameter Collection</param>
        /// <param name="transaction">transaction</param>
        /// <param name="commandType">Text | StoredProcedure</param>
        /// <returns>DataTable</returns>
#endif
        public static DataTable ExecuteDataTable(string commandText, SqlParameter[] commandParameters, MicroDBTransaction transaction, CommandType commandType = CommandType.Text)
        {
            DataSet ds = ExecuteDataSet(commandText, commandParameters, transaction, commandType);
            
            //返回结果
            if (ds == null || ds.Tables == null || ds.Tables.Count <= 0)
                return null;
            return ds.Tables[0];
        }

#if lang_zh
        /// <summary>
        /// 查询，返回DataTable结果集
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandParameters">参数集合</param>
        /// <param name="commandType">SQL语句 | 存储过程</param>
        /// <param name="connectionAliasName">连接别名；如果没起别名，则使用默认的连接字符串</param>
        /// <returns>DataTable结果集</returns>
#else
        /// <summary>
        /// Query，Get result of DataTable
        /// </summary>
        /// <param name="commandText">SQL statement</param>
        /// <param name="commandParameters">SqlParameter Collection</param>
        /// <param name="commandType">Text | StoredProcedure</param>
        /// <param name="connectionAliasName">the Alias Name of Connection (if not pass name,it will use the DEFAULT name instead.)</param>
        /// <returns>DataTable</returns>
#endif
        public static DataTable ExecuteDataTable(string commandText, SqlParameter[] commandParameters, string connectionAliasName = ALIAS_NAME_DEFAULT, CommandType commandType = CommandType.Text)
        {
            DataSet ds = ExecuteDataSet(commandText, commandParameters, connectionAliasName, commandType);
            
            //返回结果
            if (ds == null || ds.Tables == null || ds.Tables.Count <= 0)
                return null;
            return ds.Tables[0];
        }


#if ASYNC_SUPPORT
#if lang_zh
        /// <summary>
        /// 异步查询，返回DataTable结果集
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandParameters">参数集合</param>
        /// <param name="transaction">使用指定的事务处理</param>
        /// <param name="commandType">SQL语句 | 存储过程</param>
        /// <returns>DataTable结果集</returns>
#else
        /// <summary>
        /// async Query，Get result of DataTable
        /// </summary>
        /// <param name="commandText">SQL statement</param>
        /// <param name="commandParameters">SqlParameter Collection</param>
        /// <param name="transaction">transaction</param>
        /// <param name="commandType">Text | StoredProcedure</param>
        /// <returns>DataTable</returns>
#endif
        public static async Task<DataTable> ExecuteDataTableAsync(string commandText, SqlParameter[] commandParameters, MicroDBTransaction transaction, CommandType commandType = CommandType.Text)
        {
            DataSet ds = await ExecuteDataSetAsync(commandText, commandParameters, transaction, commandType);

            //返回结果
            if (ds == null || ds.Tables == null || ds.Tables.Count <= 0)
                return null;
            return ds.Tables[0];
        }
#endif

#if ASYNC_SUPPORT
#if lang_zh
        /// <summary>
        /// 查询，返回DataTable结果集
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandParameters">参数集合</param>
        /// <param name="commandType">SQL语句 | 存储过程</param>
        /// <param name="connectionAliasName">连接别名；如果没起别名，则使用默认的连接字符串</param>
        /// <returns>DataTable结果集</returns>
#else
        /// <summary>
        /// async Query，Get result of DataTable
        /// </summary>
        /// <param name="commandText">SQL statement</param>
        /// <param name="commandParameters">SqlParameter Collection</param>
        /// <param name="commandType">Text | StoredProcedure</param>
        /// <param name="connectionAliasName">the Alias Name of Connection (if not pass name,it will use the DEFAULT name instead.)</param>
        /// <returns>DataTable</returns>
#endif
        public static async Task<DataTable> ExecuteDataTableAsync(string commandText, SqlParameter[] commandParameters, string connectionAliasName = ALIAS_NAME_DEFAULT, CommandType commandType = CommandType.Text)
        {
            DataSet ds = await ExecuteDataSetAsync(commandText, commandParameters, connectionAliasName, commandType);

            //返回结果
            if (ds == null || ds.Tables == null || ds.Tables.Count <= 0)
                return null;
            return ds.Tables[0];
        }
#endif

        #endregion

        #region 查询，返回单一结果

#if lang_zh
        /// <summary>
        /// 查询，返回单一结果
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandParameters">参数集合</param>
        /// <param name="transaction">使用指定的事务处理</param>
        /// <param name="commandType">SQL语句 | 存储过程</param>
        /// <returns>单一结果</returns>
#else
        /// <summary>
        /// Query，Get result of One Object
        /// </summary>
        /// <param name="commandText">SQL statement</param>
        /// <param name="commandParameters">SqlParameter Collection</param>
        /// <param name="transaction">transaction</param>
        /// <param name="commandType">Text | StoredProcedure</param>
        /// <returns>Object</returns>
#endif
        public static object ExecuteScalar(string commandText, SqlParameter[] commandParameters, MicroDBTransaction transaction, CommandType commandType = CommandType.Text)
        {
            return SyncSQLHelper.ExecuteScalar(transaction.tran, commandType, commandText, commandParameters);
        }

#if lang_zh
        /// <summary>
        /// 查询，返回单一结果
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandParameters">参数集合</param>
        /// <param name="commandType">SQL语句 | 存储过程</param>
        /// <param name="connectionAliasName">连接别名；如果没起别名，则使用默认的连接字符串</param>
        /// <returns>单一结果</returns>
#else
        /// <summary>
        /// Query，Get result of One Object
        /// </summary>
        /// <param name="commandText">SQL statement</param>
        /// <param name="commandParameters">SqlParameter Collection</param>
        /// <param name="commandType">Text | StoredProcedure</param>
        /// <param name="connectionAliasName">the Alias Name of Connection (if not pass name,it will use the DEFAULT name instead.)</param>
        /// <returns>Object</returns>
#endif
        public static object ExecuteScalar(string commandText, SqlParameter[] commandParameters, string connectionAliasName = ALIAS_NAME_DEFAULT, CommandType commandType = CommandType.Text)
        {
            return SyncSQLHelper.ExecuteScalar(GetConnection(connectionAliasName), commandType, commandText, commandParameters);
        }


#if ASYNC_SUPPORT
#if lang_zh
        /// <summary>
        /// 异步查询，返回单一结果
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandParameters">参数集合</param>
        /// <param name="transaction">使用指定的事务处理</param>
        /// <param name="commandType">SQL语句 | 存储过程</param>
        /// <returns>单一结果</returns>
#else
        /// <summary>
        /// async Query，Get result of One Object
        /// </summary>
        /// <param name="commandText">SQL statement</param>
        /// <param name="commandParameters">SqlParameter Collection</param>
        /// <param name="transaction">transaction</param>
        /// <param name="commandType">Text | StoredProcedure</param>
        /// <returns>Object</returns>
#endif
        public static async Task<object> ExecuteScalarAsync(string commandText, SqlParameter[] commandParameters, MicroDBTransaction transaction, CommandType commandType = CommandType.Text)
        {
            if (transaction == null) throw new InvalidOperationException(ERRMSG_TRANSACTION_IS_NULL, new ArgumentNullException("transaction"));

            return await AsyncSQLHelper.ExecuteScalarAsync(transaction.tran, commandType, commandText, commandParameters);
        }
#endif

#if ASYNC_SUPPORT
#if lang_zh
        /// <summary>
        /// 异步查询，返回单一结果
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandParameters">参数集合</param>
        /// <param name="commandType">SQL语句 | 存储过程</param>
        /// <param name="connectionAliasName">连接别名；如果没起别名，则使用默认的连接字符串</param>
        /// <returns>单一结果</returns>
#else
        /// <summary>
        /// async Query，Get result of One Object
        /// </summary>
        /// <param name="commandText">SQL statement</param>
        /// <param name="commandParameters">SqlParameter Collection</param>
        /// <param name="commandType">Text | StoredProcedure</param>
        /// <param name="connectionAliasName">the Alias Name of Connection (if not pass name,it will use the DEFAULT name instead.)</param>
        /// <returns>Object</returns>
#endif
        public static async Task<object> ExecuteScalarAsync(string commandText, SqlParameter[] commandParameters, string connectionAliasName = ALIAS_NAME_DEFAULT, CommandType commandType = CommandType.Text)
        {
            return await AsyncSQLHelper.ExecuteScalarAsync(GetConnection(connectionAliasName), commandType, commandText, commandParameters);
        }
#endif
        #endregion

        #region 执行，返回受影响行数

#if lang_zh
        /// <summary>
        /// 执行
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandParameters">参数集合</param>
        /// <param name="transaction">使用指定的事务处理</param>
        /// <param name="commandType">SQL语句 | 存储过程</param>
        /// <returns>受影响行数</returns>
#else
        /// <summary>
        /// ExecuteNonQuery
        /// </summary>
        /// <param name="commandText">SQL statement</param>
        /// <param name="commandParameters">SqlParameter Collection</param>
        /// <param name="transaction">transaction</param>
        /// <param name="commandType">Text | StoredProcedure</param>
        /// <returns>the affected rows</returns>
#endif
        public static int ExecuteNonQuery(string commandText, SqlParameter[] commandParameters, MicroDBTransaction transaction, CommandType commandType = CommandType.Text)
        {
            if (transaction == null) throw new InvalidOperationException(ERRMSG_TRANSACTION_IS_NULL, new ArgumentNullException("transaction"));

            return SyncSQLHelper.ExecuteNonQuery(transaction.tran, commandType, commandText, commandParameters);
        }

#if lang_zh
        /// <summary>
        /// 执行
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandParameters">参数集合</param>
        /// <param name="commandType">SQL语句 | 存储过程</param>
        /// <param name="connectionAliasName">连接别名；如果没起别名，则使用默认的连接字符串</param>
        /// <returns>受影响行数</returns>
#else
        /// <summary>
        /// ExecuteNonQuery
        /// </summary>
        /// <param name="commandText">SQL statement</param>
        /// <param name="commandParameters">SqlParameter Collection</param>
        /// <param name="commandType">Text | StoredProcedure</param>
        /// <param name="connectionAliasName">the Alias Name of Connection (if not pass name,it will use the DEFAULT name instead.)</param>
        /// <returns>the affected rows</returns>
#endif
        public static int ExecuteNonQuery(string commandText, SqlParameter[] commandParameters, string connectionAliasName = ALIAS_NAME_DEFAULT, CommandType commandType = CommandType.Text)
        {
            return SyncSQLHelper.ExecuteNonQuery(GetConnection(connectionAliasName), commandType, commandText, commandParameters);
        }

#if ASYNC_SUPPORT
#if lang_zh
        /// <summary>
        /// 异步执行
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandParameters">参数集合</param>
        /// <param name="transaction">使用指定的事务处理</param>
        /// <param name="commandType">SQL语句 | 存储过程</param>
        /// <returns>受影响行数</returns>
#else
        /// <summary>
        /// async ExecuteNonQuery
        /// </summary>
        /// <param name="commandText">SQL statement</param>
        /// <param name="commandParameters">SqlParameter Collection</param>
        /// <param name="transaction">transaction</param>
        /// <param name="commandType">Text | StoredProcedure</param>
        /// <returns>the affected rows</returns>
#endif
        public static async Task<int> ExecuteNonQueryAsync(string commandText, SqlParameter[] commandParameters, MicroDBTransaction transaction, CommandType commandType = CommandType.Text)
        {
            if (transaction == null) throw new InvalidOperationException(ERRMSG_TRANSACTION_IS_NULL, new ArgumentNullException("transaction"));

            return await AsyncSQLHelper.ExecuteNonQueryAsync(transaction.tran, commandType, commandText, commandParameters);
        }
#endif

#if ASYNC_SUPPORT
#if lang_zh
        /// <summary>
        /// 异步执行
        /// </summary>
        /// <param name="commandText">SQL语句</param>
        /// <param name="commandParameters">参数集合</param>
        /// <param name="commandType">SQL语句 | 存储过程</param>
        /// <param name="connectionAliasName">连接别名；如果没起别名，则使用默认的连接字符串</param>
        /// <returns>受影响行数</returns>
#else
        /// <summary>
        /// async ExecuteNonQuery
        /// </summary>
        /// <param name="commandText">SQL statement</param>
        /// <param name="commandParameters">SqlParameter Collection</param>
        /// <param name="commandType">Text | StoredProcedure</param>
        /// <param name="connectionAliasName">the Alias Name of Connection (if not pass name,it will use the DEFAULT name instead.)</param>
        /// <returns>the affected rows</returns>
#endif
        public static async Task<int> ExecuteNonQueryAsync(string commandText, SqlParameter[] commandParameters, string connectionAliasName = ALIAS_NAME_DEFAULT, CommandType commandType = CommandType.Text)
        {
            return await AsyncSQLHelper.ExecuteNonQueryAsync(GetConnection(connectionAliasName), commandType, commandText, commandParameters);
        }
#endif

        #endregion


        //------Extend Methods----------

        #region 获取事务支持的对象


#if lang_zh
        /// <summary>
        /// 获取事务支持的对象<para />
        /// ( 成功执行的最后，请调用MarkSuccess()，事务结束前会自动Commit；发生错误或者不显式MarkSuccess()的情况下，均会自动Rollback所有修改 )
        /// </summary>
        /// <param name="IsolationLevel">隔离等级</param>
        /// <param name="ConnectionAliasName">连接别名；如果没起别名，则使用默认的连接字符串</param>
#else
        /// <summary>
        /// Get new MicroDBTransaction Object with the Transaction support<para />
        /// ( when transaction is success，call MarkSuccess() method at last，then the transaction will automatically COMMIT; when some exception is happend then case the code NOT to call MarkSuccess() method,  the transaction will automatically Rollback )
        /// </summary>
        /// <param name="IsolationLevel">Level of Isolation</param>
        /// <param name="ConnectionAliasName">the Alias Name of Connection (if not pass name,it will use the DEFAULT name instead.)</param>
#endif

        public static MicroDBTransaction UseTransaction(IsolationLevel IsolationLevel, string ConnectionAliasName = ALIAS_NAME_DEFAULT)
        {
            MicroDBTransaction result = new MicroDBTransaction(IsolationLevel, ConnectionAliasName);

            bool ready = result.MakeReady();
            if (ready == false)
            {
#if lang_zh
                const string errMsg = "数据库连接打开失败!";
#else
                const string errMsg = "DB connection open faild!";
#endif

                throw new InvalidOperationException(errMsg);
            }

            return result;
        }

#endregion

    }
}

