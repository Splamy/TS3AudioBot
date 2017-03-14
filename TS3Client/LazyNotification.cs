namespace TS3Client
{
	using Messages;
	using System.Collections.Generic;

	struct LazyNotification
	{
		public readonly IEnumerable<INotification> Notifications;
		public readonly NotificationType NotifyType;

		public LazyNotification(IEnumerable<INotification> notifications, NotificationType notifyType)
		{
			Notifications = notifications;
			NotifyType = notifyType;
		}
	}
}
