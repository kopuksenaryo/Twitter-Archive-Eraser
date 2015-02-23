using LinqToTwitter;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;

namespace Twitter_Archive_Eraser
{
    public class Tweet : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string ID { get; set; }
        public string Text { get; set; }
        public string Username { get; set; }
        public DateTime Date { get; set; }
        public string YearMonth
        {
            get
            {
                return string.Format("{0} / {1}", Date.Year, Date.ToString("MMMM"));
            }
        }
        public TweetType Type { get; set; }

        private string _status;
        public string Status
        {
            get { return _status; }
            set
            {
                if (value != _status)
                {
                    _status = value;
                    NotifyPropertyChanged("");
                }
            }
        }

        private bool _toErase;
        public bool ToErase
        {
            get { return _toErase; }

            set
            {
                if (value != _toErase)
                {
                    _toErase = value;
                    NotifyPropertyChanged("");
                }
            }
        }

        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }
    }

    public enum TweetType
    {
        Tweet,
        Favorite,
        DM,
        Retweet
    }

    public class JsFile : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string OriginZipFile { get; set; }
        public string Path { get; set; }
        public string Filename { get; set; }
        public string FriendlyFilename { get; set; }    // Name of the month TweetMonth
        public int TweetYear { get; set; }
        public int TweetMonth { get; set; }
        

        private bool _selected;
        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (value != _selected)
                {
                    _selected = value;
                    NotifyPropertyChanged("");
                }
            }
        }

        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }
    }

    public class YearOfTweets
    {
        public int Year { get; set; }

        public List<JsFile> TweetJsFiles { get; set; }
    }

    class tweetTJson
    {
        public string id_str { get; set; }
        public string text { get; set; }
        public string created_at { get; set; }
        public Object retweeted_status { get; set; }
    }

    class ApplicationSettings
    {
        public TwitterContext Context { get; set; }
        public string Username { get; set; }
        public ulong UserID { get; set; }
        public Guid SessionId { get; set; }

        public List<JsFile> JsFiles { get; set; }

        public int NumTeetsDeleted { get; set; }

        public EraseTypes EraseType { get; set; }

        public long TotalRunningMillisec { get; set; }

        public string Version
        {
            get
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
                return fvi.FileMajorPart + "." + fvi.FileMinorPart;
            }
        }

        public ApplicationSettings()
        {
            EraseType = EraseTypes.TweetsAndRetweets;
        }

        public static void SetApplicationSettings(ApplicationSettings settings)
        {
            Application.Current.Properties["SETTINGS"] = settings;
        }

        public static ApplicationSettings GetApplicationSettings()
        {
            if (Application.Current.Properties["SETTINGS"] == null)
            {
                Application.Current.Properties["SETTINGS"] = new ApplicationSettings();
            }

            return Application.Current.Properties["SETTINGS"] as ApplicationSettings;
        }

        public enum EraseTypes
        {
            TweetsAndRetweets,
            Favorites,
            DirectMessages
        }
    }
}
