using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Runtime.CompilerServices;
using System.Text;
#if ASYNC_SUPPORT
using System.Threading.Tasks;
using System.Linq;
#endif

namespace MicroDBHelpers
{

#if lang_zh
    /// <summary>
    /// 服务于"MicroDBHelper"，配套的 DB事务支持
    /// </summary>
#else
    /// <summary>
    /// Offer firendly support of Transaction for the MicroDBHelper
    /// </summary>
#endif
    public class MicroDBTransaction : IDisposable
    {
        /* 目前先只封装SQL */

        //---------Members----------

#region 属性记录

#if lang_zh
        /// <summary>
        /// 隔离等级(只读)
        /// </summary>
#else
        /// <summary>
        /// Level of Isolation (read only)
        /// </summary>
#endif
        public IsolationLevel IsolationLevel { get; private set; }


#if lang_zh
        /// <summary>
        /// 连接别名(只读)
        /// </summary>
#else
        /// <summary>
        /// the Alias Name of Connection (read only)
        /// </summary>
#endif
        public string ConnectionAliasName { get; private set; }

        /// <summary>
        /// 标记事务是否结束
        /// </summary>
        private bool IsTranEnded = false;

        /// <summary>
        /// 标记执行是否顺利
        /// </summary>
        private bool IsSuccess = false;
        
#endregion

#region 数据库对象

        /// <summary>
        /// 数据库连接对象
        /// </summary>
        internal SqlConnection connection;

        /// <summary>
        /// 事务对象
        /// </summary>
        internal SqlTransaction tran;

#endregion


        //---------Structs----------

#region 事务用途

        /// <summary>
        /// 事务用途
        /// </summary>
        [Obsolete("Thinking only and it's not be used now.", true)]
        internal enum TransactionPurpose: byte
        {
            /// <summary>
            /// 只读
            /// </summary>
            ReadOnly,

            /// <summary>
            /// 读和写
            /// </summary>
            ReadWrite,
        }
        
#endregion


        //---------Control----------

#region 构造函数

        //隐藏默认构造函数
        private MicroDBTransaction()
        {}

#if lang_zh
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="connectionAliasName">连接别名</param>
        /// <param name="isolationLevel">隔离等级</param>
#else
        /// <summary>
        /// Default ctor
        /// </summary>
        /// <param name="isolationLevel">the Alias Name of Connection</param>
        /// <param name="connectionAliasName">the Alias Name of Connection</param>
#endif
        internal MicroDBTransaction(IsolationLevel isolationLevel, string connectionAliasName = MicroDBHelper.ALIAS_NAME_DEFAULT)
        {
            this.IsolationLevel         = isolationLevel;
            this.ConnectionAliasName    = connectionAliasName;
        }

#endregion

#region IDisposable

#if lang_zh
        /// <summary>
        /// 是否已经完成释放
        /// </summary>
#else
        /// <summary>
        /// mark as is it disposed now
        /// </summary>
#endif
        public bool IsDisposed { get; private set; }
      
#if lang_zh
        /// <summary>
        /// 释放资源
        /// </summary>
#else
        /// <summary>
        /// Dispose resources
        /// </summary>
#endif
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                //结束事务
                EndTransaction();

                //释放托管资源
                DisposeManagedResource();
            }
            
            //释放非托管资源
            IsDisposed = true;
        }

        /// <summary>
        /// 释放托管资源
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void DisposeManagedResource()
        {
            if (tran != null)
            {
                tran.Dispose();
                tran = null;
            }
            if (connection != null)
            {
                connection.Dispose();
                connection = null;
            }
        }


        /// <summary>
        /// 析构
        /// </summary>
        ~MicroDBTransaction()
        {
            Dispose(false);
        }
       
#endregion

#region ToString()

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("Transaction Object using the ConnectionAliasName:[{0}] and the IsolationLevel:[{1}] ",
                                 ConnectionAliasName,
                                 IsolationLevel.ToString()
                                );
        }

#endregion


#region 使其就绪
        /// <summary>
        /// 使其就绪
        /// </summary>
        /// <returns>就绪与否</returns>        
        internal bool MakeReady()
        {
            try
            {
                ConnectionAndCreateTran();

                return true;
            }
            catch (Exception)
            {
                this.Dispose();
                return false;
            }
        }

        /// <summary>
        /// 连接数据库并创建事务
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void ConnectionAndCreateTran()
        {
            this.connection = new SqlConnection( MicroDBHelper.GetConnection(ConnectionAliasName) );
            connection.Open();
            this.tran       = connection.BeginTransaction(IsolationLevel);
        }

#endregion

#region 结束事务

        /// <summary>
        /// 结束事务
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void EndTransaction()
        {
            if (tran != null && IsTranEnded == false)
            {
                if (this.IsSuccess)
                    tran.Commit();
                else
                    tran.Rollback();

                //标记结束
                IsTranEnded = true;
            }
        }

#endregion


#region 标记执行成功

#if lang_zh
        /// <summary>
        /// 标记执行成功(事务结束之前，会执行Commit；否则，会执行Rollback)
        /// </summary>
#else
        /// <summary>
        /// Mark this Transaction is Success ( before this MicroDBTransaction END,it will commit; otherwise, it wll rollback  )
        /// </summary>
#endif
        public void MarkSuccess()
        {
            this.IsSuccess = true;
        }

#endregion
    }

}
