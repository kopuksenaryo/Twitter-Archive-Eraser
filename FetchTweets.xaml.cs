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
        int userFavoritesCount = -1;

        Stopwatch stopWatch;
        

        public FetchTweets()
        {
            InitializeComponent();
            this.Loaded += FetchTweets_Loaded;

            stopWatch = new Stopwatch();
            m_Timer.Interval = 1000;

            new Thread(DoFetchTweets).Start();
        }

        async void FetchTweets_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = ApplicationSettings.GetApplicationSettings();
            twitterCtx = settings.Context;
            TweetsEraseType = settings.EraseType;

            Title += " " + settings.Version;

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

                    if (user != null)
                    {
                        userFavoritesCount = user.FavoritesCount;
                    }

                    lblTweetsType.Text = "favorites";
                    lblTweetsType2.Text = "favorites";
                    lblTweetsMax.Text = "3000";
                    lblTotalTweetsNB.Text = string.Format("(Your favorites count: {0})", userFavoritesCount);
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
            ToggleQueryingTwitter();
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
                page.SetTweetsList(favsTweetList.Select(t => new Tweet()
                {
                    ID = t.StatusID.ToString(),
                    Username = "@" + t.User.Name,
                    Text = t.Text,
                    ToErase = true,
                    Type = TweetType.Favorite,
                    Date = DateTime.SpecifyKind(t.CreatedAt, DateTimeKind.Local)
                }));
            }

            if(TweetsEraseType == ApplicationSettings.EraseTypes.DirectMessages)
            {
                page.SetTweetsList(dmsTweetList.Select(t => new Tweet()
                {
                    ID = t.IDResponse.ToString(),
                    Username = t.Type == DirectMessageType.SentBy ? "[To] @" + t.RecipientScreenName : "[From] @" + t.SenderScreenName,
                    Text = t.Text,
                    ToErase = true,
                    Type = TweetType.DM,
                    Date = DateTime.SpecifyKind(t.CreatedAt, DateTimeKind.Local)
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

        void Set_QueryingTwitter()
        {
            isQueryingTwitter = true;
            btnFetchTxt.Dispatcher.BeginInvoke(new Action(delegate()
            {
                btnFetchTxt.Text = "Stop";
                stopWatch.Start();
            }));
        }

        void Unset_QueryingTwitter()
        {
            isQueryingTwitter = false;
            btnFetchTxt.Dispatcher.BeginInvoke(new Action(delegate ()
            {
                btnFetchTxt.Text = "Start";
                stopWatch.Stop();
            }));
        }

        void ToggleQueryingTwitter()
        {
            if(isQueryingTwitter)
            {
                Unset_QueryingTwitter();
            }
            else
            {
                Set_QueryingTwitter();
            }
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
            ulong maxID_favs = 0;
            ulong maxID_DM_sent = 0;
            ulong maxID_DM_received = 0;

            List<Favorites> favsResponse = new List<Favorites>();
            List<DirectMessage> sentDMsResponse = new List<DirectMessage>();
            List<DirectMessage> receivedDMsResponse = new List<DirectMessage>();

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

                    // Check if we are hitting rate limits
                    if(ReachedTwitterLimits(TweetsEraseType, out RateLimitReset))
                    {
                        RateLimitReset = RateLimitReset.AddSeconds(15); // Add some seconds to avoid querying too soon
                        HandleReachedTwitterLimits();
                        break;
                    }

                    try
                    {
                        switch (TweetsEraseType)
                        {
                            case ApplicationSettings.EraseTypes.TweetsAndRetweets:
                                throw new Exception("EraseTye should never be EraseTypes.TweetsAndRetweets here");
                                break;
                            case ApplicationSettings.EraseTypes.Favorites:
                                favsResponse.Clear();
                                favsResponse = QueryFavorites(twitterCtx, maxID_favs);
                                break;
                            case ApplicationSettings.EraseTypes.DirectMessages:
                                sentDMsResponse.Clear();
                                receivedDMsResponse.Clear();
                                sentDMsResponse = QuerySentDMs(twitterCtx, maxID_DM_sent);
                                receivedDMsResponse = QueryReceivedDMs(twitterCtx, maxID_DM_received);
                                break;
                            default:
                                break;
                        }                      
                    }
                    catch (Exception ex)
                    {
                        if (twitterCtx.RateLimitRemaining == 0)
                        {
                            HandleReachedTwitterLimits();
                        }
                        else
                        {
                            Dispatcher.BeginInvoke(new Action(delegate()
                            {
                                MessageBox.Show(ex.Message);
                            }));
                        }

                        Unset_QueryingTwitter();
                        break;
                    }

                    // fetched all the favorites
                    bool reachedFavsLimitsOrNoMoreFavs = (favsResponse == null || favsResponse.Count == 0 || favsTweetList.Count == userFavoritesCount);
                    bool noMoreDms = (sentDMsResponse == null || sentDMsResponse.Count == 0) && (receivedDMsResponse == null || receivedDMsResponse.Count == 0);

                    if ((TweetsEraseType == ApplicationSettings.EraseTypes.Favorites && reachedFavsLimitsOrNoMoreFavs)
                        || (TweetsEraseType == ApplicationSettings.EraseTypes.DirectMessages && noMoreDms))
                    {
                        lblContinue.Dispatcher.BeginInvoke(new Action(delegate ()
                        {
                            lblContinue.Text = "Done fetching tweets!. Please click 'Next' to go to the filter & delete page.";
                            lblContinue.Visibility = System.Windows.Visibility.Visible;
                            stackReachedLimits.Visibility = System.Windows.Visibility.Collapsed;
                        }));

                        Unset_QueryingTwitter();
                        break;
                    }

                    // query next batch
                    if (TweetsEraseType == ApplicationSettings.EraseTypes.Favorites)
                    {
                        maxID_favs = favsResponse.Min(fav => fav.StatusID) - 1;
                        favsTweetList.AddRange(favsResponse);
                        UpdateNumFetchedTweets(favsTweetList.Count);

                        shouldContinue = favsResponse.Count > 0;
                    }

                    if (TweetsEraseType == ApplicationSettings.EraseTypes.DirectMessages)
                    {
                        maxID_DM_sent = sentDMsResponse.Min(dm => dm.IDResponse) - 1;
                        maxID_DM_received = receivedDMsResponse.Min(dm => dm.IDResponse) - 1;

                        dmsTweetList.AddRange(sentDMsResponse);
                        dmsTweetList.AddRange(receivedDMsResponse);

                        UpdateNumFetchedTweets(dmsTweetList.Count);

                        shouldContinue = sentDMsResponse.Count > 0 || receivedDMsResponse.Count > 0;
                    }

                } while (shouldContinue);
            }
        }

        private bool ReachedTwitterLimits(ApplicationSettings.EraseTypes tweetsEraseType, out DateTime rateLimitReset)
        {
            var helpResponse =
                (from help in twitterCtx.Help
                     where help.Type == HelpType.RateLimits
                     select help)
                    .SingleOrDefault();

            if(helpResponse == null)
            {
                // fail quickly, assume limits are hit
                rateLimitReset = DateTime.Now.AddSeconds(15);   // retry in 15 seconds

                return true;
            }

            var favsRemainingLimits = helpResponse.RateLimits["favorites"].Where(limit => limit.Resource.ToLowerInvariant() == "/favorites/list").FirstOrDefault();
            var sentDMsRemainingLimits = helpResponse.RateLimits["direct_messages"].Where(limit => limit.Resource.ToLowerInvariant() == "/direct_messages/sent").FirstOrDefault();
            var receivedDMsRemainingLimits = helpResponse.RateLimits["direct_messages"].Where(limit => limit.Resource.ToLowerInvariant() == "/direct_messages").FirstOrDefault();

            // The following is OK since the app works only in one given mode: either fetching favorites or DMs
            if(favsRemainingLimits.Remaining == 0)
            {
                rateLimitReset = FromUnixTime(favsRemainingLimits.Reset);
                return true;
            }

            if (sentDMsRemainingLimits.Remaining == 0)
            {
                rateLimitReset = FromUnixTime(sentDMsRemainingLimits.Reset);
                return true;
            }

            if (receivedDMsRemainingLimits.Remaining == 0)
            {
                rateLimitReset = FromUnixTime(receivedDMsRemainingLimits.Reset);
                return true;
            }

            rateLimitReset = DateTime.UtcNow;
            return false;
        }

        List<Favorites> QueryFavorites(TwitterContext ctx, ulong maxID)
        {
            const int PerQueryCount = 200;

            List<Favorites> result = new List<Favorites>();
            if (maxID <= 1)
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
                                   fav.MaxID == maxID).ToList();
            }

            return result;
        }

        List<DirectMessage> QuerySentDMs(TwitterContext ctx, ulong maxID)
        {
            const int PerQueryCount = 200;

            List<DirectMessage> result = new List<DirectMessage>();
            if (maxID <= 1)
            {
                result = ctx.DirectMessage
                            .Where(dm => dm.Type == DirectMessageType.SentBy &&
                                         dm.Count == PerQueryCount &&
                                         dm.IncludeEntities == false).ToList();
            }
            else
            {
                result = ctx.DirectMessage.Where(dm => dm.Type == DirectMessageType.SentBy &&
                                                       dm.Count == PerQueryCount &&
                                                       dm.IncludeEntities == false &&
                                                       dm.MaxID == maxID).ToList();
            }

            return result;
        }

        List<DirectMessage> QueryReceivedDMs(TwitterContext ctx, ulong maxID)
        {
            const int PerQueryCount = 200;

            List<DirectMessage> result = new List<DirectMessage>();
            if (maxID <= 1)
            {
                result = ctx.DirectMessage.Where(dm => dm.Type == DirectMessageType.SentTo &&
                                         dm.Count == PerQueryCount &&
                                         dm.IncludeEntities == false).ToList();
            }
            else
            {
                result = ctx.DirectMessage.Where(dm => dm.Type == DirectMessageType.SentTo &&
                                                               dm.Count == PerQueryCount &&
                                                               dm.IncludeEntities == false &&
                                                               dm.MaxID == maxID).ToList();
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

        public static DateTime FromUnixTime(ulong unixTime)
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
            Unset_QueryingTwitter();
            StopProgressStatus();
            EnableRateLimitTimer();

            // disable querying for new tweets since we hit rate limit anyways
            btnFetch.Dispatcher.BeginInvoke(new Action(delegate ()
             {
                 btnFetch.IsEnabled = false;
             }));
        }

        void EnableRateLimitTimer()
        {
            m_Timer.Elapsed += m_Timer_Elapsed;
            m_Timer.Enabled = true;
        }

        private void DisableRateLimitTimer()
        {
            m_Timer.Elapsed -= m_Timer_Elapsed;
            m_Timer.Enabled = false;
        }

        void m_Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            var timer = sender as System.Timers.Timer;
            if (timer != null && timer.Enabled == false)
            {
                return;
            }

            var diff = RateLimitReset.Subtract(DateTime.UtcNow);
            var minutes = (int)diff.TotalMinutes;
            var seconds = diff.Seconds;

            if(diff.TotalMilliseconds < 1)
            {
                DisableRateLimitTimer();

                lblRemainingTime.Dispatcher.BeginInvoke(new Action(delegate()
                {
                    stackReachedLimits.Visibility = System.Windows.Visibility.Collapsed;
                    lblContinue.Visibility = System.Windows.Visibility.Visible;
                    lblRemainingTime.Text = string.Format("{0}min & {1}sec", minutes, seconds);

                    btnFetch.IsEnabled = true;
                }));

                // there are more tweets to fetch
                Set_QueryingTwitter();
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
