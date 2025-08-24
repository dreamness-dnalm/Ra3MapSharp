using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Dreamness.Ra3.Map.Facade.Util
{
    public class PathUtil
    {
        public static string RA3MapFolder = Path.Combine(Environment.GetEnvironmentVariable("APPDATA"), "Red Alert 3", "Maps");
        // public static string WorkingDir = Directory.GetCurrentDirectory();
    }
}