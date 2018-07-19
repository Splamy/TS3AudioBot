namespace TS3ABotUnitTests
{
	using NUnit.Framework;
	using System.Collections;
	using System.Reflection;
	using System.Text;
	using TS3Client;
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

		[Test]
		public void Deserializer5Test()
		{
			var notif = Deserializer.GenerateResponse<ClientData>(Encoding.UTF8.GetBytes(
				"clid=1 cid=1 client_database_id=2 client_nickname=TestBob1 client_type=0 client_unique_identifier=u\\/dFMOFFipxS9fJ8HKv0KH6WVzA="
				+ "|clid=2 cid=4 client_database_id=2 client_nickname=TestBob client_type=0 client_unique_identifier=u\\/dFMOFFipxS9fJ8HKv0KH6WVzA="
				+ "|clid=3 cid=4 client_database_id=6 client_nickname=Splamy client_type=0 client_unique_identifier=uA0U7t4PBxdJ5TLnarsOHQh4\\/tY="
				+ "|clid=4 cid=4 client_database_id=7 client_nickname=AudioBud client_type=0 client_unique_identifier=b+P0CqXms5I0C+A66HZ4Sbu\\/PNw="
			));
			Assert.True(notif.Ok);
			var notifv = notif.Value;
			Assert.AreEqual(notifv.Length, 4);
			AssertEx.PropertyValuesAreEquals(notifv[0], new ClientData() { ClientId = 1, ChannelId = 1, DatabaseId = 2, Name = "TestBob1", ClientType = ClientType.Full, Uid = "u/dFMOFFipxS9fJ8HKv0KH6WVzA=" });
			AssertEx.PropertyValuesAreEquals(notifv[1], new ClientData() { ClientId = 2, ChannelId = 4, DatabaseId = 2, Name = "TestBob", ClientType = ClientType.Full, Uid = "u/dFMOFFipxS9fJ8HKv0KH6WVzA=" });
			AssertEx.PropertyValuesAreEquals(notifv[2], new ClientData() { ClientId = 3, ChannelId = 4, DatabaseId = 6, Name = "Splamy", ClientType = ClientType.Full, Uid = "uA0U7t4PBxdJ5TLnarsOHQh4/tY=" });
			AssertEx.PropertyValuesAreEquals(notifv[3], new ClientData() { ClientId = 4, ChannelId = 4, DatabaseId = 7, Name = "AudioBud", ClientType = ClientType.Full, Uid = "b+P0CqXms5I0C+A66HZ4Sbu/PNw=" });
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
