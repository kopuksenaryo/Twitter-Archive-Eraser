using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Twitter_Archive_Eraser
{
    public static class Helpers
    {
        static string DATE_PATTERN = "yyyy-MM-dd H:m:s zzz";

        public static DateTime ParseDateTime(string str)
        {
            DateTimeOffset dto;
            //We use this to prevent the app from crashing if twitter changes the date-time format, again!
            if (DateTimeOffset.TryParseExact(str, DATE_PATTERN, CultureInfo.InvariantCulture, DateTimeStyles.None, out dto))
            {
                return DateTime.SpecifyKind(dto.DateTime, DateTimeKind.Local);
            }

            return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Local);
        }

        public static string DateTimeToString(DateTime t)
        {
            return t.ToString(DATE_PATTERN);
        }

    }
}
