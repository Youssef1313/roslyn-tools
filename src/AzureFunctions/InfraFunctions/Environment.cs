using System;
using System.Collections.Generic;
using System.Text;

namespace InfraFunctions
{
    internal static class Environment
    {
        public static string GetVariable(string name)
        => System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
    }
}
