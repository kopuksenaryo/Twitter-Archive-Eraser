using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using LinqToTwitter;
using System.Windows.Media.Animation;

namespace Twitter_Archive_Eraser
{
    /// <summary>
    /// Interaction logic for SendTweet.xaml
    /// </summary>
    public partial class SendTweet : Window
    {
        bool shouldExit = false;
        public SendTweet()
        {
            InitializeComponent();

            this.Loaded += SendTweet_Loaded;
        }

        void SendTweet_Loaded(object sender, RoutedEventArgs e)
        {
            var appSettings = ApplicationSettings.GetApplicationSettings();
            //this.Title += " v" + appSettings.Version;

            if (appSettings.NumTeetsDeleted < 10)
            {
                shouldExit = true;
                btnSendTweetInnerText.Text = "Exit!";
                btnSendTweet.ToolTip = "Not enough tweets to share... :(";
            }

            int mins = (int)appSettings.TotalRunningMillisec / 1000 / 60; 
            int seconds = (int)(appSettings.TotalRunningMillisec - (mins * 60 * 1000)) / 1000;

            string type = appSettings.EraseType == ApplicationSettings.EraseTypes.TweetsAndRetweets ? "tweets" :
                          (appSettings.EraseType == ApplicationSettings.EraseTypes.Favorites ? "favorites" : "DMs");

            string totalTime = "";
            if (mins == 0) 
            { 
                totalTime = seconds + " seconds"; 
            }
            else
            {
                totalTime = mins + ":" + seconds + " min:sec";
            }

            txtTweetText.Text = String.Format("Just deleted {0} {1} using Twitter Archive Eraser by @martani_net (in {2}). Check it out here http://martani.github.io/Twitter-Archive-Eraser/",
                                              appSettings.NumTeetsDeleted,
                                              type,
                                              totalTime);

            FocusManager.SetFocusedElement(txtTweetTextParent, txtTweetText);
        }

        private void btnSendTweet_Click(object sender, RoutedEventArgs e)
        {
            if (!shouldExit)
            {
                btnSendTweet.IsEnabled = false;
                TwitterContext ctx = ApplicationSettings.GetApplicationSettings().Context;

                Status tweet = null;

                try
                {
                    tweet = ctx.TweetAsync(txtTweetText.Text).Result;
                }
                catch (Exception)
                {
                };

                if (tweet != null)
                {
                    txtTweetUpdateStatus.Text = "Sent ...";
                    Thread.Sleep(1000);
                    DialogResult = true;
                    this.Close();
                }
                else
                {
                    txtTweetUpdateStatus.Text = "Failed to send tweet, please try again";
                    btnSendTweet.IsEnabled = true;
                }
            }
            else
            {
                DialogResult = true;
                this.Close();
            }
        }

        private bool closeCompleted = false;


        private void FormFadeOut_Completed(object sender, EventArgs e)
        {
            closeCompleted = true;
            this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!closeCompleted)
            {
                FormFadeOut.Begin();
                e.Cancel = true;
            }
        }

        private void btnNo_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            this.Close();
        }
    }
}
