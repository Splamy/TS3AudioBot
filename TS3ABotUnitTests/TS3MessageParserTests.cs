namespace TS3ABotUnitTests
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Text;
	using System.Threading.Tasks;
	using NUnit.Framework;
	using TS3Client.Full;
	using TS3Client.Messages;

	[TestFixture]
	public class TS3MessageParserTests
	{
		[Test]
		public void Deserializer1Test()
		{
			var notif = Deserializer.GenerateNotification(Encoding.UTF8.GetBytes("cid=6"), NotificationType.ChannelChanged);
			Assert.True(notif.Ok);
			var notifv = notif.Value;
			Assert.AreEqual(notifv.Length, 1);
			var notifs = notifv[0];
			AssertEx.PropertyValuesAreEquals(notifs, new ChannelChanged() { ChannelId = 6 });
		}

		[Test]
		public void Deserializer2Test()
		{
			var notif = Deserializer.GenerateNotification(Encoding.UTF8.GetBytes("clid=42 cluid=asdfe\\/rvt=="), NotificationType.ClientChatComposing);
			Assert.True(notif.Ok);
			var notifv = notif.Value;
			Assert.AreEqual(notifv.Length, 1);
			var notifs = notifv[0];
			AssertEx.PropertyValuesAreEquals(notifs, new ClientChatComposing() { ClientId = 42, ClientUid = "asdfe/rvt==" });
		}

		[Test]
		public void Deserializer3Test()
		{
			var notif = Deserializer.GenerateNotification(Encoding.UTF8.GetBytes("cid=5 | cid=4"), NotificationType.ChannelChanged);
			Assert.True(notif.Ok);
			var notifv = notif.Value;
			Assert.AreEqual(notifv.Length, 2);
			AssertEx.PropertyValuesAreEquals(notifv[0], new ChannelChanged() { ChannelId = 5 });
			AssertEx.PropertyValuesAreEquals(notifv[1], new ChannelChanged() { ChannelId = 4 });
		}

		[Test]
		public void Deserializer4Test()
		{
			var notif = Deserializer.GenerateNotification(Encoding.UTF8.GetBytes("cluid=asdfe\\/rvt== clid=42 | clid=1337"), NotificationType.ClientChatComposing);
			Assert.True(notif.Ok);
			var notifv = notif.Value;
			Assert.AreEqual(notifv.Length, 2);
			AssertEx.PropertyValuesAreEquals(notifv[0], new ClientChatComposing() { ClientId = 42, ClientUid = "asdfe/rvt==" });
			AssertEx.PropertyValuesAreEquals(notifv[1], new ClientChatComposing() { ClientId = 1337, ClientUid = "asdfe/rvt==" });
		}
	}

	public static class AssertEx
	{
		public static void PropertyValuesAreEquals(object actual, object expected)
		{
			PropertyInfo[] properties = expected.GetType().GetProperties();
			foreach (PropertyInfo property in properties)
			{
				object expectedValue = property.GetValue(expected, null);
				object actualValue = property.GetValue(actual, null);

				if (actualValue is IList)
					AssertListsAreEquals(property, (IList)actualValue, (IList)expectedValue);
				else if (!Equals(expectedValue, actualValue))
					Assert.Fail("Property {0}.{1} does not match. Expected: {2} but was: {3}", property.DeclaringType.Name, property.Name, expectedValue, actualValue);
			}
		}

		private static void AssertListsAreEquals(PropertyInfo property, IList actualList, IList expectedList)
		{
			if (actualList.Count != expectedList.Count)
				Assert.Fail("Property {0}.{1} does not match. Expected IList containing {2} elements but was IList containing {3} elements", property.PropertyType.Name, property.Name, expectedList.Count, actualList.Count);

			for (int i = 0; i < actualList.Count; i++)
				if (!Equals(actualList[i], expectedList[i]))
					Assert.Fail("Property {0}.{1} does not match. Expected IList with element {1} equals to {2} but was IList with element {1} equals to {3}", property.PropertyType.Name, property.Name, expectedList[i], actualList[i]);
		}
	}
}
