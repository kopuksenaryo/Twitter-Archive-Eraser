using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Navigation;
using System.Linq;

using Ionic.Zip;
using LinqToTwitter;
using Newtonsoft.Json;

namespace Twitter_Archive_Eraser
{
    /// <summary>
    /// Interaction logic for DeleteTweets.xaml
    /// </summary>
    public partial class DeleteTweets : Window
    {
        ObservableRangeCollection<Tweet> tweets = new ObservableRangeCollection<Tweet>();
        ObservableRangeCollection<tweetTJson> notDeletedTweets = new ObservableRangeCollection<tweetTJson>();
        string notDeletedTweetsFilename = Directory.GetCurrentDirectory() + "\\not_erased_tweets.js";

        //Used for filtering the tweets
        ICollectionView tweetsCollectionView;

        string userName = (string)Application.Current.Properties["userName"];

        bool hitReturn = false;
        bool isErasing = false;

        const string STATUS_DELETED = "[DELETED ✔]";
        const string STATUS_NOT_FOUND = "[NOT FOUND ǃ]";
        const string STATUS_NOT_ALLOWED = "[NOT ALLOWED ❌]";
        const string STATUS_ERROR = "[ERROR]";

        // list of filters
        List<string> filters = new List<string>();

        DateTime startTime;

        public DeleteTweets()
        {
            InitializeComponent();
            this.Loaded += DeleteTweets_Loaded;
        }

        void DeleteTweets_Loaded(object sender, RoutedEventArgs e)
        {
            startTime = DateTime.Now;
            new Thread(LoadTweets).Start();
            //Thread.CurrentThread.CurrentCulture = new CultureInfo("es-PE"); 
        }


        private void LoadTweets()
        {
            gridTweets.Dispatcher.BeginInvoke(new Action(delegate()
            {
                gridTweets.ItemsSource = null;
                gridOverlayLoading.Visibility = System.Windows.Visibility.Visible;
                gridContainer.IsEnabled = false;
            }));

            tweetsCollectionView = null;
            notDeletedTweets = new ObservableRangeCollection<tweetTJson>();

            List<JsFile> jsFiles = Application.Current.Properties["jsFiles"] as List<JsFile>;
            if (jsFiles == null)
            {
                //TODO error message here
                return;
            }

            foreach (JsFile jsFile in jsFiles)
            {
                tweets.AddRange(GetTweetsFromFile(jsFile));
            }

            txtFeedback.Dispatcher.BeginInvoke(new Action(delegate()
            {
                txtFeedback.Text += "\nDone!";
            }));

            tweetsCollectionView = CollectionViewSource.GetDefaultView(tweets);

            gridTweets.Dispatcher.BeginInvoke(new Action(delegate()
            {
                gridTweets.ItemsSource = tweetsCollectionView;
                gridContainer.IsEnabled = true;
                txtTotalTweetsNB.Text = String.Format("(Total tweets: {0})", tweets.Count);
                gridOverlayLoading.Visibility = System.Windows.Visibility.Hidden;
            }));
        }


