// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Diagnostics.CodeAnalysis;
using TSLib;

namespace TS3AudioBot.Audio
{
	public sealed class MetaData
	{
		/// <summary>Defaults to: invoker.Uid - Can be set if the owner of a song differs from the invoker.</summary>
		public Uid? ResourceOwnerUid { get; set; }
		/// <summary>Starts the song at the specified time if set.</summary>
		public TimeSpan? StartOffset { get; set; }

		public MetaData(TimeSpan? startOffset = null)
		{
			StartOffset = startOffset;
		}

		public MetaData Merge(MetaData other) => Merge(this, other);

		[return: NotNullIfNotNull("self")]
		[return: NotNullIfNotNull("other")]
		public static MetaData? Merge(MetaData? self, MetaData? other)
		{
			if (other is null)
				return self;
			if (self is null)
				return other;
			self.ResourceOwnerUid ??= other.ResourceOwnerUid;
			self.StartOffset ??= other.StartOffset;
			return self;
		}

		public static MetaData MergeDefault(MetaData? self, MetaData? other)
			=> Merge(self, other) ?? new MetaData();
	}

	public interface IMetaContainer
	{
		public MetaData? Meta { get; set; }
	}

	public static class MetaContainerExtensions
	{
		public static T MergeMeta<T>(this T container, MetaData? other) where T : IMetaContainer
		{
			container.Meta = MetaData.Merge(container.Meta, other);
			return container;
		}
	}
}
