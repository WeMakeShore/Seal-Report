﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace Seal.Model
{
    public class SealSchedule
    {
        public string GUID { get; set; }
        public string ReportPath { get; set; }
        public string ReportGUID { get; set; }
        public DateTime NextExecution { get; set; } = DateTime.MaxValue;
        public bool Enabled { get; set; } = false;
        public DateTime Start { get; set; } = DateTime.Now;
        public DateTime End { get; set; } = DateTime.MinValue;
        public TriggerType Type { get; set; } = TriggerType.Daily;
        public int DaysInterval { get; set; } = 1;
        public int WeeksInterval { get; set; } = 1;
        public int[] Weekdays { get; set; } = new int[] { 1 };
        public int[] Months { get; set; } = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        public int[] Days { get; set; } = new int[] { 1 };
        [XmlIgnore]
        public TimeSpan RepeatInterval { get; set; } = TimeSpan.Zero;
        public long RepeatIntervalTicks
        {
            get { return RepeatInterval.Ticks; }
            set { RepeatInterval = new TimeSpan(value); }
        }
        [XmlIgnore]
        public TimeSpan RepeatDuration { get; set; } = TimeSpan.Zero;
        public long RepeatDurationTicks
        {
            get { return RepeatDuration.Ticks; }
            set { RepeatDuration = new TimeSpan(value); }
        }

        /// <summary>
        /// True if the schedule is beign executed by the scheduler
        /// </summary>
        [XmlIgnore]
        public bool BeingExecuted = false;

        /// <summary>
        /// Current file path of the web schedule
        /// </summary>
        [XmlIgnore]
        public string FilePath = "";

        /// <summary>
        /// Last modification date of the seal schedule
        /// </summary>
        [XmlIgnore]
        public DateTime LastModification = DateTime.MinValue;

        /// <summary>
        /// Load a web schedule from a file
        /// </summary>
        static public SealSchedule LoadFromFile(string path)
        {
            SealSchedule result = null;
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(SealSchedule));
                using (XmlReader xr = XmlReader.Create(path))
                {
                    result = (SealSchedule)serializer.Deserialize(xr);
                }
                result.FilePath = path;
                result.LastModification = File.GetLastWriteTime(path);
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Unable to read the file '{0}'.\r\n{1}\r\n", path, ex.Message, ex.StackTrace));
            }
            return result;
        }

        /// <summary>
        /// Save the current web schedule to its file
        /// </summary>
        public void SaveToFile()
        {
            if (File.Exists(FilePath) && LastModification != File.GetLastWriteTime(FilePath))
            {
                throw new Exception("Unable to save the schedule file. The file has been modified by another user.");
            }
            XmlSerializer serializer = new XmlSerializer(typeof(SealSchedule));
            using (XmlWriter xw = XmlWriter.Create(FilePath))
            {
                serializer.Serialize(xw, this);
            }
            LastModification = File.GetLastWriteTime(FilePath);
        }

        [XmlIgnore]
        DateTime EndFinal
        {
            get
            {
                return End == DateTime.MinValue ? DateTime.MaxValue : End;
            }
        }

        public void CalculateNextExecution()
        {
            NextExecution = Start;

            switch (Type)
            {
                case TriggerType.Time:
                    if (RepeatInterval != TimeSpan.Zero)
                    {
                        //Handle repeat
                        var endRepeatDate = (RepeatDuration != TimeSpan.Zero ? NextExecution + RepeatDuration : DateTime.MaxValue);
                        if (endRepeatDate < DateTime.Now) break;
                        while (true)
                        {
                            if (NextExecution > DateTime.Now || NextExecution > EndFinal || NextExecution > endRepeatDate)
                            {
                                //Found within repeat
                                break;
                            }
                            NextExecution += RepeatInterval;
                        }
                    }
                    break;

                case TriggerType.Daily:
                    while (true)
                    {
                        if (NextExecution > DateTime.Now || NextExecution > EndFinal)
                        {
                            //Found
                            break;
                        }

                        if (RepeatInterval != TimeSpan.Zero)
                        {
                            //Handle repeat
                            var nextRepeatExecution = NextExecution;
                            var endRepeatDate = (RepeatDuration != TimeSpan.Zero ? nextRepeatExecution + RepeatDuration : DateTime.MaxValue);
                            while (endRepeatDate > DateTime.Now)
                            {
                                if (nextRepeatExecution > DateTime.Now || nextRepeatExecution > EndFinal || nextRepeatExecution > endRepeatDate)
                                {
                                    break;
                                }
                                nextRepeatExecution += RepeatInterval;
                            }

                            if (nextRepeatExecution > DateTime.Now || nextRepeatExecution > EndFinal)
                            {
                                //Found within repeat
                                NextExecution = nextRepeatExecution;
                                break;
                            }
                        }

                        NextExecution = NextExecution.AddDays(DaysInterval);
                    }
                    break;

                case TriggerType.Weekly:
                    while (true)
                    {
                        if (Weekdays.Length == 0) break;

                        //Check day of week
                        for (int i = 0; i < 6; i++)
                        {
                            var nextWeekExecution = NextExecution.AddDays(i);
                            if (!Weekdays.Contains((int)nextWeekExecution.DayOfWeek))
                            {
                                continue;
                            }

                            if (nextWeekExecution > DateTime.Now || nextWeekExecution > EndFinal)
                            {
                                //Found
                                NextExecution = nextWeekExecution;
                                break;
                            }

                            if (RepeatInterval != TimeSpan.Zero)
                            {
                                //Handle repeat
                                var nextRepeatExecution = nextWeekExecution;
                                var endRepeatDate = (RepeatDuration != TimeSpan.Zero ? nextRepeatExecution + RepeatDuration : DateTime.MaxValue);
                                while (endRepeatDate > DateTime.Now)
                                {
                                    if (nextRepeatExecution > DateTime.Now || nextRepeatExecution > EndFinal || nextRepeatExecution > endRepeatDate)
                                    {
                                        break;
                                    }
                                    nextRepeatExecution += RepeatInterval;
                                }

                                if (nextRepeatExecution > DateTime.Now || nextRepeatExecution > EndFinal)
                                {
                                    //Found within repeat
                                    NextExecution = nextRepeatExecution;
                                    break;
                                }
                            }
                        }

                        if (NextExecution > DateTime.Now || NextExecution > EndFinal)
                        {
                            //Found
                            break;
                        }

                        NextExecution = NextExecution.AddDays(7 * WeeksInterval);
                    }
                    break;

                case TriggerType.Monthly:
                    while (true)
                    {
                        if (Months.Length == 0) break;
                        if (Days.Length == 0) break;

                        //Check days and months
                        bool isLastDayOfMonth = (Days.Contains(32) && Months.Contains(NextExecution.Month) && NextExecution.Day == DateTime.DaysInMonth(NextExecution.Year, NextExecution.Month));
                        if (!isLastDayOfMonth)
                        {
                            if (!Days.Contains(NextExecution.Day) || !Months.Contains(NextExecution.Month))
                            {
                                NextExecution = NextExecution.AddDays(1);
                                continue;
                            }
                        }

                        if (NextExecution > DateTime.Now || NextExecution > EndFinal)
                        {
                            //Found
                            break;
                        }

                        if (RepeatInterval != TimeSpan.Zero)
                        {
                            //Handle repeat
                            var nextRepeatExecution = NextExecution;
                            var endRepeatDate = (RepeatDuration != TimeSpan.Zero ? nextRepeatExecution + RepeatDuration : DateTime.MaxValue);
                            while (endRepeatDate > DateTime.Now)
                            {
                                if (nextRepeatExecution > DateTime.Now || nextRepeatExecution > EndFinal || nextRepeatExecution > endRepeatDate)
                                {
                                    break;
                                }
                                nextRepeatExecution += RepeatInterval;
                            }

                            if (nextRepeatExecution > DateTime.Now || nextRepeatExecution > EndFinal)
                            {
                                //Found within repeat
                                NextExecution = nextRepeatExecution;
                                break;
                            }
                        }

                        //Check next day
                        NextExecution = NextExecution.AddDays(1);
                    }
                    break;
            }

            if (NextExecution < DateTime.Now) NextExecution = DateTime.MaxValue;
        }

        public bool IsReached()
        {
            bool result = (
                    Enabled &&
                    Type == TriggerType.Time &&
                    NextExecution <= DateTime.Now
                    )
                 || (
                    Enabled &&
                    Type != TriggerType.Time &&
                    NextExecution <= DateTime.Now &&
                    Start <= DateTime.Now &&
                    EndFinal >= DateTime.Now
                    );

            return result;
        }
    }
}