        List<Tweet> GetTweetsFromFile(JsFile jsFile)
        {
            txtFeedback.Dispatcher.BeginInvoke(new Action(delegate()
            {
                txtFeedback.Text += "\nLoading tweets: " + jsFile.FriendlyFilename + " " + jsFile.TweetYear;
            }));

            string jsonData = "";

            // Case of js file
            if(String.IsNullOrEmpty(jsFile.OriginZipFile))
            {
                jsonData = File.ReadAllText(jsFile.Path);
            }
            else
	        {
                if (!File.Exists(jsFile.OriginZipFile))
                    return null;

                try
                {
                    using (ZipFile zipArchive = ZipFile.Read(jsFile.OriginZipFile))
                    {
                        MemoryStream stream = new MemoryStream();
                        ZipEntry jsonFile = zipArchive[jsFile.Path];
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
            return LoadTweetsFromJson(jsonData);
        }
        
        List<Tweet> LoadTweetsFromJson(string jsonData)
        {
            List<tweetTJson> tweets = JsonConvert.DeserializeObject<List<tweetTJson>>(jsonData);
            if (tweets == null)
                return null;
            
            return tweets.Select(t => new Tweet { 
                                            ID = t.id_str, 
                                            Text = t.text,
                                            IsRetweet = t.retweeted_status != null ? "    ✔" : "",
                                            ToErase = true, 
                                            Status = "", 
                                            Date = ParseDateTime(t.created_at) }).ToList<Tweet>();
        }

        DateTime ParseDateTime(string str)
        {
            string datePattern = "yyyy-MM-dd H:m:s zzz";
                               //"2013-06-06 00:16:40 +0000"

            DateTimeOffset dto;
            //We use this to prevent the app from crashing if twitter changes the date-time format, again!
            if (DateTimeOffset.TryParseExact(str, datePattern, CultureInfo.InvariantCulture, DateTimeStyles.None, out dto))
            {
                return dto.DateTime;
            }

            return DateTime.MinValue;
        }

        private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (tweetsCollectionView == null)
                return;

            foreach (var item in tweetsCollectionView)
            {
                Tweet t = item as Tweet;
                if (t != null)
                    t.ToErase = true;
            }
        }

        private void SelectAllCheckBox_UnChecked(object sender, RoutedEventArgs e)
        {
            if (tweetsCollectionView == null)
                return;

            foreach (var item in tweetsCollectionView)
            {
                Tweet t = item as Tweet;
                if (t != null)
                    t.ToErase = false;
            }
        }

        private void DG_Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            Hyperlink link = e.OriginalSource as Hyperlink;
            string tweetID = link.NavigateUri.ToString();

            string url = "https://twitter.com/" + userName + "/statuses/" + tweetID;

            Process.Start(url);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            hitReturn = true;
            this.Close();
        }

        private void Window_Closing_1(object sender, CancelEventArgs e)
        {
            cancellationSource.Cancel();
            WebUtils.ReportStats((string)Application.Current.Properties["userName"],
                                    (string)Application.Current.Properties["sessionGUID"],
                                    tweets.Count,
                                    tweets.Where(t => String.Equals(t.Status, STATUS_DELETED)).Count(),
                                    tweets.Where(t => String.Equals(t.Status, STATUS_NOT_FOUND)).Count(),
                                    tweets.Where(t => String.Equals(t.Status, STATUS_ERROR)).Count(),
                                    tweets.Where(t => String.Equals(t.Status, STATUS_NOT_ALLOWED)).Count(),
                                    false,
                                    (int)sliderParallelConnections.Value,
                                    filters,
                                    (DateTime.Now - startTime).TotalSeconds);    

            if (!hitReturn)
                Application.Current.Shutdown();
        }

        void DisableControls()
        {
            //Could be called from a different thread
            btnEraseTweetsLabel.Dispatcher.BeginInvoke(new Action(delegate()
            {
                btnEraseTweetsLabel.Text = "Stop";

                stackProgress.Visibility = System.Windows.Visibility.Visible;
                btnBack.IsEnabled = false;

                grpFilterTweets.IsEnabled = false;
                grpParallelConnections.IsEnabled = false;
            }));
        }

        void EnableControls()
        {
            btnEraseTweetsLabel.Dispatcher.BeginInvoke(new Action(delegate()
            {
                btnEraseTweetsLabel.Text = "Erase selected tweets";
                btnEraseTweets.IsEnabled = true;
                btnBack.IsEnabled = true;

                grpFilterTweets.IsEnabled = true;
                grpParallelConnections.IsEnabled = true;
            }));

            // This is called after all tasks are canceled, should renew cacelation token
            cancellationSource = new CancellationTokenSource();
        }

        private void btnEraseTweets_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (!isErasing)
            {
                if (MessageBox.Show("Are you sure you want to delete all the selected tweets.\nThis cannot be undone!", "Twitter Archive Eraser",
                                    MessageBoxButton.OKCancel, MessageBoxImage.Warning)
                    == MessageBoxResult.OK)
                {
                    new Thread(StartTwitterErase).Start((int)sliderParallelConnections.Value);
                    isErasing = true;

                    DisableControls();
                }
            }
            else
            {
                if (MessageBox.Show("Are you sure you want to stop erasing tweets?", "Twitter Archive Eraser",
                                    MessageBoxButton.OKCancel, MessageBoxImage.Warning)
                    == MessageBoxResult.OK)
                {
                    cancellationSource.Cancel();
                    isErasing = false;

                    btnEraseTweets.IsEnabled = false;
                    btnEraseTweetsLabel.Text = "Stopping...";
                }
            }
            
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }


