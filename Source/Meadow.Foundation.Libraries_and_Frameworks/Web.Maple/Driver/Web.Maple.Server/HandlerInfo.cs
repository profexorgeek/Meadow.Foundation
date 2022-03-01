using System;
using System.Reflection;

namespace Meadow.Foundation.Web.Maple.Server
{
    internal class HandlerInfo
    {
        public Type? HandlerType { get; set; }
        public MethodInfo? Method { get; set; }
        public ParameterInfo? Parameter { get; set; }
    }
}