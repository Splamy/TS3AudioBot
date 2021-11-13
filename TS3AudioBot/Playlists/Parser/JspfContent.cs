// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using PlaylistsNET.Content;
using PlaylistsNET.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using TS3AudioBot.Helper;

namespace TS3AudioBot.Playlists.Parser;

public class JspfContent : IPlaylistParser<XspfPlaylist>, IPlaylistWriter<XspfPlaylist>
{
	public XspfPlaylist GetFromStream(Stream stream)
	{
		using var sr = new StreamReader(stream);
		var data = sr.ReadToEnd();
		return JsonSerializer.Deserialize<XspfPlaylist>(data) ?? throw new NullReferenceException("Data empty");
	}

	public XspfPlaylist GetFromString(string playlistString)
	{
		throw new NotImplementedException();
	}

	public string ToText(XspfPlaylist playlist)
	{
		return JsonSerializer.Serialize(playlist);
	}
}

public class XspfPlaylist : IBasePlaylist
{
	[JsonPropertyName("title")]
	public string? Title { get; set; }
	[JsonPropertyName("creator")]
	public string? Creator { get; set; }

	[JsonPropertyName("track")]
	public List<XspfPlaylistEntry>? PlaylistEntries { get; set; }

	public string? Path { get; set; }
	public string? FileName { get; set; }

	public XspfPlaylist()
	{
	}

	public List<string> GetTracksPaths() => PlaylistEntries?.SelectNotNull(x => x.Location?.FirstOrDefault()).ToList() ?? new List<string>();
}

public class XspfPlaylistEntry
{
	public XspfPlaylistEntry() { }

	[JsonPropertyName("title")]
	public string? Title { get; set; }
	[JsonPropertyName("duration")]
	public long? Duration { get; set; } // MS : TODO timespan converter

	[JsonPropertyName("meta")]
	[JsonConverter(typeof(JspfMetaConverter))]
	public List<XspfMeta>? Meta { get; set; }

	[JsonPropertyName("location")]
	public List<string>? Location { get; set; }
}

public class XspfMeta
{
	public string Key { get; set; }
	public string Value { get; set; }

	public XspfMeta(string key, string value)
	{
		Key = key;
		Value = value;
	}
}

internal class JspfMetaConverter : JsonConverter<XspfMeta>
{
	public override XspfMeta? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var key = reader.GetString();
		var value = reader.GetString();
		if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
			throw new FormatException();
		return new XspfMeta(key, value);
	}

	public override void Write(Utf8JsonWriter writer, XspfMeta value, JsonSerializerOptions options)
	{
		if (value is null)
		{
			writer.WriteNullValue();
		}
		else
		{
			if (value is null) throw new ArgumentNullException(nameof(value));
			writer.WriteStartObject();
			writer.WriteString(value.Key, value.Value);
			writer.WriteEndObject();
		}
	}
}
