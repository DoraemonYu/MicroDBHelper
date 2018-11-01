using System;
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
    /// 连接资源的仓库子项
    /// </summary>
#else
    /// <summary>
    /// Repository Item of Connection Resources
    /// </summary>
#endif
    internal class ConnectionRepositoryItem
    {

        public string ConnectionString { get; set; }


        public Version ServerProductVersion { get; set; }

    }

}
