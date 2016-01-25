using System;

namespace TS3Query
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    sealed class QuerySerializedAttribute : Attribute
    {
        public string Name { get; private set; }
        public QuerySerializedAttribute(string name)
        {
            Name = string.IsNullOrWhiteSpace(name) ? null : name;
        }
    }
}
