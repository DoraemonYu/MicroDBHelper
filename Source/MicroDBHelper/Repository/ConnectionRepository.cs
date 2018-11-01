using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
#if ASYNC_SUPPORT
using System.Linq;
using System.Threading.Tasks;
#endif

namespace MicroDBHelpers
{

#if lang_zh
    /// <summary>
    /// 连接资源的仓库
    /// </summary>
#else
    /// <summary>
    /// Repository of Connection Resources
    /// </summary>
#endif
    internal sealed class ConnectionRepository
    {

        //---------Members----------

        #region 词典

        private static Dictionary<string, ConnectionRepositoryItem> repository = new Dictionary<string, ConnectionRepositoryItem>();

        #endregion


        //---------Control----------

        #region 添加资源项

#if lang_zh
        /// <summary>
        /// 添加资源项
        /// </summary>
        /// <param name="ConnectionAliasName">连接别名</param>
        /// <param name="item">资源项</param>
        /// <returns>成功与否</returns>
#else
        /// <summary>
        /// Set the target Item
        /// </summary>
        /// <param name="ConnectionAliasName">the Alias Name of Connection</param>
        /// <param name="item">the item</param>
        /// <returns>successfully or not</returns>
#endif
        internal static bool SetRepositoryItem(string ConnectionAliasName, ConnectionRepositoryItem item)
        {
            //检查
            if (item == null)
                return false;

            //设置值
            repository[ConnectionAliasName] = item;
            return true;
        }

        #endregion

        #region 删除资源项

#if lang_zh
        /// <summary>
        /// 删除资源项
        /// </summary>
        /// <param name="ConnectionAliasName">连接别名</param>
        /// <returns>成功与否</returns>
#else
        /// <summary>
        /// Unset the target Item
        /// </summary>
        /// <param name="ConnectionAliasName">the Alias Name of Connection</param>
        /// <returns>successfully or not</returns>
#endif
        internal static bool UnsetRepositoryItem(string ConnectionAliasName)
        {
            //如果存在
            lock (((ICollection)repository).SyncRoot)
                if (repository.ContainsKey(ConnectionAliasName))
                    return repository.Remove(ConnectionAliasName);

            //如果不存在
            return false;
        }

        #endregion

        #region 获取资源项

#if lang_zh
        /// <summary>
        /// 获取资源项
        /// </summary>
        /// <param name="ConnectionAliasName">连接别名</param>
        /// <returns>资源项</returns>
#else
        /// <summary>
        /// Get the target Item
        /// </summary>
        /// <param name="ConnectionAliasName">the Alias Name of Connection</param>
        /// <returns>Item</returns>
#endif
        internal static ConnectionRepositoryItem GetRepositoryItem(string ConnectionAliasName)
        {
            ConnectionRepositoryItem result;
            if (repository.TryGetValue(ConnectionAliasName, out result))
                return result;
            else
                return null;
        }

        #endregion


    }
}