        void ApplyFilterToCollectionView()
        {
            filters.Add(txtFilterTweets.Text);

            tweetsCollectionView.Filter = t =>
            {
                Tweet tweet = t as Tweet;
                if (tweet == null) return false;

                return Regex.IsMatch(tweet.Text, txtFilterTweets.Text);
            };
            tweetsCollectionView.Refresh();
        }


        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            //CollectionViewSource tweetsDataView = this.Resources["tweetsDataView"] as CollectionViewSource;
            ApplyFilterToCollectionView();
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            tweetsCollectionView.Filter = t => { return true; };
            tweetsCollectionView.Refresh();
        }


        #region Erasing Tweets Logic

        //shared state variables
        int nextTweetID = 0;
        Object _lockerNextTweetID = new Object();

        int nbTweetsDeleted = 0;
        Object _lockerNbTweetsDeleted = new Object();

        Object _lockerNotDeletedTweetsLst = new Object();

        //In case of a cancelation
        CancellationTokenSource cancellationSource = new CancellationTokenSource();

        //The number of the tweets to erase
        int nbTweetsToErase;

#if DEBUG
        int sleepFakeWaitMilliseconds;
#endif

        void onDeletingTweetUIUpdate(Tweet tweet)
        {
            lock (_lockerNbTweetsDeleted)
            {
                nbTweetsDeleted++;
            }

            //update datagrid
            gridTweets.Dispatcher.BeginInvoke(new Action(delegate()
            {
                gridTweets.SelectedItem = tweet;
                gridTweets.ScrollIntoView(tweet);
            }));

            //update progressbar
            progressBar.Dispatcher.BeginInvoke(new Action(delegate()
            {
                progressBar.Value = nbTweetsDeleted * 100 / nbTweetsToErase;
                txtPrcnt.Text = nbTweetsDeleted * 100 / nbTweetsToErase + "%";
            }));
        }

        //Fetched the index (in the tweets collection) of the next tweet to be deleted
        int getNextTweetIDSync()
        {
            //return the next val, increment
            lock (_lockerNextTweetID)
            {
                //As long as we have more tweets to erase
                while (nextTweetID < tweets.Count 
                       && (tweets[nextTweetID].ToErase == false || !String.IsNullOrEmpty(tweets[nextTweetID].Status)))
                {
                    nextTweetID++;
                }

                //Have we reached the end?
                if(nextTweetID == tweets.Count)
                {
                    return Int32.MinValue;
                }
                else //We have got a new tweet to erase
                {
                    //Prepare the next call to fetch the next tweet
                    nextTweetID++;
                    return nextTweetID - 1;
                }
            }
        }


