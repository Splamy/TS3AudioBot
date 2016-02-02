namespace TS3Query
{
	using System;

	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	sealed class NotificationNameAttribute : Attribute
	{
		public NotificationType NotificationType { get; }
		public string Name { get; }

		public NotificationNameAttribute(NotificationType type)
		{
			NotificationType = type;
			Name = type.GetQueryString();
		}
	}
}
