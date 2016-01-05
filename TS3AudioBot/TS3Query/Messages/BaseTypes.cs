namespace TS3Query.Messages
{
	using System;
	using System.Linq;

	public interface IQueryMessage { }

	public abstract class Response : IQueryMessage { }

	public abstract class Notification : EventArgs, IQueryMessage
	{
		internal NotificationType GetNotifyType()
			=> GetType().GetCustomAttributes(false).OfType<NotificationNameAttribute>()
			.FirstOrDefault().NotificationType;
	}
}
