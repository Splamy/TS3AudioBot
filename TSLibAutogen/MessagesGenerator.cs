using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TSLibAutogen
{
	class MessagesGenerator
	{
		static readonly Dictionary<string, string> BackingTypes = new()
		{
			{ "Uid", "str" },
			{ "ClientDbId", "u64" },
			{ "ClientId", "u16" },
			{ "ChannelId", "u64" },
			{ "ServerGroupId", "u64" },
			{ "ChannelGroupId", "u64" },
			{ "Codec", "u8" },
			{ "Ts3ErrorCode", "u32" },
			{ "LicenseType", "u16" },
			{ "PermissionId", "u32" },
		};

		public static IEnumerable<GenFile> Build(Model model)
		{
			var src = new CodeBuilder();

			src.AppendLine("using System;");
			src.AppendLine("using System.Collections.Generic;");
			src.AppendLine("using System.Buffers.Text;");
			src.AppendLine("using TSLib.Commands;");
			src.AppendLine("using TSLib.Helper;");
			src.AppendLine(Util.ConversionSet);
			src.AppendLine();

			src.AppendLine("#pragma warning disable CS8618");
			src.AppendLine("namespace TSLib.Messages {");

			// Message classes
			foreach (var msg in model.Messages.Msgs)
			{
				var isNotify = msg.Notify is not null;
				var isResponse = msg.response;
				src.AppendFormatLine("public sealed partial class {0}{1} {{", msg.Name,
					(isNotify, isResponse) switch
					{
						(true, true) => " : INotification, IResponse",
						(true, _) => " : INotification",
						(_, true) => " : IResponse",
						_ => "",
					});

				if (isNotify)
					src.AppendFormatLine("public NotificationType NotifyType => NotificationType.{0};", msg.Name);
				if (isResponse)
					src.AppendLine("public string? ReturnCode { get; set; }");

				// Fields
				foreach (var (field, optional) in msg.Fields)
					src.AppendFormatLine("public {0} {1} {{ get; set; }}", field.TypeFin(optional), field.pretty);
				src.AppendLine();

				// SetField
				src.AppendLine("public void SetField(string name, ReadOnlySpan<byte> value, Deserializer ser) {");
				if (msg.Fields.Length > 0 || isResponse)
				{
					src.AppendLine("switch(name) {");
					foreach (var (field, _) in msg.Fields)
						src.AppendFormatLine("case \"{0}\": {1} break;", field.ts, GenerateDeserializer(field));
					if (isResponse)
						src.AppendFormatLine("case \"return_code\": {0} break;", GenerateDeserializer(model.Messages.ReturnCode));
					src.PopCloseBrace(); // switch
				}
				src.PopCloseBrace(); // fn SetField
				src.AppendLine();

				// Expand
				src.AppendLine("public void Expand(IMessage[] to, IEnumerable<string> flds) {");
				if (msg.Fields.Length > 0)
				{
					src.AppendFormatLine("var toc = ({0}[])to;", msg.Name);
					src.AppendLine("foreach (var fld in flds) {");
					src.AppendLine("switch(fld) {");
					foreach (var (field, _) in msg.Fields)
						src.AppendFormatLine("case \"{0}\": foreach(var toi in toc) {{ toi.{1} = {1}; }} break;", field.ts, field.pretty);
					src.PopCloseBrace(); // switch
					src.PopCloseBrace(); // foreach
				}
				src.PopCloseBrace(); // fn Expand

				src.PopCloseBrace(); // class
				src.AppendLine();
			}

			// Notification type enum
			src.AppendLine("public enum NotificationType {");
			src.AppendLine("Unknown,");
			foreach (var ntfy in model.Messages.Notifies)
			{
				src.AppendFormatLine("///<summary>{0}{1}ntfy:{1}</summary>", ntfy.s2c ? "[S2C] " : "", ntfy.c2s ? "[C2S] " : "", ntfy.Notify);
				src.AppendFormatLine("{0},", ntfy.Name);
			}
			src.PopCloseBrace(); // enum NotificationType

			// MessageHelper
			src.AppendLine("public static class MessageHelper {");
			// Helper: Ntfy-string to type
			void GenerateToEnum(IEnumerable<Message> ntfys, string fnName)
			{
				src.AppendFormatLine("public static NotificationType {0}(string name) {{", fnName);
				src.AppendLine("switch(name) {");
				foreach (var ntfy in ntfys)
					src.AppendFormatLine("case \"{0}\": return NotificationType.{1};", ntfy.Notify, ntfy.Name);
				src.AppendLine("default: return NotificationType.Unknown;");
				src.PopCloseBrace(); // switch
				src.PopCloseBrace(); // fn GetToClientNotificationType
			}
			GenerateToEnum(model.Messages.Notifies.Where(x => x.s2c), "GetToClientNotificationType");
			GenerateToEnum(model.Messages.Notifies.Where(x => x.c2s), "GetToServerNotificationType");

			// Helper: Ntfy-type to class instance
			src.AppendLine("public static INotification GenerateNotificationType(NotificationType name) {");
			src.AppendLine("switch(name) {");
			foreach (var ntfy in model.Messages.Notifies)
				src.AppendFormatLine("case NotificationType.{0}: return new {0}();", ntfy.Name);
			src.AppendLine("case NotificationType.Unknown:");
			src.AppendLine("default: throw Tools.UnhandledDefault(name);");
			src.PopCloseBrace(); // switch
			src.PopCloseBrace(); // fn GenerateNotificationType

			// Helper instantiate ntfy-array
			src.AppendLine("public static INotification[] InstantiateNotificationArray(NotificationType name, int len) {");
			src.AppendLine("switch(name) {");
			foreach (var ntfy in model.Messages.Notifies)
				src.AppendFormatLine("case NotificationType.{0}: {{ var arr = new {0}[len]; for (int i = 0; i < len; i++) arr[i] = new {0}(); return arr; }}", ntfy.Name);
			src.AppendLine("default: throw Tools.UnhandledDefault(name);");
			src.PopCloseBrace(); // switch
			src.PopCloseBrace(); // fn InstantiateNotificationArray

			src.PopCloseBrace(); // class MessageHelper

			src.PopCloseBrace(); // namespace

			return new[] { new GenFile("Messages", SourceText.From(src.ToString(), Encoding.UTF8)) };
		}

		private static string GenerateDeserializer(Field fld)
		{
			if (fld.isArray)
				return $"{{ if(value.Length == 0) {fld.pretty} = Array.Empty<{fld.type}>(); else {{"
					 + $" var ss = new SpanSplitter<byte>(); ss.First(value, (byte)',');"
					 + $" int cnt = 0; for (int i = 0; i < value.Length; i++) if (value[i] == ',') cnt++;"
					 + $" {fld.pretty} = new {fld.type}[cnt + 1];"
					 + $" for(int i = 0; i < cnt + 1; i++) {{ {GenerateSingleDeserializer(fld, "ss.Trim(value)", fld.pretty + "[i]")} if (i < cnt) value = ss.Next(value); }} }} }}";
			else
				return GenerateSingleDeserializer(fld, "value", fld.pretty);
		}

		private static string GenerateSingleDeserializer(Field fld, string input, string output)
		{
			switch (fld.type)
			{
			case "bool":
				return $"{output} = {input}.Length > 0 && {input}[0] != '0';";
			case "i8":
			case "u8":
			case "i16":
			case "u16":
			case "i32":
			case "u32":
			case "i64":
			case "u64":
			case "f32":
			case "f64":
			case "ClientDbId":
			case "ClientId":
			case "ChannelId":
			case "ServerGroupId":
			case "ChannelGroupId":
				if (!BackingTypes.TryGetValue(fld.type, out var backType))
					backType = fld.type;
				return $"{{ if(Utf8Parser.TryParse({input}, out {backType} oval, out _)) {output} = ({fld.type})oval; }}";
			case "DurationSeconds":
				return $"{{ if(Utf8Parser.TryParse({input}, out f64 oval, out _)) {output} = TimeSpan.FromSeconds(oval); }}";
			case "DurationMilliseconds":
			case "DurationMillisecondsFloat":
				return $"{{ if(Utf8Parser.TryParse({input}, out f64 oval, out _)) {output} = TimeSpan.FromMilliseconds(oval); }}";
			case "DateTime":
				return $"{{ if(Utf8Parser.TryParse({input}, out u32 oval, out _)) {output} = Tools.FromUnix(oval); }}";
			case "str":
			case "Uid":
			case "IpAddr":
				return $"{output} = ({fld.type})TsString.Unescape({input});";
			case "HostMessageMode":
			case "CodecEncryptionMode":
			case "HostBannerMode":
			case "Reason":
			case "ClientType":
			case "TextMessageTargetMode":
			case "GroupType":
			case "GroupNamingMode":
			case "Codec":
			case "Ts3ErrorCode":
			case "LicenseType":
			case "TokenType":
			case "LogLevel":
			case "PluginTargetMode":
			case "PermissionType":
			case "ChannelPermissionHint":
			case "ClientPermissionHint":
				if (!BackingTypes.TryGetValue(fld.type, out backType))
					backType = "i32";
				return $"{{ if(Utf8Parser.TryParse({input}, out {backType} oval, out _) && TsEnums.IsDefined{fld.type}(oval)) {output} = ({fld.type})oval; }}";
			case "IconId":
				return $"{{ if(!{input}.IsEmpty && {input}[0] == (u8)'-') {{ if(Utf8Parser.TryParse({input}, out i32 oval, out _)) {output} = oval; }} else {{ if(Utf8Parser.TryParse({input}, out u64 oval, out _)) {output} = unchecked((i32)oval); }} }}";
			case "PermissionId":
				return $"{{ if(Utf8Parser.TryParse({input}, out u16 oval, out _)) {output} = ser.PermissionTransform.GetName(oval); }}";
			default:
				//Warn($"Missing deserializer for {fld.type}");
				return "";
			}
		}
	}
}
