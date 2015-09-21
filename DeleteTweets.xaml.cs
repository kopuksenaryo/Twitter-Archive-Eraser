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
using System.Windows.Controls;
using Xceed.Wpf.DataGrid;
using System.Windows.Controls.Primitives;

namespace Twitter_Archive_Eraser
{
    /// <summary>
    /// Interaction logic for DeleteTweets.xaml
    /// </summary>
    public partial class DeleteTweets : Window
    {
        private ObservableRangeCollection<Tweet> mTweetsCollection = new ObservableRangeCollection<Tweet>();
        List<JsonTweet> notDeletedTweets = new List<JsonTweet>();
        string notDeletedTweetsFilename = Directory.GetCurrentDirectory() + "\\not_erased_tweets.js";
        public bool areTweetsFetchedThroughAPI = false;
        
        //Used for filtering the tweets
        ICollectionView tweetsCollectionView;

        static ApplicationSettings appSettings = ApplicationSettings.GetApplicationSettings();

        string userName = appSettings.Username;
        ApplicationSettings.EraseTypes TweetsEraseType = appSettings.EraseType;

        bool hitReturn = false;
        bool isCurrentlyErasing = false;
        bool isFilterViewActive = false;

        const string STATUS_DELETED = "[DELETED ✔]";
        const string STATUS_NOT_FOUND = "[NOT FOUND ǃ]";
        const string STATUS_NOT_ALLOWED = "[NOT ALLOWED ❌]";
        const string STATUS_ERROR = "[ERROR]";

        bool filterShowRetweetsOnly = false;
        bool isRegularExpressionMatch = false;
        bool filterTargetUsername = false;

        // list of filters
        List<string> filters = new List<string>();

        DateTime startTime;
        Stopwatch runningTime = new Stopwatch();

        public DeleteTweets()
        {
            InitializeComponent();
            this.Loaded += DeleteTweets_Loaded;
        }

        void DeleteTweets_Loaded(object sender, RoutedEventArgs e)
        {
            // Show/Hide options depending on what the user wants to delete
            var settings = ApplicationSettings.GetApplicationSettings();
            switch (settings.EraseType)
            {
                case ApplicationSettings.EraseTypes.TweetsAndRetweets:
                    chkSearchByUsername.Visibility = System.Windows.Visibility.Collapsed;
                    break;
                case ApplicationSettings.EraseTypes.Favorites:
                case ApplicationSettings.EraseTypes.DirectMessages:
                    chkShowRetweetOnly.Visibility = System.Windows.Visibility.Collapsed;
                    btnBack.IsEnabled = false;
                    break;
                default:
                    break;
            }

            startTime = DateTime.Now;
            new Thread(LoadTweetsFromJsFiles).Start();
            //Thread.CurrentThread.CurrentCulture = new CultureInfo("es-PE"); 

            // Hide DM sender/receiver column when deleting tweets/favorites
            var column = gridTweets.Columns.Where(c => c.FieldName == "Username").FirstOrDefault();
            if(column != null && TweetsEraseType == ApplicationSettings.EraseTypes.TweetsAndRetweets) 
            {
                column.Visible = false;
            }

            // Hide extra ToErase column. When providing a custom template for that field, the grid view choses to add it anywyas
            column = gridTweets.Columns.Where(c => c.FieldName == "ToErase").FirstOrDefault();
            if (column != null)
            {
                column.Visible = false;
            }
        }

        private void RunOnUIThread(Action action)
        {
            Dispatcher.BeginInvoke(action);
        }

        public void SetTweetsList(IEnumerable<Tweet> input)
        {
            mTweetsCollection.AddRange(input);
        }

