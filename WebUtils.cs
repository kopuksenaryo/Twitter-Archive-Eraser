using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Twitter_Archive_Eraser
{
    class WebUtils
    {
        const string BASE_STATISTICS_URL = "<PUT_STATISTICS_URL_HERE>";
        const string OPERATION_TAG = "OP";
        const string OP_NEW_USER = "OP_NEW_USER";
        const string OP_MONTHS_TO_DELETE = "OP_MONTHS_TO_DELETE";
        const string OP_DELETED_TWEETS = "OP_DELETED_TWEETS";


        public static string SendRequest(string queryString, string userName, string sessionGUID)
        {
            using (WebClient wc = new WebClient())
            {
                return wc.DownloadString(BASE_STATISTICS_URL + "user=" + userName + "&GUID=" + sessionGUID + "&" + queryString);
            }
        }

        public static void ReportNewUser(string userName, string sessionGUID)
        {
            string query = OPERATION_TAG + "=" + OP_NEW_USER;
            SendRequest(query, userName, sessionGUID);
        }

        public static void ReportMonthsToDelete(string userName, string sessionGUID, List<string> years_months)
        {
            string query = OPERATION_TAG + "=" + OP_MONTHS_TO_DELETE + 
                           "&months=" + String.Join(",", years_months);
            SendRequest(query, userName, sessionGUID);
        }

        public static void ReportStats(string userName, string sessionGUID, int total, int nb_deleted, int nb_not_found, 
                                       int nb_error, int nb_not_allowed, bool is_retrying, int nb_parallel, 
                                       List<string> filters, double duration_in_seconds)
        {
            string query = OPERATION_TAG + "=" + OP_DELETED_TWEETS +
                           "&total=" + total +
                           "&nb_deleted=" + nb_deleted +
                           "&nb_not_found=" + nb_not_found +
                           "&nb_error=" + nb_error +
                           "&nb_not_allowed=" + nb_not_allowed +
                           "&nb_parallel=" + nb_parallel +
                           "&filters=" + String.Join("___", filters) +
                           "&is_retrying=" + is_retrying.ToString() +
                           "&duration_in_seconds=" + duration_in_seconds;
            SendRequest(query, userName, sessionGUID);
        }

    }
}
