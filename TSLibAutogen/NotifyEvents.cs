using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TSLibAutogen;

class NotifyEvents
{
	public static readonly HashSet<string> sharedNotifications = new()
	{
		"ChannelCreated",
		"ChannelDeleted",
		"ChannelChanged",
		"ChannelEdited",
		"ChannelMoved",
		"ChannelPasswordChanged",
		"ClientEnterView",
		"ClientLeftView",
		"ClientMoved",
		"ServerEdited",
		"TextMessage",
		"TokenUsed",
	};

	public static IEnumerable<GenFile> Build(Model model)
	{
		yield return BuildPart(model, "TSLib.Full", "TsFullClient", false, true, model.Messages.Notifies.Where(m => m.s2c));
		yield return BuildPart(model, "TSLib.Query", "TsQueryClient", false, false, model.Messages.Notifies.Where(m => m.s2c && sharedNotifications.Contains(m.Name)));
		yield return BuildPart(model, "TSLib", "TsBaseFunctions", true, false, model.Messages.Notifies.Where(m => m.s2c && sharedNotifications.Contains(m.Name)));
	}

	private static GenFile BuildPart(Model model, string ns, string filename, bool root, bool full, IEnumerable<Message> enu)
	{
		var msgs = enu.ToArray();
		var src = new CodeBuilder();

		src.AppendLine("using System;");
		src.AppendLine("using TSLib.Commands;");
		src.AppendLine("using TSLib.Helper;");
		src.AppendLine("using TSLib.Messages;");
		src.AppendFormatLine("namespace {0} {{", ns);
		src.AppendFormatLine("partial class {0} {{", filename);

		// event fields
		var modifier = root ? " abstract" : " override";
		foreach (var ntfy in msgs)
		{
			src.AppendFormatLine("public{0} event NotifyEventHandler<{1}>? On{1};",
				sharedNotifications.Contains(ntfy.Name) ? modifier : "", ntfy.Name);

			src.AppendFormatLine("public{0} event EventHandler<{1}>? OnEach{1};",
				sharedNotifications.Contains(ntfy.Name) ? modifier : "", ntfy.Name);
		}

		if (!root)
		{
			src.AppendLine("private void InvokeEvent(LazyNotification lazyNotification) {");
			src.AppendLine("var ntf = lazyNotification.Notifications;");
			src.AppendLine("switch (lazyNotification.NotifyType) {");

			foreach (var ntfy in msgs)
			{
				src.AppendFormatLine("case NotificationType.{0}: {{", ntfy.Name);
				src.AppendFormatLine("var ntfc = ({0}[])ntf;", ntfy.Name);
				src.AppendFormatLine("Process{0}(ntfc);", ntfy.Name);
				src.AppendFormatLine("On{0}?.Invoke(this, ntfc);", ntfy.Name);
				src.AppendFormatLine("var ev = OnEach{0};", ntfy.Name);
				if (full) src.AppendLine("var book = Book;");
				src.AppendLine("foreach(var that in ntfc) {");
				if (full && model.M2B.Any(r => r.From == ntfy)) src.AppendFormatLine("book?.Apply{0}(that);", ntfy.Name);
				src.AppendFormatLine("ProcessEach{0}(that);", ntfy.Name);
				src.AppendLine("ev?.Invoke(this, that);");
				src.PopCloseBrace(); // foreach
				src.AppendLine("break;");
				src.PopCloseBrace(); // case
			}

			if (!full) src.AppendLine("case NotificationType.CommandError: break;");
			src.AppendLine("case NotificationType.Unknown:");
			src.AppendLine("default:");
			src.AppendLine("throw Tools.UnhandledDefault(lazyNotification.NotifyType);");

			src.PopCloseBrace(); // switch
			src.PopCloseBrace(); // fn InvokeEvent

			src.AppendLine();
			foreach (var ntfy in msgs)
			{
				src.AppendFormatLine("partial void Process{0}({0}[] notifies);", ntfy.Name);
				src.AppendFormatLine("partial void ProcessEach{0}({0} notifies);", ntfy.Name);
			}
		}

		src.PopCloseBrace(); // class
		src.PopCloseBrace(); // namespace

		return new GenFile(filename + "Gen", SourceText.From(src.ToString(), Encoding.UTF8));
	}
}