        private void LoadTweetsFromJsFiles()
        {
            RunOnUIThread(delegate()
            {
                gridTweets.ItemsSource = null;
                gridOverlayLoading.Visibility = System.Windows.Visibility.Visible;
                gridContainer.IsEnabled = false;
            });
            
            tweetsCollectionView = null;
            notDeletedTweets = new List<JsonTweet>();

            // If caller already assigned the list of tweets
            if (areTweetsFetchedThroughAPI)
            {
                ;
            }
            else
            {
                List<JsFile> jsFiles = appSettings.JsFiles;
                if (jsFiles == null)
                {
                    //TODO error message here
                    return;
                }

                foreach (JsFile jsFile in jsFiles)
                {
                    RunOnUIThread(delegate()
                    {
                        txtFeedback.Text += "\nLoading tweets: " + jsFile.FriendlyFilename + " " + jsFile.Year;
                    });

                    mTweetsCollection.AddRange(jsFile.ExtractTweets());
                }
            }

            RunOnUIThread(delegate()
            {
                txtFeedback.Text += "\nDone!";
            });

            mTweetsCollection.OrderBy(t => t.Date);
            
            tweetsCollectionView = CollectionViewSource.GetDefaultView(mTweetsCollection);
            tweetsCollectionView.GroupDescriptions.Add(new PropertyGroupDescription("YearAndMonth"));
            // tweetsCollectionView.GroupDescriptions.Add(new PropertyGroupDescription("Type"));

            RunOnUIThread(delegate()
            {
                gridTweets.ItemsSource = tweetsCollectionView;
                gridTweets.ItemScrollingBehavior = ItemScrollingBehavior.Deferred;

                gridContainer.IsEnabled = true;
                txtTotalTweetsNB.Text = string.Format("(Total tweets: {0})", mTweetsCollection.Count);
                gridOverlayLoading.Visibility = Visibility.Hidden;
            });
        }

        private void DG_Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            var button = e.OriginalSource as Button;
            if (button == null)
                return;

            string tweetID = button.Content.ToString();

            string url = "https://twitter.com/" + userName + "/statuses/" + tweetID;

            Process.Start(url);
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            hitReturn = true;
            this.Close();
        }

        private void Window_Closing_1(object sender, CancelEventArgs e)
        {
            globalCancellationSource.Cancel();
            WebUtils.ReportStats(appSettings.Username,
                                    appSettings.SessionId.ToString(),
                                    appSettings.EraseType.ToString(),
                                    mTweetsCollection.Count,
                                    mTweetsCollection.Where(t => String.Equals(t.Status, STATUS_DELETED)).Count(),
                                    mTweetsCollection.Where(t => String.Equals(t.Status, STATUS_NOT_FOUND)).Count(),
                                    mTweetsCollection.Where(t => String.Equals(t.Status, STATUS_ERROR)).Count(),
                                    mTweetsCollection.Where(t => String.Equals(t.Status, STATUS_NOT_ALLOWED)).Count(),
                                    false,
                                    (int)sliderParallelConnections.Value,
                                    filters,
                                    (DateTime.Now - startTime).TotalSeconds);    

            // if user didn't hit the return button, this is a legit exit
            if (!hitReturn)
                Environment.Exit(0);
        }

        void SetDeletionInProgressState()
        {
            //Could be called from a different thread
            RunOnUIThread(delegate()
            {
                btnEraseTweetsLabel.Text = "Stop";

                stackProgress.Visibility = Visibility.Visible;
                progressBar2.IsIndeterminate = true;
                btnBack.IsEnabled = false;

                grpFilterTweets.IsEnabled = false;
                grpParallelConnections.IsEnabled = false;

                runningTime.Start();
            });
        }

        void SetStoppedDeletionState()
        {
            RunOnUIThread(delegate()
            {
                btnEraseTweetsLabel.Text = "Erase selected tweets";
                btnEraseTweets.IsEnabled = true;

                // can go back to select archive files, no back to fetch favs/DMs
                if (appSettings.EraseType == ApplicationSettings.EraseTypes.TweetsAndRetweets)
                {
                    btnBack.IsEnabled = true;
                }

                grpFilterTweets.IsEnabled = true;
                grpParallelConnections.IsEnabled = true;

                progressBar2.IsIndeterminate = false;

                runningTime.Stop();
            });
        }

