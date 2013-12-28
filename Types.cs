using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Twitter_Archive_Eraser
{
    public class Tweet : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string ID { set; get; }
        public string Text { set; get; }
        public DateTime Date { set; get; }
        public string IsRetweet { set; get; }

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
}
