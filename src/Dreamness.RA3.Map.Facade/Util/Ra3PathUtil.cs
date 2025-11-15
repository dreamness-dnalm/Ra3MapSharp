using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Dreamness.Ra3.Map.Facade.Util
{
    /// <summary>
    /// Ra3路径工具类
    /// </summary>
    public class Ra3PathUtil
    {
        /// <summary>
        /// RA3地图文件夹路径
        /// </summary>
        public static string RA3MapFolder = Path.Combine(Environment.GetEnvironmentVariable("APPDATA"), "Red Alert 3", "Maps");
        
    }
}