        // Trigger start/pause deletion of tweets
        private void btnEraseTweets_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (isFilterViewActive)
            {
                MessageBox.Show("You are viewing only a subset of tweets. Please click 'Show all/Reset filter' button to show all the tweets first.", "Twitter Archive Eraser");
                return;
            }

            if (!isCurrentlyErasing)
            {
                if (MessageBox.Show("Are you sure you want to delete all the selected tweets.\nThis cannot be undone!", "Twitter Archive Eraser",
                                    MessageBoxButton.OKCancel, MessageBoxImage.Warning)
                    == MessageBoxResult.OK)
                {
                    isCurrentlyErasing = true;
                    SetDeletionInProgressState();

                    int nbParallel = (int)sliderParallelConnections.Value;
                    Task.Factory.StartNew(() => StartTwitterErase(nbParallel));
                }
            }
            else
            {
                if (MessageBox.Show("Are you sure you want to stop erasing tweets?", "Twitter Archive Eraser",
                                    MessageBoxButton.OKCancel, MessageBoxImage.Warning)
                    == MessageBoxResult.OK)
                {
                    //isErasing = false;
                    globalCancellationSource.Cancel();
                    btnEraseTweets.IsEnabled = false;
                    btnEraseTweetsLabel.Text = "Stopping...";
                }
            }
        }

        #region Erasing Tweets Logic

        //shared state variables
        int nextTweetID = 0;
        Object _lockerNextTweetID = new Object();

        // modified only using atomic Interlocked operations
        int nbOutstandingOperations = 0;

        Object _lockerNotDeletedTweetsLst = new Object();

        //In case of a cancelation
        CancellationTokenSource globalCancellationSource = new CancellationTokenSource();

        //The number of the tweets to erase
        int nbTweetsToErase;

#if DEBUG_TEST
        int sleepFakeWaitMilliseconds;
