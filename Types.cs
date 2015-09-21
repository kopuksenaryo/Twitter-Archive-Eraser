using Ionic.Zip;
using LinqToTwitter;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
        public string YearAndMonth
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
        public string FullPath { get; set; }
        public string Filename { get; set; }
        public string FriendlyFilename { get; set; }    // Name of the month TweetMonth
        public int Year { get; set; }
        public int Month { get; set; }
        
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

        public IEnumerable<Tweet> ExtractTweets()
        {
            string jsonData = "";

            // Case of js file
            if (string.IsNullOrEmpty(OriginZipFile))
            {
                jsonData = File.ReadAllText(FullPath);
            }
            else
            {
                // js file from in ZIP archive
                if (!File.Exists(OriginZipFile))
                    return null;

                try
                {
                    // TODO: potential memory improvement by opening the zip file only once
                    using (ZipFile zipArchive = ZipFile.Read(OriginZipFile))
                    {
                        MemoryStream stream = new MemoryStream();
                        ZipEntry jsonFile = zipArchive[FullPath];
                        jsonFile.Extract(stream);
                        stream.Position = 0;
                        using (var reader = new StreamReader(stream))
                        {
                            jsonData = reader.ReadToEnd();
                        }
                    }
                }
                catch (Exception)   //file is not a suitable json
                {
                    ;
                }
            }

            jsonData = jsonData.Substring(jsonData.IndexOf('[') <= 0 ? 0 : jsonData.IndexOf('[') - 1);
            return GetTweetsFromJsonData(jsonData);
        }

        private IEnumerable<Tweet> GetTweetsFromJsonData(string jsonData)
        {
            List<JsonTweet> jsonTweets = JsonConvert.DeserializeObject<List<JsonTweet>>(jsonData);
            if (jsonTweets == null)
                return null;

            return jsonTweets.Select(t => new Tweet
            {
                ID = t.id_str,
                Text = t.text,
                Type = t.retweeted_status != null ? TweetType.Retweet : TweetType.Tweet,
                ToErase = true,
                Status = "",
                Date = Helpers.ParseDateTime(t.created_at)
            });
        }
    }

    public class JsFilesGroup
    {
        // group key 
        public int Key { get; set; }

        public List<JsFile> JsFiles { get; set; }
    }

    class JsonTweet
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

                return string.Format("{0}.{1}.{2}", fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart);
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
