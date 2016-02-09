namespace TS3Query.Messages
{
	using System;

	public interface IQueryMessage { }

	public interface INotification : IQueryMessage
	{
		NotificationType NotifyType { get; }
	}

	public interface IResponse : IQueryMessage { }

	[AttributeUsage(AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
	sealed class QuerySubInterfaceAttribute : Attribute { }

	[AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
	sealed class QueryNotificationAttribute : Attribute
	{
		public NotificationType NotificationType { get; }
		public string Name { get; }

		public QueryNotificationAttribute(NotificationType type)
		{
			NotificationType = type;
			Name = type.GetQueryString();
		}
	}

	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
	sealed class QuerySerializedAttribute : Attribute
	{
		public string Name { get; private set; }
		public QuerySerializedAttribute(string name)
		{
			Name = string.IsNullOrWhiteSpace(name) ? null : name;
		}
	}
}
