namespace TS3AudioBot.Tests;

public static class TestExtensions
{
	extension(Assert assert)
	{
		public static void DoesNotThrow(Action action)
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				Assert.Fail($"Expected no exception, but got: {ex}");
			}
		}
	}
}
