#region Copyright (C) 2007-2013 Team MediaPortal

/*
    Copyright (C) 2007-2013 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using MediaPortal.Plugins.SlimTv.Interfaces.Items;

namespace MediaPortal.Plugins.SlimTv.Interfaces
{
  [Flags]
  public enum RecordingStatus
  {
    None,
    Scheduled,
    SeriesScheduled,
    RuleScheduled,
    Recording
  }

  public enum ScheduleRecordingType
  {
    Once,
    Daily,
    Weekly,
    EveryTimeOnThisChannel,
    EveryTimeOnEveryChannel,
    Weekends,
    WorkingDays,
    WeeklyEveryTimeOnThisChannel
  }

  public interface IScheduleControl
  {
    bool CreateSchedule(IProgram program, out ISchedule schedule);
    bool RemoveSchedule(IProgram program); // ISchedule schedule ?
    bool GetRecordingStatus(IProgram program, out RecordingStatus recordingStatus);
    bool GetRecordingFileOrStream(IProgram program, out string fileOrStream);

    /// <summary>
    /// Tries to get a list of programs for the given <paramref name="schedule"/>.
    /// </summary>
    /// <param name="schedule">Schedule</param>
    /// <param name="programs">Returns programs</param>
    /// <returns><c>true</c> if at least one program could be found</returns>
    bool GetProgramsForSchedule(ISchedule schedule, out IList<IProgram> programs);

    //bool GetSchedules(IChannel channel, out IList<ISchedule> schedules);
    //bool GetSchedules(out IList<ISchedule> schedules);

    //bool AddRule(IScheduleRule rule);
    //bool RemoveRule(IScheduleRule rule);
    //bool GetRules(out IList<IScheduleRule> rules);
    //TODO
  }
}
