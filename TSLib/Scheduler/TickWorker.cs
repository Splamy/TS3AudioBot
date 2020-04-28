// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

namespace TSLib.Scheduler
{
	public class TickWorker
	{
		readonly DedicatedTaskScheduler parent;
		private TimeSpan interval;

		internal Action Method { get; }
		internal TimeSpan Timestamp { get; set; } = TimeSpan.Zero;

		public TimeSpan Interval
		{
			get => interval;
			set
			{
				if (value <= TimeSpan.Zero)
					throw new ArgumentOutOfRangeException(nameof(Interval), "Interval must not be 0 or negative");
				interval = value;
			}
		}

		public bool IsActive { get; private set; } = false;

		public void Enable()
		{
			if (!IsActive)
			{
				parent.EnableTimer(this);
				IsActive = true;
			}
		}

		public void Disable()
		{
			if (IsActive)
			{
				parent.DisableTimer(this);
				IsActive = false;
			}
		}

		internal TickWorker(DedicatedTaskScheduler parent, Action method, TimeSpan interval)
		{
			this.parent = parent;
			Method = method;
			Interval = interval;
		}
	}
}