        //We start multiple actions in parallel to delete tweets
        void EraseTweetsAction(TwitterContext ctx, CancellationToken cancelToken) {

            int nextTweetID = getNextTweetIDSync();

#if DEBUG
            Random rnd = new Random();
#endif

            //Are we done?
            while (nextTweetID != Int32.MinValue)
            {
                //We can't cancel here, we have already fetched a new ID and if we cancel here it will never be deteled

                Tweet tweet = tweets[nextTweetID];

                //Clear Tweets logic here
                try
                {
#if DEBUG
                    Thread.Sleep(sleepFakeWaitMilliseconds);
                    if (rnd.Next() % 3 == 0)    // Simulate error
                    {
                        throw new ArgumentNullException();
                    }
                    else
                    {
                        throw new Exception("Sorry, that page does not exist");
                    }
#else
                    Status ret = ctx.DestroyStatus(tweet.ID);
#endif
                    tweet.Status = STATUS_DELETED;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("Sorry, that page does not exist"))
                        tweet.Status = STATUS_NOT_FOUND;
                    else if (ex.Message.Contains("You may not delete another user's status"))
                        tweet.Status = STATUS_NOT_ALLOWED;
                    else
                    {
                        tweet.Status = STATUS_ERROR;
                        var tmp = new tweetTJson() { created_at = tweet.Date.ToString("yyyy-MM-dd H:m:s zzz"), id_str = tweet.ID, text = tweet.Text };

                        lock (_lockerNotDeletedTweetsLst)
                        {
                            notDeletedTweets.Add(tmp);    
                        }
                    }
                }

                onDeletingTweetUIUpdate(tweet);

                //We cancel once a tweet is completely handeled, we make sure not to request for a new one
                if (cancelToken.IsCancellationRequested)
                    return;

                nextTweetID = getNextTweetIDSync();
            }
        }

        void StartTwitterErase(object nbParallelConnectionsObj)
        {
            int nbParallelConnections = (int)nbParallelConnectionsObj;

            TwitterContext ctx = (TwitterContext)Application.Current.Properties["context"];

            //No need to synchronize here, all tasks are (supposed?) not started yet.
            nbTweetsToErase = tweets.Where(t => t.ToErase == true || !String.IsNullOrEmpty(t.Status)).Count();

#if !DEBUG
            if (ctx == null)
            {
                MessageBox.Show("Error loading twitter authentication info; please try again", "Twitter Archive Eraser", MessageBoxButton.OK, MessageBoxImage.Error);
                isErasing = false;
                EnableControls();
                return;
            }
#endif

#if DEBUG
            sleepFakeWaitMilliseconds = 5000 / nbParallelConnections;
#endif

            Task[] tasks = new Task[nbParallelConnections];

            for (int i = 0; i < nbParallelConnections; i++)
            {
                tasks[i] = Task.Factory.StartNew(() => EraseTweetsAction(ctx, cancellationSource.Token));
            }

            try
            {
                Task.WaitAll(tasks);
                EnableControls();
            }
            catch (Exception e)
            {
                nextTweetID = 0;
            }

            isErasing = false;  // Done erasing

            if (nbTweetsDeleted >= (nbTweetsToErase - nbParallelConnections))
            {
                progressBar.Dispatcher.BeginInvoke(new Action(delegate()
                {
                    progressBar.Value = 100;
                    txtPrcnt.Text = "100%";
                    
                    EnableControls();
                    nextTweetID = 0;
                    nbTweetsDeleted = 0;
                    nbTweetsToErase = 0;

                    if (notDeletedTweets.Count == 0)
                    {
                        MessageBox.Show("Done! Everything is clean ;).\n", "Twitter Archive Eraser", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        string jsonTweets = JsonConvert.SerializeObject(notDeletedTweets);
                        File.WriteAllText(notDeletedTweetsFilename, jsonTweets);

                        if(MessageBox.Show(notDeletedTweets.Count + " tweets were not deleted!\n" +
                                                                 "Do you want to retry deleting these tweets again?\n\n" +
                                                                 "You can try deleting these tweets later by loading them in Twitter Archive Eraser from the following file:\n\n" +
                                                                 notDeletedTweetsFilename + "\n\n" +
                                                                 "Select 'Yes' to retry now, or 'No' to retry later",
                                        "Twitter Archive Eraser", 
                                        MessageBoxButton.YesNo, 
                                        MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
                        {
                            WebUtils.ReportStats((string)Application.Current.Properties["userName"],
                                                  (string)Application.Current.Properties["sessionGUID"],
                                                  tweets.Count,
                                                  tweets.Where(t => String.Equals(t.Status, STATUS_DELETED)).Count(),
                                                  tweets.Where(t => String.Equals(t.Status, STATUS_NOT_FOUND)).Count(),
                                                  tweets.Where(t => String.Equals(t.Status, STATUS_ERROR)).Count(),
                                                  tweets.Where(t => String.Equals(t.Status, STATUS_NOT_ALLOWED)).Count(),
                                                  true,
                                                  nbParallelConnections,
                                                  filters,
                                                  (DateTime.Now - startTime).TotalSeconds);

                            tweets = new ObservableRangeCollection<Tweet>();
                            tweets.AddRange(notDeletedTweets.Select(t => new Tweet(){
                                                                Text = t.text,
                                                                ID = t.id_str,
                                                                IsRetweet = t.retweeted_status != null ? "    ✔" : "",
                                                                Status = "",
                                                                ToErase = true,
                                                                Date = ParseDateTime(t.created_at)
                                        }));

                            // We pass an empty list of jsFiles to reuse code from DeleteTweets_Loaded
                            // Nothing of clean code I know!
                            // Sometimes I have hard times sleeping because I know such things is released to the wild
                            Application.Current.Properties["jsFiles"] = new List<JsFile>();
                            DeleteTweets_Loaded(null, null);
                        }
                    }
                }));
            }
        }

        #endregion

        private void sliderParallelConnections_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            grpParallelConnections.Header = "Number of parallel connections: " + sliderParallelConnections.Value + " ";
        }

        private void txtFilterTweets_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                ApplyFilterToCollectionView();
            }
        }
    }    
}
