using LinqToTwitter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Twitter_Archive_Eraser
{
    /// <summary>
    /// Interaction logic for FetchTweets.xaml
    /// </summary>
    public partial class FetchTweets : Window
    {
        System.Timers.Timer m_Timer = new System.Timers.Timer();
        DateTime RateLimitReset;

        bool isQueryingTwitter = false;
        List<Favorites> favsTweetList = new List<Favorites>();
        List<DirectMessage> dmsTweetList = new List<DirectMessage>();

        TwitterContext twitterCtx;
        ApplicationSettings.EraseTypes TweetsEraseType;

        Stopwatch stopWatch;

        public FetchTweets()
        {
            InitializeComponent();
            this.Loaded += FetchTweets_Loaded;

            stopWatch = new Stopwatch();

            m_Timer.Elapsed += m_Timer_Elapsed;
            m_Timer.Interval = 1000;
            new Thread(DoFetchTweets).Start();
        }

        async void FetchTweets_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = ApplicationSettings.GetApplicationSettings();
            twitterCtx = settings.Context;
            TweetsEraseType = settings.EraseType;

            //this.Title += " v" + settings.Version;

            if(twitterCtx == null)
            {
                MessageBox.Show("Internal error: Twitter context is null", "Twitter Archive Eraser");
            }

            switch (TweetsEraseType)
            {
                case ApplicationSettings.EraseTypes.TweetsAndRetweets:
                    throw new Exception("EraseTye should never be EraseTypes.TweetsAndRetweets here");
                    break;
                case ApplicationSettings.EraseTypes.Favorites:
                    var user =
                        await
                        (from tweet in twitterCtx.User
                         where tweet.Type == UserType.Show &&
                               tweet.UserID == settings.UserID
                         select tweet)
                        .SingleOrDefaultAsync();

                    int totalFavCount = 0;
                    if (user != null)
                    {
                        totalFavCount = user.FavoritesCount;
                    }

                    lblTweetsType.Text = "favorites";
                    lblTweetsType2.Text = "favorites";
                    lblTweetsMax.Text = "3000";
                    lblTotalTweetsNB.Text = string.Format("(Your favorites count: {0})", totalFavCount);
                    break;
                case ApplicationSettings.EraseTypes.DirectMessages:
                    lblTweetsType.Text = "direct messages";
                    lblTweetsType2.Text = "DMs";
                    lblTweetsMax.Text = "1000 (800 sent, 200 received)";
                    lblTotalTweetsNB.Text = string.Format("(Your DM count: {0})", "N/A");

                    imgTwitterAuth2.Source = new BitmapImage(new Uri(@"pack://application:,,,/Twitter Archive Eraser;component/dm-icon.png"));
                    imgTwitterAuth2.Margin = new Thickness(5);

                    break;
                default:
                    break;
            }
            //int numFavs = twitterCtx.User.Where(user => user.UserID == settings.UserID).FirstOrDefault().FavoritesCount;
            //txtTotalTweetsNB.Text = string.Format("(Your favorites count: {0})", numFavs);
        }

        private void btnFetch_Click(object sender, RoutedEventArgs e)
        {
            ToggleIsQueryingTwitterFlag();
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            isQueryingTwitter = false;
            btnFetch.IsEnabled = false;

            if(dmsTweetList.Count == 0 && favsTweetList.Count == 0)
            {
                MessageBox.Show("No tweets to delete! Please click start to fetch tweets.", "Twitter Archive Eraser");
                btnFetch.IsEnabled = true;
                return;
            }

            DeleteTweets page = new DeleteTweets();

            if (TweetsEraseType == ApplicationSettings.EraseTypes.Favorites)
            {
                page.tweets.AddRange(favsTweetList.Select(t => new Tweet()
                {
                    ID = t.StatusID.ToString(),
                    Username = "@" + t.User.Name,
                    Text = t.Text,
                    ToErase = true,
                    Type = TweetType.Favorite,
                    Date = t.CreatedAt
                }));
            }

            if(TweetsEraseType == ApplicationSettings.EraseTypes.DirectMessages)
            {
                page.tweets.AddRange(dmsTweetList.Select(t => new Tweet()
                {
                    ID = t.IDResponse.ToString(),
                    Username = t.Type == DirectMessageType.SentBy ? "[To] @" + t.RecipientScreenName : "[From] @" + t.SenderScreenName,
                    Text = t.Text,
                    ToErase = true,
                    Type = TweetType.DM,
                    Date = t.CreatedAt
                }));
            }

            // Free memory
            dmsTweetList.Clear();
            favsTweetList.Clear();
            dmsTweetList = null;
            favsTweetList = null;

            page.areTweetsFetchedThroughAPI = true;

            this.Hide();
            page.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            ApplicationSettings.GetApplicationSettings().TotalRunningMillisec = stopWatch.ElapsedMilliseconds;

            stopWatch.Reset();

            page.ShowDialog();
            this.Show();
        }

        void ToggleIsQueryingTwitterFlag()
        {
            isQueryingTwitter = !isQueryingTwitter;
            btnFetchTxt.Dispatcher.BeginInvoke(new Action(delegate()
            {
                btnFetchTxt.Text = isQueryingTwitter ? "Stop" : "Start";
                if(isQueryingTwitter)
                {
                    stopWatch.Start();
                }
                else
                {
                    stopWatch.Stop();
                }
            }));
        }

        void StopProgressStatus()
        {
            progressBar.Dispatcher.BeginInvoke(new Action(delegate()
            {
                progressBar.IsIndeterminate = false;
                //progressBar.Maximum = 10;
                //progressBar.Value = 4;
            }));
        }

        void StartProgressStatus()
        {
            progressBar.Dispatcher.BeginInvoke(new Action(delegate()
            {
                progressBar.IsIndeterminate = true;
            }));
        }

        void DoFetchTweets()
        {
            ulong sinceID = 1;
            ulong maxID = 0;

            List<Favorites> favsResponse = new List<Favorites>();
            List<DirectMessage> dmsResponse = new List<DirectMessage>();

            while (true)
            {
                if(!isQueryingTwitter)
                {
                    StopProgressStatus();
                    Thread.Sleep(200);
                    continue;
                }

                StartProgressStatus();

                bool shouldContinue = true;
                do
                {
                    if (!isQueryingTwitter)
                    {
                        StopProgressStatus();
                        Thread.Sleep(200);
                        continue;
                    }

                    StartProgressStatus();

                    try
                    {
                        switch (this.TweetsEraseType)
                        {
                            case ApplicationSettings.EraseTypes.TweetsAndRetweets:
                                throw new Exception("EraseTye should never be EraseTypes.TweetsAndRetweets here");
                                break;
                            case ApplicationSettings.EraseTypes.Favorites:
                                favsResponse = QueryFavorites(twitterCtx, sinceID, maxID);
                                break;
                            case ApplicationSettings.EraseTypes.DirectMessages:
                                dmsResponse = QueryDMs(twitterCtx, sinceID, maxID);
                                break;
                            default:
                                break;
                        }                      
                    }
                    catch (Exception ex)
                    {
                        if (twitterCtx.RateLimitRemaining == 0)
                        {
                            RateLimitReset = FromUnixTime(twitterCtx.RateLimitReset);
                            HandleReachedTwitterLimits();
                        }
                        else
                        {
                            this.Dispatcher.BeginInvoke(new Action(delegate()
                            {
                                MessageBox.Show(ex.Message);
                            }));
                        }

                        ToggleIsQueryingTwitterFlag();
                        break;
                    }

                    // fetched all the favorites
                    if ((TweetsEraseType == ApplicationSettings.EraseTypes.Favorites && (favsResponse == null || favsResponse.Count == 0))
                        || (TweetsEraseType == ApplicationSettings.EraseTypes.DirectMessages && (dmsResponse == null || dmsResponse.Count == 0)))
                    {
                        lblContinue.Dispatcher.BeginInvoke(new Action(delegate()
                        {
                            lblContinue.Text = "Done fetching tweets!. Please click 'Next' to go to the filter & delete page.";
                            lblContinue.Visibility = System.Windows.Visibility.Visible;
                            stackReachedLimits.Visibility = System.Windows.Visibility.Collapsed;
                        }));

                        ToggleIsQueryingTwitterFlag();
                        break;
                    }

                    // query next batch
                    if (TweetsEraseType == ApplicationSettings.EraseTypes.Favorites)
                    {
                        maxID = favsResponse.Min(fav => fav.StatusID) - 1;
                        favsTweetList.AddRange(favsResponse);
                        UpdateNumFetchedTweets(favsTweetList.Count);

                        shouldContinue = favsResponse.Count > 0;
                    }

                    if (TweetsEraseType == ApplicationSettings.EraseTypes.DirectMessages)
                    {
                        maxID = dmsResponse.Min(dm => dm.IDResponse) - 1;
                        dmsTweetList.AddRange(dmsResponse);
                        UpdateNumFetchedTweets(dmsTweetList.Count);

                        shouldContinue = dmsResponse.Count > 0;
                    }

                } while (shouldContinue);
            }
        }

        List<Favorites> QueryFavorites(TwitterContext ctx, ulong sinceID, ulong maxID)
        {
            const int PerQueryCount = 200;

            List<Favorites> result = new List<Favorites>();
            if (sinceID <= 1 && maxID <= 1)
            {
                result = ctx.Favorites
                            .Where(fav => fav.Type == FavoritesType.Favorites &&
                                   fav.Count == PerQueryCount &&
                                   fav.IncludeEntities == false).ToList();
            }
            else
            {
                result = ctx.Favorites
                            .Where(fav => fav.Type == FavoritesType.Favorites &&
                                   fav.Count == PerQueryCount &&
                                   fav.IncludeEntities == false &&
                                   fav.SinceID == sinceID &&
                                   fav.MaxID == maxID).ToList();
            }

            return result;
        }

        List<DirectMessage> QueryDMs(TwitterContext ctx, ulong sinceID, ulong maxID)
        {
            const int PerQueryCount = 200;

            List<DirectMessage> result = new List<DirectMessage>();
            if (sinceID <= 1 && maxID <= 1)
            {
                result = ctx.DirectMessage
                            .Where(dm => dm.Type == DirectMessageType.SentBy &&
                                         dm.Count == PerQueryCount &&
                                         dm.IncludeEntities == false).ToList();

                result.AddRange(ctx.DirectMessage.Where(dm => dm.Type == DirectMessageType.SentTo &&
                                         dm.Count == PerQueryCount &&
                                         dm.IncludeEntities == false).ToList());
            }
            else
            {
                result = ctx.DirectMessage.Where(dm => dm.Type == DirectMessageType.SentBy &&
                                                       dm.Count == PerQueryCount &&
                                                       dm.IncludeEntities == false &&
                                                       dm.SinceID == sinceID &&
                                                       dm.MaxID == maxID).ToList();

                // TODO: Will potentially miss DMs since we are sharing the same 'MaxID' for Sent and Received DMs
                // No need to request these, only first 200 (returned in in above call) are returned
                result.AddRange(ctx.DirectMessage.Where(dm => dm.Type == DirectMessageType.SentTo &&
                                                               dm.Count == PerQueryCount &&
                                                               dm.IncludeEntities == false &&
                                                               dm.SinceID == sinceID &&
                                                               dm.MaxID == maxID).ToList());
            }

            return result;
        }

        void UpdateNumFetchedTweets(int n)
        {
            lblFetched.Dispatcher.BeginInvoke(new Action(delegate()
            {
                lblFetched.Text = string.Format("{0} {1}", n, TweetsEraseType == ApplicationSettings.EraseTypes.DirectMessages ? "DMs" : "Favorites");
            }));
        }

        public static DateTime FromUnixTime(long unixTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(unixTime);
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Environment.Exit(0);
        }

        void HandleReachedTwitterLimits()
        {
            StopProgressStatus();

            SetupTimer();
        }

        void SetupTimer()
        {
            m_Timer.Enabled = true;
        }

        private void DisableTimer()
        {
            m_Timer.Enabled = false;
        }

        void m_Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            var diff = RateLimitReset.Subtract(DateTime.UtcNow);
            var minutes = (int)diff.TotalMinutes;
            var seconds = diff.Seconds;

            if(seconds < 1)
            {
                DisableTimer();

                lblRemainingTime.Dispatcher.BeginInvoke(new Action(delegate()
                {
                    stackReachedLimits.Visibility = System.Windows.Visibility.Collapsed;
                    lblContinue.Visibility = System.Windows.Visibility.Visible;
                    lblRemainingTime.Text = string.Format("{0}min & {1}sec", minutes, seconds);
                }));

                // there are more tweets to fetch
                ToggleIsQueryingTwitterFlag();
                return;
            }

            lblRemainingTime.Dispatcher.BeginInvoke(new Action(delegate()
            {
                stackReachedLimits.Visibility = System.Windows.Visibility.Visible;
                lblContinue.Visibility = System.Windows.Visibility.Visible;
                lblRemainingTime.Text = string.Format("{0}min & {1}sec", minutes, seconds);
            }));
        }
    }
}