#endif

        long lastDataGridScroll = 0;
        object _lockerLastDataGridScroll = new object();
        const int MIN_SCROLL_INTERVAL = 5;

        void onDeletedTweetUIUpdate(Tweet tweet)
        {
            Interlocked.Decrement(ref nbOutstandingOperations);

            // scroll only after X seconds
            if (DateTime.Now.Ticks - lastDataGridScroll > (TimeSpan.TicksPerSecond * MIN_SCROLL_INTERVAL))
            {
                lock (_lockerLastDataGridScroll)
                {
                    if (DateTime.Now.Ticks - lastDataGridScroll > (TimeSpan.TicksPerSecond * MIN_SCROLL_INTERVAL))
                    {
                        //update datagrid
                        RunOnUIThread(delegate()
                        {
                            gridTweets.SelectedItem = tweet;
                            gridTweets.BringItemIntoView(tweet);
                        });

                        lastDataGridScroll = DateTime.Now.Ticks;
                    }
                }
            }

            //update progressbar
            RunOnUIThread(delegate()
            {
                progressBar.Value = 100 - (nbOutstandingOperations * 100 / nbTweetsToErase);
                txtPrcnt.Text = 100 - (nbOutstandingOperations * 100 / nbTweetsToErase) + "%";
            });
        }

        //Fetched the index (in the tweets collection) of the next tweet to be deleted
        int getNextTweetIDSync()
        {
            //return the next val, increment
            lock (_lockerNextTweetID)
            {
                //As long as we have more tweets to erase
                while (nextTweetID < mTweetsCollection.Count &&/* didnt hit end of collection */
                      (
                      mTweetsCollection[nextTweetID].ToErase == false /* skip this tweet */ 
                      || !string.IsNullOrEmpty(mTweetsCollection[nextTweetID].Status) /* already tried to delete this one */
                      ))
                {
                    nextTweetID++;
                }

                //Have we reached the end?
                if(nextTweetID == mTweetsCollection.Count)
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

        void StartTwitterErase(int nbParallelConnections)
        {
            TwitterContext ctx = appSettings.Context;

            //No need to synchronize here, no tasks were started yet.
            nbTweetsToErase = mTweetsCollection.Where(t => t.ToErase == true && string.IsNullOrEmpty(t.Status) /* did not get deleted previously */).Count();
            nbOutstandingOperations = nbTweetsToErase;
            nextTweetID = 0;

#if !DEBUG_TEST
            if (ctx == null)
            {
                MessageBox.Show("Error loading twitter authentication info; please try again", "Twitter Archive Eraser", MessageBoxButton.OK, MessageBoxImage.Error);
                isCurrentlyErasing = false;
                SetStoppedDeletionState();
                return;
            }
#endif

#if DEBUG_TEST
            sleepFakeWaitMilliseconds = 1000;
#endif

            Task[] tasks = new Task[nbParallelConnections];

            for (int i = 0; i < nbParallelConnections; i++)
            {
                tasks[i] = Task.Factory.StartNew(() => EraseTweetsAction(ctx, globalCancellationSource.Token));
            }

            try
            {
                Task.WaitAll(tasks);
            }
            catch (Exception e)
            {
                nextTweetID = 0;
                RunOnUIThread(delegate ()
                {
                    MessageBox.Show("Error: " + e.Message);
                });
            }
            finally
            {
                
                SetStoppedDeletionState();

                // This is called after all tasks are canceled, should renew cacelation token
                globalCancellationSource = new CancellationTokenSource();
            }

            isCurrentlyErasing = false;  // Done erasing
            
            //termination condition
            if (nbOutstandingOperations == 0) // nbTweetsDeleted >= (nbTweetsToErase - nbParallelConnections))
            {
                RunOnUIThread(delegate()
                {
                    progressBar.Value = 100;
                    txtPrcnt.Text = "100%";
                    
                    SetStoppedDeletionState();
                    nextTweetID = 0;
                    nbTweetsToErase = 0;

                    var lastTweet = mTweetsCollection.Where(t => !string.IsNullOrEmpty(t.Status)).LastOrDefault();
                    if(lastTweet != null)
                    {
                        gridTweets.BringItemIntoView(lastTweet);
                    }

                    appSettings.TotalRunningMillisec += runningTime.ElapsedMilliseconds;

                    if (notDeletedTweets.Count == 0)
                    {
                        ShowShareTweetDialog(mTweetsCollection.Where(t => String.Equals(t.Status, STATUS_DELETED)).Count());
                    }
                    else
                    {
                        string jsonTweets = JsonConvert.SerializeObject(notDeletedTweets);
                        File.WriteAllText(notDeletedTweetsFilename, jsonTweets);

                        if (MessageBox.Show(notDeletedTweets.Count + " tweets were not deleted!\n" +
                                                                 "Do you want to retry deleting these tweets again?\n\n" +
                                                                 "You can try deleting these tweets later by loading them in Twitter Archive Eraser from the following file:\n\n" +
                                                                 notDeletedTweetsFilename + "\n\n" +
                                                                 "Select 'Yes' to retry now, or 'No' to retry later",
                                        "Twitter Archive Eraser",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
                        {
                            WebUtils.ReportStats(appSettings.Username,
                                                  appSettings.SessionId.ToString(),
                                                  appSettings.EraseType.ToString(),
                                                  mTweetsCollection.Count,
                                                  mTweetsCollection.Where(t => String.Equals(t.Status, STATUS_DELETED)).Count(),
                                                  mTweetsCollection.Where(t => String.Equals(t.Status, STATUS_NOT_FOUND)).Count(),
                                                  mTweetsCollection.Where(t => String.Equals(t.Status, STATUS_ERROR)).Count(),
                                                  mTweetsCollection.Where(t => String.Equals(t.Status, STATUS_NOT_ALLOWED)).Count(),
                                                  true,
                                                  nbParallelConnections,
                                                  filters,
                                                  (DateTime.Now - startTime).TotalSeconds);

                            gridTweets.ItemsSource = null;
                            gridTweets.Items.Refresh();

                            tweetsCollectionView = null;

                            mTweetsCollection = new ObservableRangeCollection<Tweet>();

                            mTweetsCollection.AddRange(notDeletedTweets.Select(t => new Tweet()
                            {
                                Text = t.text,
                                ID = t.id_str,
                                Type = t.retweeted_status != null ? TweetType.Retweet : TweetType.Tweet,
                                Status = "",
                                ToErase = true,
                                Date = Helpers.ParseDateTime(t.created_at)
                            }));

                            areTweetsFetchedThroughAPI = true;

                            notDeletedTweets.Clear();

                            // We pass an empty list of jsFiles to reuse code from DeleteTweets_Loaded
                            // Nothing of clean code I know!
                            // Sometimes I have hard times sleeping because I know such things are released in the wild
                            appSettings.JsFiles = new List<JsFile>();
                            DeleteTweets_Loaded(null, null);
                        }
                        else
                        {
                            ShowShareTweetDialog(mTweetsCollection.Where(t => String.Equals(t.Status, STATUS_DELETED)).Count());
                        }
                    }
                });
            }
        }

        //We start multiple actions in parallel to delete tweets
        void EraseTweetsAction(TwitterContext ctx, CancellationToken cancellationToken)
        {
            int nextTweetID = getNextTweetIDSync();

#if DEBUG_TEST
            Random rnd = new Random();
#endif

            //Are we done?
            while (nextTweetID != Int32.MinValue)
            {
                //We can't cancel here, we have already fetched a new ID and if we cancel here it will never be deteled
                Tweet tweet = mTweetsCollection[nextTweetID];

                //Clear Tweets logic here
                try
                {
#if DEBUG_TEST
                    Thread.Sleep(sleepFakeWaitMilliseconds);
                    if (rnd.Next() % 3 == 0)    // Simulate error
                    {
                        throw new ArgumentNullException();
                    }
                    else
                    {
                        Exception e = new Exception("Sorry, that page does not exist");
                        throw new Exception("", e);
                    }
#else
                    ulong tid = ulong.Parse(tweet.ID);
                    Status ret = null;
                    DirectMessage ret2 = null;

                    switch (TweetsEraseType)
                    {
                        case ApplicationSettings.EraseTypes.TweetsAndRetweets:
                            ret = ctx.DeleteTweetAsync(tid).Result;
                            break;
                        case ApplicationSettings.EraseTypes.Favorites:
                            ret = ctx.DestroyFavoriteAsync(tid).Result;
                            break;
                        case ApplicationSettings.EraseTypes.DirectMessages:
                            ret2 = ctx.DestroyDirectMessageAsync(tid, true).Result;
                            break;
                        default:
                            break;
                    }
#endif
                    tweet.Status = STATUS_DELETED;
                }
                catch (Exception ex)
                {
                    TwitterQueryException exception = ex.InnerException as TwitterQueryException;
                    if (exception != null && exception.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        tweet.Status = STATUS_NOT_FOUND;
                    }
                    else if (exception != null &&
                            (exception.StatusCode == System.Net.HttpStatusCode.Unauthorized || exception.StatusCode == System.Net.HttpStatusCode.Forbidden))
                    {
                        tweet.Status = STATUS_NOT_ALLOWED;
                    }
                    else
                    {
                        tweet.Status = STATUS_ERROR;
                        var tmp = new JsonTweet() { created_at = Helpers.DateTimeToString(tweet.Date), id_str = tweet.ID, text = tweet.Text };

                        lock (_lockerNotDeletedTweetsLst)
                        {
                            notDeletedTweets.Add(tmp);
                        }
                    }
                }
                finally
                {
                    onDeletedTweetUIUpdate(tweet);
                }

                //We cancel once a tweet is completely handeled, we make sure not to request for a new one
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                nextTweetID = getNextTweetIDSync();
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
                isFilterViewActive = true;
            }
        }

        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilterToCollectionView();
            isFilterViewActive = true;
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            tweetsCollectionView.Filter = t => { return true; };
            tweetsCollectionView.Refresh();
            isFilterViewActive = false;

            filterShowRetweetsOnly = false;
            chkShowRetweetOnly.IsChecked = false;
            txtFilterTweets.Text = "";
        }

        void ShowShareTweetDialog(int numTweetsDeleted)
        {
            appSettings.NumTeetsDeleted = numTweetsDeleted;
            SendTweet sendTweetWindow = new SendTweet();
            sendTweetWindow.ShowDialog();
        }

        void ApplyFilterToCollectionView()
        {
            filters.Add(txtFilterTweets.Text);

            if (isRegularExpressionMatch)
            {
                try
                {
                    // validate that regex pattern is valid
                    Regex.IsMatch("dummy", txtFilterTweets.Text, RegexOptions.IgnoreCase);
                }
                catch (Exception)
                {
                    MessageBox.Show(string.Format("Invalid RegEx pattern {0}", txtFilterTweets.Text), "Twitter Archive Eraser");
                    return;
                }
            }

            // reset previous filter
            tweetsCollectionView.Filter = t => { return true; };
            tweetsCollectionView.Refresh();

            if (isRegularExpressionMatch)
            {
                tweetsCollectionView.Filter = t =>
                {
                    Tweet tweet = t as Tweet;
                    if (tweet == null) return false;

                    if (filterTargetUsername)
                    {
                        return Regex.IsMatch(tweet.Username, txtFilterTweets.Text, RegexOptions.IgnoreCase)
                            && (filterShowRetweetsOnly == true ? tweet.Type == TweetType.Retweet : true);
                    }
                    else
                    {
                        return Regex.IsMatch(tweet.Text, txtFilterTweets.Text, RegexOptions.IgnoreCase)
                            && (filterShowRetweetsOnly == true ? tweet.Type == TweetType.Retweet : true);
                    }
                };
            }
            else
            {
                tweetsCollectionView.Filter = t =>
                {
                    Tweet tweet = t as Tweet;
                    if (tweet == null) return false;

                    if(filterTargetUsername)
                    {
                        return tweet.Username.ToLowerInvariant().Contains(txtFilterTweets.Text.ToLowerInvariant())
                            && (filterShowRetweetsOnly == true ? tweet.Type == TweetType.Retweet : true);
                    }
                    else
                    {
                        return tweet.Text.ToLowerInvariant().Contains(txtFilterTweets.Text.ToLowerInvariant())
                            && (filterShowRetweetsOnly == true ? tweet.Type == TweetType.Retweet : true);
                    }
                };
            }

            tweetsCollectionView.Refresh();
        }

        private void chkSimpleTextSearch_Click(object sender, RoutedEventArgs e)
        {
            isRegularExpressionMatch = chkSimpleTextSearch.IsChecked == true ? true : false;
        }

        private void chkShowRetweetOnly_Click(object sender, RoutedEventArgs e)
        {
            filterShowRetweetsOnly = chkShowRetweetOnly.IsChecked == true ? true : false;
        }

        private void chkSearchByUsername_Click(object sender, RoutedEventArgs e)
        {
            filterTargetUsername = chkSearchByUsername.IsChecked == true ? true : false;
        }

        private void chkGroupHeader_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if(checkBox == null)
                return;

            if (tweetsCollectionView == null)
                return;

            string groupYearMoth = checkBox.Tag.ToString();
            bool isChecked = checkBox.IsChecked == true ? true : false;

            foreach (var item in tweetsCollectionView)
            {
                Tweet t = item as Tweet;
                if (t != null && t.YearAndMonth == groupYearMoth)
                    t.ToErase = isChecked;
            }
        }

        private void chkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox == null)
                return;

            if (tweetsCollectionView == null)
                return;

            bool isChecked = checkBox.IsChecked ?? false;

            foreach (var item in tweetsCollectionView)
            {
                Tweet t = item as Tweet;
                if (t != null)
                    t.ToErase = isChecked;
            }
        }

        private void btnExpandCollapseGroupHeader_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton b = sender as ToggleButton;
            var g = b.Tag as Xceed.Wpf.DataGrid.Group;

            // null checks??
            g.IsExpanded = !g.IsExpanded;
        }
    }    
}
